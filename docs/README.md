# Документація SignalCli.NET

Повний reference типізованого .NET-API над `signal-cli`. Швидкий старт + установка живуть у [`/README.md`](../README.md); ця папка — для глибшого читання.

## API reference (per-категорія)

| Файл | Що покриває |
|---|---|
| [`api/messaging.md`](api/messaging.md) | `ISignalMessage` — 14 send-методів (текст, attachments, стікери, реакції, квитанції, typing, remote-delete, polls, payment-notifications, pin/unpin, admin-delete, message-request-response) |
| [`api/accounts.md`](api/accounts.md) | `ISignalAccounts` — 13 методів (list/sync + 8 destructive, gated through `EnableDestructiveOperations`) |
| [`api/devices.md`](api/devices.md) | `ISignalDevices` — 6 методів (Start/FinishLink — як secondary; AddDevice/ListDevices/RemoveDevice/UpdateDevice — як primary) |
| [`api/groups.md`](api/groups.md) | `ISignalGroups` — 4 методи (ListGroups, JoinGroup, UpdateGroup dual-mode, QuitGroup idempotent) |
| [`api/contacts.md`](api/contacts.md) | `ISignalContacts` — 8 методів (List/Trust + Update + Block/Unblock + Remove) |
| [`api/events.md`](api/events.md) | `ISignalEventService` — Subscribe/Unsubscribe + 17 event-kind'ів × 2 поверхні (`IObservable<T>` + `IAsyncEnumerable<T>`) |
| [`api/resources-stickers.md`](api/resources-stickers.md) | `ISignalResources` (3) + `ISignalStickers` (3) + `ISignalCliClient` (`VersionAsync` + raw `InvokeMethodAsync`) |
| [`api/di-options.md`](api/di-options.md) | `ServiceCollectionExtensions` (4 extension methods) + повний reference `SignalCliOptions` |

## Приклади

| Файл | Що покриває |
|---|---|
| [`examples/worker-auto-reply.md`](examples/worker-auto-reply.md) | Console-worker з auto-reply + device-link flow |

## Operational

| Файл | Що покриває |
|---|---|
| [`cloud-development.md`](cloud-development.md) | Claude Code on the Web: SessionStart hook, network policy, observability/OTel |

## Convention для нових docs

- Поглиблений middle-depth опис per-метод per CLAUDE.md §0.5: signature, опис, параметри, винятки, signal-cli source citation (`<X>Command.java @ <commit-sha>`), приклад.
- Усі публічні методи з `Tests/SignalCli.Tests/RegressionGuards/SignalCli.public-api.txt` мусять бути згадані хоча б в одному файлі під `docs/api/` — це enforce'ується regression-guard'ом `DocsApiCoverageTests` (RG09).
- Внутрішні sub-string citation'и (`§F<N>`) посилаються на upstream-нотатки у CLAUDE.md → `signal-cli-api-coverage` capability quirks.
