"""Global GPU lock — cross-project shared VRAM coordination.

Schema v2 (shared mode): multiple holders can coexist as long as the sum of
their declared VRAM stays under the GPU capacity. Backwards compatible with
v1 (single-holder) lock files via on-read migration.

## Why
Multiple ML projects on one machine fight for VRAM. Without coordination:
  - One job crashes the others (OOM)
  - Killing zombies loses work
  - Suspending processes is fragile

With this lock, projects declare their VRAM need; the file tracks all
holders, refuses to admit a new one if total would exceed capacity, and
auto-cleans dead/expired holders on every operation. Cross-platform file
locking (msvcrt on Windows, fcntl on POSIX) prevents read-modify-write
races between concurrent acquirers.

## Usage in your script

```python
from gpu_lock import acquire, release
import atexit

if not acquire("MyProject_training", vram_mb=8000, on_busy="wait"):
    print("GPU busy, exiting")
    sys.exit(0)
atexit.register(release)

# ... your GPU code ...
```

## Or as context manager

```python
from gpu_lock import gpu_lock_context

with gpu_lock_context("MyProject", vram_mb=8000):
    train_model()
```

## CLI

```bash
python gpu_lock.py --status         # show all holders + capacity
python gpu_lock.py --force-release  # emergency clean (use with care)
```

## State file (schema v2)

```json
{
  "schema_version": 2,
  "capacity_mb": 24000,
  "holders": [
    {"pid": 1234, "name": "SPINAI_training", "vram_mb": 18000,
     "started_at": 1700000000.0, "expires_at": 1700043200.0}
  ]
}
```
"""
from __future__ import annotations

import argparse
import atexit
import contextlib
import json
import os
import sys
import time
from pathlib import Path
from typing import List, Optional

# Global lock location — all cooperating projects must use this
LOCK_FILE = Path(os.environ.get(
    "GPU_LOCK_FILE",
    str(Path.home() / "gpu_lock.json")
))
SENTINEL_FILE = Path(str(LOCK_FILE) + ".lock")

SCHEMA_VERSION = 2
DEFAULT_TTL_HOURS = 12
DEFAULT_CAPACITY_MB = 24000


def _now() -> float:
    return time.time()


# ── Capacity detection ──────────────────────────────────────────────────────

_capacity_cache: Optional[int] = None


def _detect_capacity() -> int:
    """Detect GPU total VRAM in MB. Order: env var → pynvml → fallback."""
    global _capacity_cache
    if _capacity_cache is not None:
        return _capacity_cache

    env = os.environ.get("GPU_CAPACITY_MB")
    if env:
        try:
            _capacity_cache = int(env)
            return _capacity_cache
        except ValueError:
            pass

    try:
        import pynvml  # type: ignore
        pynvml.nvmlInit()
        try:
            handle = pynvml.nvmlDeviceGetHandleByIndex(0)
            info = pynvml.nvmlDeviceGetMemoryInfo(handle)
            _capacity_cache = int(info.total / 1024 / 1024)
            return _capacity_cache
        finally:
            try:
                pynvml.nvmlShutdown()
            except Exception:
                pass
    except Exception:
        pass

    _capacity_cache = DEFAULT_CAPACITY_MB
    return _capacity_cache


# ── PID liveness ────────────────────────────────────────────────────────────

def _is_pid_alive(pid: int) -> bool:
    if not pid or pid <= 0:
        return False
    try:
        import psutil  # type: ignore
        return psutil.pid_exists(pid)
    except ImportError:
        pass
    if os.name == "nt":
        try:
            import ctypes  # type: ignore
            PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
            STILL_ACTIVE = 259
            handle = ctypes.windll.kernel32.OpenProcess(
                PROCESS_QUERY_LIMITED_INFORMATION, False, pid
            )
            if not handle:
                return False
            try:
                exit_code = ctypes.c_ulong()
                ok = ctypes.windll.kernel32.GetExitCodeProcess(
                    handle, ctypes.byref(exit_code)
                )
                if not ok:
                    return False
                return exit_code.value == STILL_ACTIVE
            finally:
                ctypes.windll.kernel32.CloseHandle(handle)
        except Exception:
            return True  # uncertain → assume alive (safer)
    else:
        try:
            os.kill(pid, 0)
            return True
        except ProcessLookupError:
            return False
        except PermissionError:
            return True
        except OSError:
            return False


# ── Cross-platform file lock (sentinel-based) ───────────────────────────────

class _FileLock:
    """OS-level exclusive file lock using msvcrt (Windows) or fcntl (POSIX).

    Locks a sentinel byte in `SENTINEL_FILE` so multiple processes can
    serialize their read-modify-write of the JSON state file.
    """

    def __init__(self, sentinel_path: Path):
        self.sentinel_path = sentinel_path
        self.fp = None

    def __enter__(self):
        self.sentinel_path.parent.mkdir(parents=True, exist_ok=True)
        # 'a+b' = create if missing, append+read binary; we never use the data
        self.fp = open(self.sentinel_path, "a+b")
        if os.name == "nt":
            import msvcrt
            self.fp.seek(0)
            # LK_LOCK retries for ~10s, then raises OSError. Loop until success.
            while True:
                try:
                    msvcrt.locking(self.fp.fileno(), msvcrt.LK_LOCK, 1)
                    break
                except OSError:
                    time.sleep(0.05)
        else:
            import fcntl  # type: ignore
            fcntl.flock(self.fp.fileno(), fcntl.LOCK_EX)
        return self

    def __exit__(self, exc_type, exc, tb):
        if self.fp is None:
            return
        try:
            if os.name == "nt":
                import msvcrt
                self.fp.seek(0)
                try:
                    msvcrt.locking(self.fp.fileno(), msvcrt.LK_UNLCK, 1)
                except OSError:
                    pass
            else:
                import fcntl  # type: ignore
                fcntl.flock(self.fp.fileno(), fcntl.LOCK_UN)
        finally:
            try:
                self.fp.close()
            except Exception:
                pass
            self.fp = None


# ── State I/O (must be called inside a _FileLock) ───────────────────────────

def _new_state() -> dict:
    return {
        "schema_version": SCHEMA_VERSION,
        "capacity_mb": _detect_capacity(),
        "holders": [],
    }


def _migrate_v1(data: dict) -> dict:
    """Convert legacy single-holder dict to v2 multi-holder schema."""
    started_at = float(data.get("started_at", _now()))
    expires_at = float(
        data.get("expires_at", _now() + DEFAULT_TTL_HOURS * 3600)
    )
    holder = {
        "pid": int(data.get("holder_pid", 0)),
        "name": str(data.get("holder_name", "?")),
        "vram_mb": int(data.get("vram_estimate_mb", 0)),
        "started_at": started_at,
        "started_at_iso": data.get("started_at_iso") or time.strftime(
            "%Y-%m-%d %H:%M:%S", time.localtime(started_at)
        ),
        "expires_at": expires_at,
        "expires_at_iso": data.get("expires_at_iso") or time.strftime(
            "%Y-%m-%d %H:%M:%S", time.localtime(expires_at)
        ),
    }
    return {
        "schema_version": SCHEMA_VERSION,
        "capacity_mb": _detect_capacity(),
        "holders": [holder] if holder["pid"] > 0 else [],
    }


def _read_state() -> dict:
    """Read state file. Migrates v1 → v2. Returns empty v2 state if missing.

    NOTE: Caller must hold the _FileLock for safe RMW. Reads outside the lock
    are best-effort (used by `status()`).
    """
    if not LOCK_FILE.exists():
        return _new_state()
    try:
        data = json.loads(LOCK_FILE.read_text(encoding="utf-8"))
    except Exception:
        return _new_state()
    if not isinstance(data, dict):
        return _new_state()

    # v1 schema (no schema_version, has holder_pid)
    if "schema_version" not in data and "holder_pid" in data:
        return _migrate_v1(data)

    # v2 schema
    if data.get("schema_version") == SCHEMA_VERSION and "holders" in data:
        # Refresh capacity (env var or pynvml may have changed since file write)
        data["capacity_mb"] = data.get("capacity_mb") or _detect_capacity()
        holders = data.get("holders") or []
        if not isinstance(holders, list):
            holders = []
        data["holders"] = holders
        return data

    # Unknown / future schema → start fresh (don't trash the file though)
    return _new_state()


def _write_state(state: dict) -> None:
    LOCK_FILE.parent.mkdir(parents=True, exist_ok=True)
    state["schema_version"] = SCHEMA_VERSION
    LOCK_FILE.write_text(json.dumps(state, indent=2), encoding="utf-8")


def _cleanup_holders(holders: List[dict]) -> List[dict]:
    """Drop holders whose pid is dead or whose lease has expired."""
    now = _now()
    alive = []
    for h in holders:
        if not isinstance(h, dict):
            continue
        pid = int(h.get("pid", 0))
        if not _is_pid_alive(pid):
            continue
        exp = float(h.get("expires_at", 0) or 0)
        if exp and now > exp:
            continue
        alive.append(h)
    return alive


def _used_mb(holders: List[dict]) -> int:
    return sum(int(h.get("vram_mb", 0)) for h in holders)


# ── Public API ──────────────────────────────────────────────────────────────

def acquire(
    name: str,
    vram_mb: int,
    on_busy: str = "wait",
    poll_interval: float = 10.0,
    max_wait: Optional[float] = None,
    ttl_hours: float = DEFAULT_TTL_HOURS,
) -> bool:
    """Acquire a share of GPU VRAM.

    Multiple holders can coexist as long as
    `sum(holder.vram_mb) + vram_mb <= capacity_mb`.

    Args:
        name: Descriptive name (logged + visible in --status)
        vram_mb: Estimated VRAM usage in MB
        on_busy: "wait" (block, default) | "skip" (return False) | "error" (raise)
        poll_interval: Seconds between retries when waiting
        max_wait: Max seconds to wait (None = forever)
        ttl_hours: Auto-expire after this many hours

    Returns:
        True if acquired, False if skipped (on_busy="skip" + insufficient room)
    """
    if on_busy not in ("wait", "skip", "error"):
        raise ValueError(f"on_busy must be wait/skip/error, got {on_busy}")

    vram_mb = int(vram_mb)
    waited = 0.0

    while True:
        with _FileLock(SENTINEL_FILE):
            state = _read_state()
            state["holders"] = _cleanup_holders(state["holders"])
            capacity = int(state.get("capacity_mb") or _detect_capacity())
            state["capacity_mb"] = capacity
            used = _used_mb(state["holders"])
            free = capacity - used

            if vram_mb <= free:
                # Fits — append my entry and write
                now = _now()
                exp = now + ttl_hours * 3600
                entry = {
                    "pid": os.getpid(),
                    "name": name,
                    "vram_mb": vram_mb,
                    "started_at": now,
                    "started_at_iso": time.strftime(
                        "%Y-%m-%d %H:%M:%S", time.localtime(now)
                    ),
                    "expires_at": exp,
                    "expires_at_iso": time.strftime(
                        "%Y-%m-%d %H:%M:%S", time.localtime(exp)
                    ),
                }
                state["holders"].append(entry)
                _write_state(state)
                print(
                    f"[gpu_lock] Acquired by {name} (pid={os.getpid()}, "
                    f"vram~{vram_mb}MB) | total {used + vram_mb}/{capacity}MB",
                    flush=True,
                )
                return True

            # Not enough room — snapshot info for the on_busy branch
            holder_str = ", ".join(
                f"{h['name']}:{h['vram_mb']}MB(pid={h['pid']})"
                for h in state["holders"]
            ) or "(none)"

        # Outside file lock now
        if on_busy == "skip":
            print(
                f"[gpu_lock] {name} needs {vram_mb}MB but only {free}MB free "
                f"of {capacity}MB; holders: [{holder_str}]. Skipping.",
                flush=True,
            )
            return False
        if on_busy == "error":
            raise RuntimeError(
                f"Cannot acquire {vram_mb}MB; {free}MB free of {capacity}MB; "
                f"holders: [{holder_str}]"
            )

        if max_wait is not None and waited >= max_wait:
            raise TimeoutError(
                f"{name} waited {waited:.0f}s for {vram_mb}MB GPU room; gave up"
            )
        if int(waited) % 60 == 0:
            print(
                f"[gpu_lock] {name} waiting for {vram_mb}MB; currently used "
                f"{used}/{capacity}MB, holders: [{holder_str}]",
                flush=True,
            )
        time.sleep(poll_interval)
        waited += poll_interval


def release() -> bool:
    """Remove this process's holder entry. Safe to call always."""
    my_pid = os.getpid()
    with _FileLock(SENTINEL_FILE):
        if not LOCK_FILE.exists():
            return False
        state = _read_state()
        before = len(state["holders"])
        state["holders"] = [
            h for h in state["holders"] if int(h.get("pid", 0)) != my_pid
        ]
        if len(state["holders"]) == before:
            return False  # we weren't a holder
        if state["holders"]:
            _write_state(state)
        else:
            try:
                LOCK_FILE.unlink()
            except Exception:
                # If unlink fails, at least leave a clean state file
                _write_state(state)
        print(f"[gpu_lock] Released by pid={my_pid}", flush=True)
        return True


def force_release() -> bool:
    """Delete the entire lock file (cleanup nuke). Use only if stuck."""
    with _FileLock(SENTINEL_FILE):
        if LOCK_FILE.exists():
            try:
                LOCK_FILE.unlink()
                return True
            except Exception:
                return False
    return False


def status() -> Optional[dict]:
    """Return current state with dead holders removed, or None if no holders."""
    with _FileLock(SENTINEL_FILE):
        state = _read_state()
        state["holders"] = _cleanup_holders(state["holders"])
        state["capacity_mb"] = int(state.get("capacity_mb") or _detect_capacity())
        if not state["holders"]:
            return None
        return state


@contextlib.contextmanager
def gpu_lock_context(
    name: str,
    vram_mb: int,
    on_busy: str = "wait",
    max_wait: Optional[float] = None,
):
    """Context manager wrapping acquire/release.

    Yields True if acquired, False if skipped (on_busy="skip" + busy).

    Usage:
        with gpu_lock_context("training", vram_mb=8000) as ok:
            if not ok:
                sys.exit(0)
            train()
    """
    acquired = acquire(name, vram_mb, on_busy=on_busy, max_wait=max_wait)
    try:
        yield acquired
    finally:
        if acquired:
            release()


# Auto-release on interpreter exit (covers scripts that forget)
def _cleanup_on_exit():
    try:
        # Best-effort: only if we're still listed
        my_pid = os.getpid()
        if not LOCK_FILE.exists():
            return
        # Use a non-blocking quick check first to avoid hangs at shutdown
        try:
            data = json.loads(LOCK_FILE.read_text(encoding="utf-8"))
        except Exception:
            return
        if "schema_version" in data:
            holders = data.get("holders") or []
        elif "holder_pid" in data:
            holders = [{"pid": data.get("holder_pid")}]
        else:
            holders = []
        if any(int(h.get("pid", 0)) == my_pid for h in holders):
            release()
    except Exception:
        pass


atexit.register(_cleanup_on_exit)


# ── CLI ─────────────────────────────────────────────────────────────────────

def _cli():
    p = argparse.ArgumentParser()
    p.add_argument("--status", action="store_true", help="Show all holders + capacity")
    p.add_argument("--force-release", action="store_true",
                   help="Force-delete the lock file (cleanup)")
    p.add_argument("--lock-file", help="Override lock file path (testing)")
    args = p.parse_args()

    if args.lock_file:
        global LOCK_FILE, SENTINEL_FILE
        LOCK_FILE = Path(args.lock_file)
        SENTINEL_FILE = Path(str(LOCK_FILE) + ".lock")

    if args.force_release:
        ok = force_release()
        print("Released" if ok else "No lock file")
        return

    s = status()
    if s is None:
        capacity = _detect_capacity()
        print(f"GPU lock: 0/{capacity} MB used ({capacity} free)")
        print(f"  (no holders)  file={LOCK_FILE}")
        return

    holders = s.get("holders", [])
    capacity = int(s.get("capacity_mb") or _detect_capacity())
    used = _used_mb(holders)
    free = capacity - used

    print(f"GPU lock: {used}/{capacity} MB used ({free} free)")
    for i, h in enumerate(holders, 1):
        exp_iso = h.get("expires_at_iso") or ""
        if not exp_iso:
            try:
                exp_iso = time.strftime(
                    "%Y-%m-%d %H:%M:%S",
                    time.localtime(float(h.get("expires_at", 0))),
                )
            except Exception:
                exp_iso = "?"
        name = str(h.get("name", "?"))
        pid = int(h.get("pid", 0))
        vram = int(h.get("vram_mb", 0))
        print(
            f"  [{i}] {name:<26} pid={pid}  vram~{vram} MB  expires {exp_iso}"
        )


if __name__ == "__main__":
    _cli()
