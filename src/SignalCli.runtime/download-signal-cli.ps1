<#
.SYNOPSIS
  Downloads, verifies (SHA-256) and extracts the signal-cli distribution into <OutDir>.

  Intentionally ASCII-only: Windows PowerShell 5.1 mis-parses non-ASCII .ps1 files
  saved without a UTF-8 BOM, so we avoid non-ASCII characters entirely.

  Extraction is staged through an ASCII temp directory and then copied into <OutDir>,
  because the bundled Windows tar (bsdtar) fails to extract directly into paths that
  contain non-ASCII characters (e.g. a localized user-profile or folder name).
#>
param (
    [string]$OutDir = "signal-cli",
    # post-modernize-tuning §8d.4-§8d.6 (audit N6): Version і Sha256 тепер передаються
    # ззовні (з csproj <SignalCliVersion>/<SignalCliSha256> через Exec Command), щоб
    # bump-version був single-csproj-edit. Дефолти лишаємо для прямого виклику скрипта
    # без csproj-binding'у (наприклад, локальне дослідження).
    [string]$Version = "0.14.3",
    [string]$Sha256 = "60a0a51312d07ed0cd6f4d5080b2ffe6ee838ea99f92297f2803e24af1826c6f"
)

$ErrorActionPreference = 'Stop'

$SignalCliBin = Join-Path $OutDir "bin\signal-cli.bat"

if (Test-Path $SignalCliBin) {
    Write-Host "signal-cli already present in $OutDir, skipping download."
    exit 0
}

$Filename = "signal-cli-$Version.tar.gz"
$Url = "https://github.com/AsamK/signal-cli/releases/download/v$Version/$Filename"
# SHA-256 verified against GitHub Releases; canonical value lives in
# src/SignalCli.runtime/SignalCli.runtime.csproj <SignalCliSha256>.
$ExpectedSha256 = $Sha256

# Use a fresh ASCII temp directory for download + extraction.
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("signal-cli-" + [System.Guid]::NewGuid().ToString())
$archive = Join-Path $temp $Filename
$staging = Join-Path $temp "extract"

try {
    New-Item -ItemType Directory -Force -Path $staging | Out-Null

    Write-Host "Downloading $Filename ..."
    $ProgressPreference = "SilentlyContinue"   # faster large download, no progress bar in CI
    Invoke-WebRequest -Uri $Url -OutFile $archive

    Write-Host "Verifying SHA-256 ..."
    $actualSha256 = (Get-FileHash -Path $archive -Algorithm SHA256).Hash
    if ($actualSha256 -ine $ExpectedSha256) {
        throw "SHA-256 mismatch! expected $ExpectedSha256 but got $actualSha256"
    }
    Write-Host "SHA-256 OK."

    Write-Host "Extracting ..."
    # Use the Windows system tar (bsdtar) explicitly: a "tar" on PATH may be Git's
    # GNU tar, which treats the "C:\..." archive path as a remote host and fails.
    $tarExe = Join-Path $env:SystemRoot 'System32\tar.exe'
    if (-not (Test-Path $tarExe)) { $tarExe = 'tar' }
    # bsdtar may emit non-fatal warnings (e.g. about symlinks); validate success by
    # checking for the launcher below instead of relying on the exit code/stderr.
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & $tarExe -xf $archive -C "$staging" --strip-components=1 2>$null
    $ErrorActionPreference = $prevEap

    $stagedBin = Join-Path $staging "bin\signal-cli.bat"
    if (-not (Test-Path $stagedBin)) {
        throw "Extraction failed: $stagedBin not found"
    }

    Write-Host "Installing into $OutDir ..."
    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
    Copy-Item -Path (Join-Path $staging '*') -Destination $OutDir -Recurse -Force

    Write-Host "Done. signal-cli v$Version installed to $OutDir"
}
finally {
    Remove-Item -Path $temp -Recurse -Force -ErrorAction SilentlyContinue
}
