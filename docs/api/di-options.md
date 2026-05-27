# DI + конфігурація — `ServiceCollectionExtensions` та `SignalCliOptions`

Реєстрація бібліотеки у DI-контейнер `Microsoft.Extensions.DependencyInjection` і повний reference властивостей `SignalCliOptions`.

---

## `ServiceCollectionExtensions`

Усі 4 extension-методи **ідемпотентні** — повторні виклики тихо no-op'ують замість дублювати hosted-сервіси. Guard через private sentinel-marker `SignalCliRegistrationMarker` (CLAUDE.md § DI registration).

### `AddSignalCli(Action<SignalCliOptions>?)` — основний overload

```csharp
public IServiceCollection AddSignalCli(Action<SignalCliOptions>? configureOptions);
```

Реєструє усі hosted services (`SignalCliHostedService`, `JsonRpcClientHostedService`, `SignalCliHealthMonitor`), facade-сервіси (`ISignalMessage`, `ISignalAccounts`, ...), і `IOptions<SignalCliOptions>` з валідацією на старті host'а.

Валідація layered:
1. `[Required]`/`[Range]` data-annotations через source-gen `SignalCliOptionsValidator` ([OptionsValidator], reflection-free, AOT-safe);
2. Cross-field XOR: `JavaExecutable` АБО `SignalCliExecutable` має бути задано;
3. `ValidateOnStart()` — помилки видно на `host.StartAsync()`, не на першому RPC.

```csharp
services.AddSignalCli(o =>
{
    o.AppHome = "/opt/signal";
    o.LibDirectory = "lib";
    o.JavaExecutable = "/usr/bin/java";
    o.MaxRestartAttempts = 5;
    o.HealthCheckIntervalSeconds = 30;
});
```

### `AddSignalCli(IConfiguration)` — bind з `appsettings.json`

```csharp
public IServiceCollection AddSignalCli(IConfiguration configurationSection);
```

Bind'ить `SignalCliOptions` до секції конфігурації. **AOT-safe з 4.0.1** — `EnableConfigurationBindingGenerator=true` у csproj робить configuration-source-gen reflection-free.

```json
// appsettings.json
{
  "SignalCli": {
    "AppHome": "/opt/signal",
    "LibDirectory": "lib",
    "JavaExecutable": "/usr/bin/java",
    "MaxRestartAttempts": 3,
    "HealthCheckIntervalSeconds": 40
  }
}
```

```csharp
services.AddSignalCli(builder.Configuration.GetSection("SignalCli"));
```

**Винятки:** `ArgumentNullException` якщо `configurationSection` — `null`.

### `AddSignalCliWithBundledRuntimeDefaults(Action<SignalCliOptions>?)`

```csharp
public IServiceCollection AddSignalCliWithBundledRuntimeDefaults(
    Action<SignalCliOptions>? configure = null);
```

Зручний preset для consumer'ів пакетів `SignalCli.Runtime.Jre.{win-x64,osx-arm64}` або `SignalCli.Runtime.Native`. Auto-resolve'ить:

- `AppHome` = `AppContext.BaseDirectory`;
- `LibDirectory` = `"SignalCli/lib"`;
- `JavaExecutable` = `JavaPathResolver.TryResolveJavaPath(AppHome)` (bundled JRE → `JAVA_HOME` → Windows Oracle → `PATH`).

`configure` опціональний — override будь-якого field'у. На Linux native-режимі: задай `SignalCliExecutable` і занули `JavaExecutable`.

```csharp
services.AddSignalCliWithBundledRuntimeDefaults(o =>
{
    o.StoragePathCli = Path.Combine(AppContext.BaseDirectory, "signal-storage");
    o.MaxRestartAttempts = 3;
});
```

### `AddSignalEvents()`

```csharp
public IServiceCollection AddSignalEvents();
```

Окремий extension — реєструє `ISignalEventService` як singleton + hosted service. **Не входить в `AddSignalCli`** (consumer може хотіти RPC-only без подій). Ідемпотентний.

```csharp
services.AddSignalCli(opts => { /* ... */ });
services.AddSignalEvents();   // потрібно якщо плануєш TextMessages/Reaction/...
```

---

## `SignalCliOptions` — властивості

Звичайний клас (`get; set;`-сетери — Microsoft.Extensions.Options-pattern не любить `init`), але **соціально immutable** після `host.StartAsync()` — внутрішні сервіси читають `_options.Value` один раз у конструкторі.

### Required

| Властивість | Тип | Дефолт | Опис |
|---|---|---|---|
| `AppHome` | `string` | `""` ⚠ | Головна директорія програми (config/log/lib). `[Required(AllowEmptyStrings=false)]` |
| `LibDirectory` | `string` | `""` ⚠ | Піддиректорія з JAR-файлами signal-cli (відносно `AppHome`). `[Required]` |

**XOR cross-field rule** — задай **рівно один**:

| Властивість | Тип | Опис |
|---|---|---|
| `JavaExecutable` | `string?` | Шлях до Java executable (для JVM-режиму) |
| `SignalCliExecutable` | `string?` | Шлях до native signal-cli бінарника (Java не потрібна) |

### CLI lifecycle

| Властивість | Тип | Дефолт | Range | Опис |
|---|---|---|---|---|
| `CliLogLevelCli` | `CliLogLevel` | `Info` | `Verbose`/`Debug`/`Info` | Рівень логування signal-cli |
| `LogFileCli` | `string?` | `null` | — | Шлях до файлу логу; default — від `AppHome` |
| `StoragePathCli` | `string?` | `null` | — | Директорія data store'а; default — від `AppHome` |
| `UseManualReceiveMode` | `bool` | `true` | — | Manual vs on-start receive-mode |
| `EnvironmentVariables` | `IReadOnlyDictionary<string,string>` | empty | — | Env vars передані процесу signal-cli |

### Process supervision

| Властивість | Тип | Дефолт | Range | Опис |
|---|---|---|---|---|
| `MaxRestartAttempts` | `int` | `3` | `0-100` | Max спроб restart (0 = вимкнено) |
| `RestartDelaySeconds` | `int` | `5` | `0-3600` | Затримка між restart-спробами |
| `RestartWindowSeconds` | `int` | `60` | `1-86400` | Вікно стабільності після якого counter restart'ів скидається |
| `StopTimeoutSeconds` | `int` | `2` | `0-3600` | Grace timeout для signal-cli після "exit"-команди (зараз — після stdin EOF) |
| `HealthCheckIntervalSeconds` | `int` | `40` | `1-3600` | Інтервал між version-ping'ами |
| `HealthCheckTimeoutSeconds` | `int` | `10` | `1-3600` | Max wait для health-check response'у |

### RPC + back-pressure

| Властивість | Тип | Дефолт | Range | Опис |
|---|---|---|---|---|
| `RequestTimeoutSeconds` | `int` | `30` | `1-3600` | Per-RPC-request timeout |
| `NotificationChannelCapacity` | `int` | `1024` | `1-1000000` | Ємність bounded-каналу stdout-reader ↔ fan-out subscriber. `FullMode = Wait` (back-pressure до stdout-reader'а) |

### Опт-ін гейтінг

| Властивість | Тип | Дефолт | Опис |
|---|---|---|---|
| `EnableDestructiveOperations` | `bool` | `false` | ⚠ Розблоковує 8 destructive методів у `ISignalAccounts` (`unregister`, `deleteLocalAccountData`, `updateAccount`, ...). Default `false` → кидають `InvalidOperationException` ПЕРЕД RPC dispatch. Деталі — [`accounts.md`](accounts.md). |

---

## Реєстраційний граф (що отримаєш у DI після `AddSignalCli` + `AddSignalEvents`)

```
ISignalCliClient            → SignalService
ISignalMessage              → SignalMessage
ISignalAccounts             → SignalAccounts
ISignalDevices              → SignalDevices
ISignalGroups               → SignalGroups
ISignalContacts             → SignalContacts
ISignalStickers             → SignalStickers
ISignalResources            → SignalResources
ISignalEventService         → SignalEventService (+ AddSignalEvents)

IOptions<SignalCliOptions>  → validated на host.StartAsync()
TimeProvider                → TimeProvider.System (override'нути для тестів)

# Internals (не для прямого resolution, але в IHostedService-таблиці):
SignalCliHostedService          # process lifecycle + restart budget
JsonRpcClientHostedService      # JSON-RPC transport
SignalCliHealthMonitor          # periodic version-ping
```

Окремий пакет `SignalCli.NET.HealthChecks` додає `IHealthCheck`-адаптер — деталі у README §Health-checks.

---

## Override TimeProvider для тестів

Стандартна реєстрація — `TimeProvider.System`. У тестах підмінюй через `services.Replace(...)`:

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

services.AddSignalCli(opts => { /* ... */ });
services.Replace(ServiceDescriptor.Singleton<TimeProvider>(new FakeTimeProvider()));
```

CLAUDE.md rule #11 — `SignalCliHealthMonitor`/`SignalCliHostedService.Restart*` тести **мусять** використовувати `FakeTimeProvider.Advance(...)` замість `Task.Delay`.
