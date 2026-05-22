<#
.SYNOPSIS
  Downloads, verifies (SHA-256) and normalizes an Eclipse Temurin JRE so that
  "<OutDir>/bin/java[.exe]" exists. Bundled by the SignalCli.Runtime.Jre.* packages
  so consumers do not need a system Java installation.

  Intentionally ASCII-only: Windows PowerShell 5.1 mis-parses non-ASCII .ps1 files
  that are saved without a UTF-8 BOM, so we avoid non-ASCII characters entirely.
#>
param(
    [Parameter(Mandatory = $true)][string]$OutDir,
    [Parameter(Mandatory = $true)][string]$Feature,   # e.g. 25
    [Parameter(Mandatory = $true)][string]$Version,   # e.g. 25.0.3+9
    [Parameter(Mandatory = $true)][string]$Os,        # windows | mac
    [Parameter(Mandatory = $true)][string]$Arch,      # x64 | aarch64
    [Parameter(Mandatory = $true)][string]$Sha256
)

$ErrorActionPreference = 'Stop'

# Idempotent: skip if a JRE is already present (offline / incremental builds).
$existing = Get-ChildItem -Path $OutDir -Recurse -Include 'java.exe', 'java' -ErrorAction SilentlyContinue |
    Where-Object { $_.Directory.Name -eq 'bin' } | Select-Object -First 1
if ($existing) {
    Write-Host "JRE already present in $OutDir, skipping download."
    exit 0
}

$tagVer = $Version -replace '\+', '%2B'   # release tag: jdk-25.0.3%2B9
$fnVer = $Version -replace '\+', '_'      # asset version: 25.0.3_9
$ext = if ($Os -eq 'windows') { 'zip' } else { 'tar.gz' }
$url = "https://github.com/adoptium/temurin$Feature-binaries/releases/download/jdk-$tagVer/OpenJDK${Feature}U-jre_${Arch}_${Os}_hotspot_$fnVer.$ext"

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    $archive = Join-Path $tmp "jre.$ext"
    Write-Host "Downloading $url ..."
    Invoke-WebRequest -Uri $url -OutFile $archive

    Write-Host "Verifying SHA-256 ..."
    $actual = (Get-FileHash -Algorithm SHA256 $archive).Hash.ToLower()
    if ($actual -ne $Sha256.ToLower()) {
        throw "SHA-256 mismatch! expected $Sha256 but got $actual"
    }
    Write-Host "SHA-256 OK."

    $extract = Join-Path $tmp 'extract'
    New-Item -ItemType Directory -Path $extract | Out-Null
    if ($ext -eq 'zip') {
        Expand-Archive -Path $archive -DestinationPath $extract -Force
    }
    else {
        # Use the Windows system tar (bsdtar) explicitly: a "tar" on PATH may be Git's
        # GNU tar, which treats the "C:\..." archive path as a remote host and fails.
        $tarExe = Join-Path $env:SystemRoot 'System32\tar.exe'
        if (-not (Test-Path $tarExe)) { $tarExe = 'tar' }
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        & $tarExe -xzf $archive -C $extract 2>$null
        $ErrorActionPreference = $prevEap
    }

    # Locate the JRE home: the directory whose 'bin' subfolder contains java[.exe].
    # Handles Windows (<root>/bin/java.exe) and macOS (<root>/Contents/Home/bin/java).
    $javaBin = Get-ChildItem -Path $extract -Recurse -Include 'java.exe', 'java' -ErrorAction SilentlyContinue |
        Where-Object { $_.Directory.Name -eq 'bin' } | Select-Object -First 1
    if (-not $javaBin) { throw "java executable not found after extraction" }
    $jreHome = $javaBin.Directory.Parent.FullName

    Write-Host "Normalizing JRE layout into $OutDir ..."
    if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
    New-Item -ItemType Directory -Path $OutDir | Out-Null
    Copy-Item -Path (Join-Path $jreHome '*') -Destination $OutDir -Recurse -Force

    Write-Host "JRE (Temurin $Version, $Os/$Arch) installed to $OutDir"
}
finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
