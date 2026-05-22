param (
    [string]$OutDir = "signal-cli"
)

$SignalCliBin = Join-Path $OutDir "bin\signal-cli.bat"

if (Test-Path $SignalCliBin) {
    Write-Host "✅ signal-cli вже існує в $OutDir, завантаження не потрібне."
    exit 0
}

$Version = "0.14.3"
$Filename = "signal-cli-$Version.tar.gz"
$Url = "https://github.com/AsamK/signal-cli/releases/download/v$Version/$Filename"
# SHA-256 офіційного релізного архіву (звірено із завантаженням з GitHub Releases).
$ExpectedSha256 = "60a0a51312d07ed0cd6f4d5080b2ffe6ee838ea99f92297f2803e24af1826c6f"

Write-Host "📥 Завантаження $Filename ..."
$temp = Join-Path $env:TEMP "signal-cli-tmp"
$archive = Join-Path $temp $Filename

try {
    New-Item -ItemType Directory -Force -Path $temp | Out-Null
} catch {
    Write-Error "❌ Не вдалося створити тимчасову директорію `${temp}`: $_"
    exit 1
}

try {
    # Прискорює завантаження великого файлу та уникає прогрес-бару в CI
    $ProgressPreference = "SilentlyContinue"
    Invoke-WebRequest -Uri $Url -OutFile $archive -ErrorAction Stop
} catch {
    Write-Error "❌ Помилка під час завантаження `${Url}`: $_"
    exit 2
}

Write-Host "🔐 Перевірка цілісності (SHA-256) ..."
try {
    $actualSha256 = (Get-FileHash -Path $archive -Algorithm SHA256).Hash
} catch {
    Write-Error "❌ Не вдалося обчислити SHA-256 для `${archive}`: $_"
    exit 5
}

if ($actualSha256 -ine $ExpectedSha256) {
    Remove-Item -Path $archive -Force -ErrorAction SilentlyContinue
    Write-Error "❌ Невідповідність SHA-256! Очікувано $ExpectedSha256, отримано $actualSha256. Завантаження перервано."
    exit 6
}
Write-Host "✅ SHA-256 збігається."

Write-Host "📦 Розпаковка до $OutDir ..."
try {
    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
} catch {
    Write-Error "❌ Не вдалося створити директорію `${OutDir}`: $_"
    exit 3
}

# tar (bsdtar у Windows) може писати у stderr нефатальні попередження
# (наприклад про symlink-и), тому не покладаємось на код виходу,
# а перевіряємо результат за наявністю виконуваного файлу нижче.
tar -xf $archive -C "$OutDir" --strip-components=1 2>$null

if (-not (Test-Path $SignalCliBin)) {
    Write-Error "❌ Розпакування не вдалося: не знайдено $SignalCliBin"
    exit 4
}

# Прибираємо тимчасовий архів
Remove-Item -Path $archive -Force -ErrorAction SilentlyContinue

Write-Host "✅ Готово! signal-cli v$Version встановлено у $OutDir"
exit 0
