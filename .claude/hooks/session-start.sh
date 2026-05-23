#!/bin/bash
# SessionStart hook для Claude Code on the web.
#
# Що робить:
#   1) Лише у віддаленому середовищі (CLAUDE_CODE_REMOTE=true) — інакше виходить тихо,
#      щоб не заважати локальній розробці.
#   2) Ставить .NET 10 SDK через apt (за наявності — пропускає).
#   3) Виконує `dotnet restore` для Tests/SignalCli.Tests, що тягне NuGet для тестів
#      і референсованого `src/SignalCli`. Це і прогріває кеш, і кешується між сесіями.
#   4) Будує `src/SignalCli` як швидку перевірку, що SDK/проєкт у робочому стані.
#
# Що НЕ робить (свідомо):
#   - НЕ збирає `SignalCli.sln` цілком — runtime-проєкти (signal-cli + Temurin JRE)
#     тягнуть сотні МБ із зовнішніх дзеркал і потрібні лише для повних E2E-тестів.
#     Якщо потрібні E2E, побудуйте їх явно: `dotnet build SignalCli.sln`.
#   - НЕ запускає тестовий ран — тести швидкі (~152 unit), але запускайте їх явно
#     із сесії, щоб бачити вивід.
#
# Ідемпотентно: повторні запуски — no-op, якщо SDK уже встановлено і кеш NuGet
# заповнений.

set -euo pipefail

# Локальна розробка має свої dotnet-installs — пропускаємо.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
    exit 0
fi

REPO_DIR="${CLAUDE_PROJECT_DIR:-$(pwd)}"
cd "$REPO_DIR"

echo "[session-start] Готую середовище для SignalCli.NET у $REPO_DIR"

# 1) .NET 10 SDK
if ! command -v dotnet >/dev/null 2>&1; then
    echo "[session-start] Встановлюю dotnet-sdk-10.0 (apt)..."
    # SUDO_PREFIX обираємо один раз: у remote-контейнері root → sudo не потрібен.
    SUDO_PREFIX=""
    if [ "$(id -u)" != "0" ] && command -v sudo >/dev/null 2>&1; then
        SUDO_PREFIX="sudo"
    fi
    # Ігноруємо помилки `apt-get update` від сторонніх PPA (deadsnakes/ondrej тощо) —
    # dotnet-sdk-10.0 у штатних noble-updates/security, і apt-cache уже їх знає.
    $SUDO_PREFIX apt-get update -qq || true
    $SUDO_PREFIX env DEBIAN_FRONTEND=noninteractive \
        apt-get install -y --no-install-recommends dotnet-sdk-10.0
else
    echo "[session-start] dotnet уже встановлено: $(dotnet --version)"
fi

# Telemetry off — детермінованіші логи в сесії.
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1' >> "${CLAUDE_ENV_FILE:-/dev/null}" 2>/dev/null || true
echo 'export DOTNET_NOLOGO=1' >> "${CLAUDE_ENV_FILE:-/dev/null}" 2>/dev/null || true

# 2) Прогріваємо NuGet для unit-тестів (тягне і src/SignalCli).
# /p:NuGetAudit=false вимикає vulnerability-сканування під час restore: у NuGet.Config
# зареєстровано приватний GitHub feed (`nuget.pkg.github.com`), якому потрібен токен,
# і його NU1900 + `TreatWarningsAsErrors=true` робить restore fatal. Vulnerability-аудит
# у нас і так виконується окремо у CI, не у session-warmup.
# --ignore-failed-sources залишає restore працездатним, навіть якщо приватний feed
# відмовив (offline / no-token).
echo "[session-start] dotnet restore Tests/SignalCli.Tests..."
dotnet restore Tests/SignalCli.Tests/SignalCli.Tests.csproj --nologo \
    --ignore-failed-sources /p:NuGetAudit=false

# 3) Швидка sanity-build (без referenced runtime-проєктів).
echo "[session-start] dotnet build src/SignalCli (sanity)..."
dotnet build src/SignalCli/SignalCli.csproj --no-restore --nologo -v minimal \
    /p:NuGetAudit=false

echo "[session-start] Готово. Швидкі команди:"
echo "  dotnet build src/SignalCli/SignalCli.csproj"
echo "  dotnet test  Tests/SignalCli.Tests/SignalCli.Tests.csproj"
echo "  dotnet build SignalCli.sln              # +runtime-проєкти (повільно, network)"
