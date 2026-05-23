# Розробка у Claude Code on the Web

Цей репозиторій налаштовано для роботи у [Claude Code on the Web](https://code.claude.com/docs/en/claude-code-on-the-web) — браузерному варіанті агента, де кожна сесія виконується в ефемерному контейнері. Ця сторінка пояснює, що відбувається при старті сесії, які команди використовувати і чого свідомо НЕ робимо у warm-up.

## Як налаштовано

- **SessionStart hook:** `.claude/hooks/session-start.sh` (виконується синхронно, blocking-mode).
- **Реєстрація:** `.claude/settings.json` → `hooks.SessionStart`.
- **Hook запускається лише у remote-середовищі.** Локальна розробка (`CLAUDE_CODE_REMOTE` не виставлено) — hook є no-op, щоб не чіпати ваш `~/.dotnet`.

## Що робить hook

1. Ставить **`.NET 10 SDK`** через apt (`dotnet-sdk-10.0` зі штатних `noble-updates`). Базовий cloud-образ Ubuntu 24.04 не має dotnet — без цього кроку нічого не побудується.
2. `dotnet restore Tests/SignalCli.Tests/SignalCli.Tests.csproj` — тягне NuGet для тестового проєкту і референсованого `src/SignalCli`. Кеш переживає між сесіями.
3. `dotnet build src/SignalCli/SignalCli.csproj` — sanity-перевірка, що SDK + код у робочому стані. На цьому етапі warm-up завершується.

### Що hook свідомо НЕ робить

- **НЕ збирає `SignalCli.sln` цілком.** Runtime-проєкти (`SignalCli.runtime`, `SignalCli.runtime.native`, `SignalCli.runtime.jre.win-x64`, `SignalCli.runtime.jre.osx-arm64`) тягнуть signal-cli (≈30 МБ) і Eclipse Temurin JRE (≈80 МБ кожен з двох) із зовнішніх дзеркал. Це потрібно лише для E2E-тестів у `Tests/SignalCli.Tests.Integration`. Будуйте їх явно тоді, коли треба:
  ```bash
  dotnet build SignalCli.sln
  ```
- **НЕ запускає тести.** 152 unit-тести виконуються секунди — запускайте з сесії, щоб бачити вивід.
- **НЕ робить NuGet vulnerability audit.** У warm-up передається `/p:NuGetAudit=false` — приватний GitHub-feed (`nuget.pkg.github.com/07artem132/`) у `NuGet.Config` потребує токен. Аудит вразливостей робиться окремим кроком у CI.

## Поширені команди в сесії

```bash
# Швидко (тільки бібліотека)
dotnet build src/SignalCli/SignalCli.csproj

# Усі юніт-тести (~152, секунди)
dotnet test  Tests/SignalCli.Tests/SignalCli.Tests.csproj

# Конкретні тести
dotnet test  Tests/SignalCli.Tests/SignalCli.Tests.csproj --filter "FullyQualifiedName~ConfigTests"

# Повна збірка (з network — runtime-пакети тягнуть signal-cli + JRE)
dotnet build SignalCli.sln

# E2E (потребує повну збірку вище)
dotnet test  Tests/SignalCli.Tests.Integration/SignalCli.Tests.Integration.csproj --filter "Category=E2E"
```

## Мережева політика

Hook потребує:

- `archive.ubuntu.com` / `security.ubuntu.com` — для `apt-get install dotnet-sdk-10.0`.
- `api.nuget.org` — для `dotnet restore`.

Для **повної** збірки `SignalCli.sln` додатково:

- `github.com` / `objects.githubusercontent.com` — signal-cli releases (`AsamK/signal-cli`).
- `download.eclipse.org` (або `api.adoptium.net`) — Eclipse Temurin JRE.

Якщо у вашому remote-середовищі обмежений вихід — переконайтеся, що ці хости дозволені, або працюйте без runtime-пакетів (їх і так пропускає hook).

## Якщо потрібно змінити hook

- Зміни — у `.claude/hooks/session-start.sh`. Він **синхронний** (блокує старт сесії, доки не завершиться) — це гарантує, що Claude не починає роботу, поки `dotnet` не готовий.
- Async-режим (`{"async": true, "asyncTimeout": <ms>}` у першому рядку stdout) теж підтримується — старт сесії стає миттєвим, але з'являється гонка: агент може спробувати запустити тести до того, як restore завершився. Перемикайтеся на async лише якщо це вас не лякає.
- Логи hook видно у виводі сесії під заголовком `SessionStart`.

## Observability (post-modernize-tuning §11)

Бібліотека експонує **дві OTel-сумісні поверхні** — `ActivitySource` для distributed-tracing і `Meter` для метрик. Обидва називаються `"SignalCli.NET"`; без активного listener'а — нульова накладна.

```csharp
// Program.cs — підписка на джерела через OpenTelemetry
services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource("SignalCli.NET")
        .AddConsoleExporter())     // або AddOtlpExporter / AddJaegerExporter / etc.
    .WithMetrics(m => m
        .AddMeter("SignalCli.NET")
        .AddConsoleExporter());
```

**Що відстежується.** Спани: `rpc.<method>` для кожного JSON-RPC виклику (теги `signal.rpc.method`, `signal.rpc.request_id`), `signalcli.process.start`, `signalcli.healthcheck.ping` (тег `signal.healthcheck.outcome` ∈ {`ok`,`timeout`,`failed`,`no_stream_pair`}), `signalcli.subscribe` (тег `signal.subscription.id`). Метрики: `signalcli.rpc.requests` (counter; теги `method`, `status` ∈ {`ok`,`timeout`,`error`}), `signalcli.rpc.duration` (histogram, `ms`), `signalcli.process.restarts` (counter; тег `trigger` ∈ {`force`,`crash`,`health`}), `signalcli.events.dropped` (counter; тег `event_type`), `signalcli.subscriptions.active` (observable gauge).

**Privacy invariant.** Значення тегів — лише method-names, status-енами, integer-id, durations, exception-type-names. Тіло повідомлення, номер телефону, шлях до файлу вкладення в тегах **відсутні** — це CLAUDE.md rule #1, поширений на observability-поверхні (див. також `specs/observability/spec.md` у відповідній OpenSpec-зміні).

## Локальна розробка

Hook не активується локально (умова `CLAUDE_CODE_REMOTE=true`). Ставте dotnet звичним для вас способом ([офіційні інструкції](https://learn.microsoft.com/dotnet/core/install/)) і виконуйте ті самі команди вручну.

Якщо хочете локально перевірити саме hook — імітуйте remote-режим:

```bash
CLAUDE_CODE_REMOTE=true CLAUDE_PROJECT_DIR=$(pwd) bash .claude/hooks/session-start.sh
```
