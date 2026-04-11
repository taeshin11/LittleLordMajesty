"""Global GPU lock — cross-project mutex for sharing a single GPU.

This module provides a file-based mutex that ANY local project can use to
coordinate GPU access. No central daemon required. The lock file lives at a
fixed location all projects can read/write.

## Why
Multiple ML projects on one machine fight for VRAM. Without coordination:
  - One job crashes the others (OOM)
  - Killing zombies loses work
  - Suspending processes is fragile

With this lock:
  - Each job declares its VRAM need before starting
  - If GPU is held by someone else, the job waits / skips / errors
  - Lock auto-expires (12h) and auto-cleans on holder death

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
    # GPU work here
    train_model()
```

## CLI

```bash
python gpu_lock.py --status         # show current holder
python gpu_lock.py --force-release  # emergency unlock (use with care)
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
from typing import Optional

# Global lock location — all cooperating projects must use this
LOCK_FILE = Path(os.environ.get(
    "GPU_LOCK_FILE",
    str(Path.home() / "gpu_lock.json")
))

# Default lock TTL (auto-expire after this if not refreshed)
DEFAULT_TTL_HOURS = 12


def _now() -> float:
    return time.time()


def _read_state() -> Optional[dict]:
    if not LOCK_FILE.exists():
        return None
    try:
        return json.loads(LOCK_FILE.read_text(encoding="utf-8"))
    except Exception:
        return None


def _write_state(state: dict) -> None:
    LOCK_FILE.parent.mkdir(parents=True, exist_ok=True)
    LOCK_FILE.write_text(json.dumps(state, indent=2), encoding="utf-8")


def _is_pid_alive(pid: int) -> bool:
    try:
        import psutil
        return psutil.pid_exists(pid)
    except ImportError:
        # Fallback for systems without psutil
        try:
            os.kill(pid, 0)
            return True
        except (ProcessLookupError, PermissionError):
            return pid > 0  # PermissionError = exists but no access
        except OSError:
            return False


def _is_holder_active(state: dict) -> bool:
    """Check if the current lock holder is still active."""
    if not state:
        return False
    pid = state.get("holder_pid")
    if not pid:
        return False
    if not _is_pid_alive(pid):
        return False
    expires = state.get("expires_at", 0)
    if expires and _now() > expires:
        return False
    return True


def acquire(
    name: str,
    vram_mb: int,
    on_busy: str = "wait",
    poll_interval: float = 10.0,
    max_wait: Optional[float] = None,
    ttl_hours: float = DEFAULT_TTL_HOURS,
) -> bool:
    """Acquire the global GPU lock.

    Args:
        name: Descriptive name of the job (for logging / status)
        vram_mb: Estimated VRAM usage in MB (for visibility)
        on_busy: What to do if GPU is busy:
            - "wait": block until released (default)
            - "skip": return False immediately
            - "error": raise RuntimeError
        poll_interval: Seconds between checks when waiting
        max_wait: Max seconds to wait (None = forever)
        ttl_hours: Auto-expire after this many hours

    Returns:
        True if lock acquired, False if skipped (on_busy="skip" + busy)

    Raises:
        RuntimeError if on_busy="error" and busy
        TimeoutError if max_wait exceeded
    """
    if on_busy not in ("wait", "skip", "error"):
        raise ValueError(f"on_busy must be wait/skip/error, got {on_busy}")

    waited = 0.0
    while True:
        state = _read_state()
        if not _is_holder_active(state):
            # Lock is free — claim it
            new_state = {
                "holder_pid": os.getpid(),
                "holder_name": name,
                "vram_estimate_mb": int(vram_mb),
                "started_at": _now(),
                "started_at_iso": time.strftime("%Y-%m-%d %H:%M:%S"),
                "expires_at": _now() + ttl_hours * 3600,
                "expires_at_iso": time.strftime(
                    "%Y-%m-%d %H:%M:%S", time.localtime(_now() + ttl_hours * 3600)
                ),
            }
            _write_state(new_state)
            print(f"[gpu_lock] Acquired by {name} (pid={os.getpid()}, vram~{vram_mb}MB)",
                  flush=True)
            return True

        # Held by another process
        holder = state.get("holder_name", "?")
        holder_pid = state.get("holder_pid")
        holder_vram = state.get("vram_estimate_mb", "?")

        if on_busy == "skip":
            print(f"[gpu_lock] GPU held by {holder} (pid={holder_pid}, "
                  f"vram~{holder_vram}MB). Skipping.", flush=True)
            return False
        if on_busy == "error":
            raise RuntimeError(
                f"GPU held by {holder} (pid={holder_pid}, vram~{holder_vram}MB)"
            )

        # wait
        if max_wait is not None and waited >= max_wait:
            raise TimeoutError(
                f"Waited {waited:.0f}s for GPU lock held by {holder}, gave up"
            )
        if int(waited) % 60 == 0:  # log every minute
            print(f"[gpu_lock] Waiting for {holder} (pid={holder_pid}, "
                  f"vram~{holder_vram}MB)... {waited:.0f}s elapsed", flush=True)
        time.sleep(poll_interval)
        waited += poll_interval


def release() -> bool:
    """Release the lock if held by current process. Safe to call always."""
    state = _read_state()
    if state and state.get("holder_pid") == os.getpid():
        try:
            LOCK_FILE.unlink()
            print(f"[gpu_lock] Released by pid={os.getpid()}", flush=True)
            return True
        except Exception:
            pass
    return False


def force_release() -> bool:
    """Force release any lock (use only for cleanup of stuck locks)."""
    if LOCK_FILE.exists():
        try:
            LOCK_FILE.unlink()
            return True
        except Exception:
            return False
    return False


def status() -> Optional[dict]:
    """Return current lock state, or None if free."""
    state = _read_state()
    if not _is_holder_active(state):
        return None
    return state


@contextlib.contextmanager
def gpu_lock_context(name: str, vram_mb: int, on_busy: str = "wait",
                     max_wait: Optional[float] = None):
    """Context manager wrapping acquire/release.

    Usage:
        with gpu_lock_context("training", vram_mb=8000):
            train()
    """
    acquired = acquire(name, vram_mb, on_busy=on_busy, max_wait=max_wait)
    if not acquired:
        yield False
        return
    try:
        yield True
    finally:
        release()


# Auto-register release on interpreter exit (for scripts that forget)
def _cleanup_on_exit():
    state = _read_state()
    if state and state.get("holder_pid") == os.getpid():
        release()


atexit.register(_cleanup_on_exit)


def _cli():
    p = argparse.ArgumentParser()
    p.add_argument("--status", action="store_true", help="Show current lock holder")
    p.add_argument("--force-release", action="store_true", help="Force unlock (cleanup)")
    p.add_argument("--lock-file", help="Override lock file path")
    args = p.parse_args()

    if args.lock_file:
        global LOCK_FILE
        LOCK_FILE = Path(args.lock_file)

    if args.force_release:
        ok = force_release()
        print("Released" if ok else "No lock to release")
        return

    s = status()
    if s is None:
        print(f"GPU lock: FREE (file: {LOCK_FILE})")
    else:
        print(f"GPU lock: HELD")
        print(f"  Holder:  {s['holder_name']}")
        print(f"  PID:     {s['holder_pid']}")
        print(f"  VRAM:    ~{s['vram_estimate_mb']} MB")
        print(f"  Started: {s.get('started_at_iso', '?')}")
        print(f"  Expires: {s.get('expires_at_iso', '?')}")


if __name__ == "__main__":
    _cli()
