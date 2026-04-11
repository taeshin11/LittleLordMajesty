# LittleLordMajesty — 4090 PC dev environment setup
#
# Run as the user (NOT admin). Steps that require admin will prompt UAC.
# Idempotent: safe to re-run, skips anything already installed.
#
# Usage:
#   1. git clone https://github.com/taeshin11/LittleLordMajesty.git C:\MakingGames\LittleLordMajesty
#   2. cd C:\MakingGames\LittleLordMajesty
#   3. PowerShell: Set-ExecutionPolicy -Scope Process Bypass; .\tools\migration\setup_4090.ps1
#
# Things this script does NOT do (you do them after):
#   - Install Unity Editor 2022.3.62f1 (use Unity Hub manually with WebGL Build Support)
#   - Copy Assets/Resources/Config/GameConfig.asset (gitignored — has API keys)
#   - Register the GitHub Actions self-hosted runner (token only valid 1h, do it last)

$ErrorActionPreference = "Stop"
$RepoRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName
Write-Host "[setup] Repo root: $RepoRoot" -ForegroundColor Cyan

function Need($cmd) { (Get-Command $cmd -ErrorAction SilentlyContinue) -ne $null }

function Step($msg) {
    Write-Host ""
    Write-Host "==> $msg" -ForegroundColor Yellow
}

# ────────────────────────────────────────────────────────────────────
# 1. Core CLI tools via winget
# ────────────────────────────────────────────────────────────────────
Step "Installing core CLI tools via winget"

$wingetIds = @(
    @{Id = "Git.Git";                Cmd = "git"},
    @{Id = "GitHub.cli";             Cmd = "gh"},
    @{Id = "Python.Python.3.11";     Cmd = "python"},
    @{Id = "OpenJS.NodeJS.LTS";      Cmd = "node"},
    @{Id = "Ollama.Ollama";          Cmd = "ollama"}
)
foreach ($w in $wingetIds) {
    if (Need $w.Cmd) {
        Write-Host "  ✓ $($w.Id) already installed"
    } else {
        Write-Host "  ↓ Installing $($w.Id)..."
        winget install -e --id $w.Id --accept-source-agreements --accept-package-agreements
    }
}

# Refresh PATH for current session so subsequent commands see new tools
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + `
            [System.Environment]::GetEnvironmentVariable("Path","User")

# ────────────────────────────────────────────────────────────────────
# 2. Python ML stack — torch (CUDA 12.4) + diffusers + transformers
# ────────────────────────────────────────────────────────────────────
Step "Installing Python ML stack (torch CUDA 12.4 + diffusers)"

python -m pip install --upgrade pip
python -c "import torch; assert torch.cuda.is_available()" 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ↓ Installing torch with CUDA 12.4 wheels (~3 GB)..."
    pip install torch torchvision --index-url https://download.pytorch.org/whl/cu124
} else {
    Write-Host "  ✓ torch + CUDA already working"
}

pip install diffusers transformers accelerate safetensors playwright huggingface_hub
playwright install chromium
Write-Host "  ✓ Python ML stack ready"

# ────────────────────────────────────────────────────────────────────
# 3. Ollama models — EXAONE 3.5 (32B fits 24 GB VRAM, 7.8B as fallback)
# ────────────────────────────────────────────────────────────────────
Step "Pulling Ollama models (EXAONE 3.5 32B + 7.8B)"

# Wait for ollama service to come up
$tries = 0
while ($tries -lt 10) {
    try { $null = Invoke-WebRequest -Uri "http://localhost:11434" -UseBasicParsing -TimeoutSec 1; break }
    catch { Start-Sleep -Seconds 1; $tries++ }
}
if ($tries -ge 10) {
    Write-Host "  ! Ollama service not responding — start manually with 'ollama serve' in another window"
} else {
    & ollama pull exaone3.5:7.8b   # ~5 GB, fast smoke test
    & ollama pull exaone3.5:32b    # ~20 GB, takes 10-30 min on first run
    & ollama list
}

# ────────────────────────────────────────────────────────────────────
# 4. Node deps for Playwright live-test harness
# ────────────────────────────────────────────────────────────────────
Step "Installing Node deps for live test harness"
Push-Location "$RepoRoot\tools\playwright_test"
if (Test-Path package.json) {
    npm install
    Write-Host "  ✓ Playwright harness ready"
}
Pop-Location

# ────────────────────────────────────────────────────────────────────
# 5. Final reminders
# ────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host " Auto-installable parts done. Manual steps remaining:" -ForegroundColor Green
Write-Host "════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
Write-Host " 1. Install Unity Editor 2022.3.62f1 via Unity Hub"
Write-Host "    https://unity.com/releases/editor/whats-new/2022.3.62"
Write-Host "    REQUIRED MODULE: WebGL Build Support"
Write-Host ""
Write-Host " 2. Copy GameConfig.asset (gitignored, has Gemini API key)"
Write-Host "    From laptop: C:\MakingGames\LittleLordMajesty\Assets\Resources\Config\GameConfig.asset(.meta)"
Write-Host "    To 4090   : same path"
Write-Host "    Use OneDrive, USB stick, or scp — anything but commit it"
Write-Host ""
Write-Host " 3. Register this PC as a GitHub Actions self-hosted runner"
Write-Host "    Open: https://github.com/taeshin11/LittleLordMajesty/settings/actions/runners/new"
Write-Host "    Pick Windows x64. Token is valid for 1 hour."
Write-Host "    Default install location: C:\actions-runner"
Write-Host "    After config, run as service:  cd C:\actions-runner; .\svc.cmd install; .\svc.cmd start"
Write-Host ""
Write-Host " 4. Deregister the laptop runner"
Write-Host "    Same settings page, find the laptop runner, ... → Remove"
Write-Host ""
Write-Host " 5. Smoke test:"
Write-Host "    cd $RepoRoot"
Write-Host "    node tools/playwright_test/live_test.js     # should report Page errors: 0"
Write-Host "    python tools/dialogue_gen/generate.py       # should generate Korean dialogue"
Write-Host "    python tools/image_gen/generate.py          # should generate art"
Write-Host ""
Write-Host " You're done. Push any change to test the new runner end-to-end."
