# signal-cli API coverage to 90%+

## Why

`SignalCli.NET` сьогодні покриває лише **9 із ~54 JSON-RPC методів** signal-cli (≈18%): `version`, `listAccounts`, `sendSyncRequest`, `listGroups`, `startLink`, `finishLink`, `send`, `subscribeReceive`, `unsubscribeReceive`. Решта — `sendReaction`, `sendReceipt`, `sendTyping`, `remoteDelete`, `joinGroup`, `updateGroup`, `quitGroup`, `listContacts`, `listIdentities`, `trust`, `updateProfile`, `updateContact`, `removeContact`, `block`, `unblock`, `sendContacts`, всі ~10 sticker/binary-fetch, всі ~8 account-lifecycle, всі ~4 device-management, polls (3), power-user messaging (5), `getUserStatus`, `submitRateLimitChallenge` — **відсутні**.

Це означає, що consumer бот для типового use-case'у (відповідь "👍 прочитано" + typing-індикатор + видалити власне повідомлення на reply від модератора) НЕ МОЖЕ бути написаний на SignalCli.NET без shell'у в `signal-cli` CLI напряму. Бібліотека-wrapper з 18% coverage'ем — це не wrapper, це starter-template.

Audit `audit-followup-2026` (4.0.0) закрив усі invariant-gap'и для існуючих методів. Тепер настав час закрити surface-gap.

Target: **≥90% покриття (≥49 із ~54 JSON-RPC методів signal-cli)** через серію minor-релізів `4.1.0 → 4.8.0`, кожен — окремий PR, окремий CHANGELOG entry, окремий commit-per-capability за конвенцією `.claude/rules/audit-debt.md`.

CLI-only методи signal-cli (`register`, `verify`, `link`, `daemon`, `jsonRpc`) — поза скоупом за дизайном upstream'у (вони НЕ доступні через JSON-RPC). `link` уже має JSON-RPC-еквівалент `startLink`/`finishLink` — реалізовано.

## What Changes

**10 capabilities**, кожна — окремий релізний wave. Кожна додає 3-8 RPC-методів + парні DTO + service-метод + serialization-test + (для read-only) E2E integration test.

| Wave | Capability | Methods | Release | Risk | E2E? |
|---|---|---|---|---|---|
| 1 | `messaging-interactive` | sendReaction, sendReceipt, sendTyping, remoteDelete | **4.1.0** | low | mock-only |
| 2 | `groups-crud` | joinGroup, updateGroup, quitGroup | **4.2.0** | medium | mock-only |
| 3 | `contacts-identity` | listContacts, listIdentities, trust, updateContact, removeContact, updateProfile, block, unblock | **4.3.0** | low | mock + E2E listContacts/listIdentities |
| 4 | `sticker-packs` + `binary-resource-fetch` | uploadStickerPack, listStickerPacks, addStickerPack, getAttachment, getAvatar, getSticker | **4.4.0** | low | mock-only |
| 5 | `device-management` | addDevice, listDevices, removeDevice, updateDevice | **4.5.0** | medium | mock + E2E listDevices |
| 6 | `account-lifecycle` *(opt-in)* | updateAccount, unregister, deleteLocalAccountData, startChangeNumber, finishChangeNumber, updateConfiguration, setPin, removePin | **4.6.0** | **HIGH** | mock-only — гейт за `SignalCliOptions.EnableDestructiveOperations` |
| 7 | `polls` + `messaging-power-user` | sendPollCreate, sendPollVote, sendPollTerminate, sendAdminDelete, sendPinMessage, sendUnpinMessage, sendMessageRequestResponse, sendPaymentNotification | **4.7.0** | medium | mock-only |
| 8 | `utility-rpc` | getUserStatus, submitRateLimitChallenge, sendContacts | **4.8.0** | low | mock + E2E getUserStatus |

**Підсумок:** 9 існуючих + 44 нових = **53 / 54 = 98% coverage** (єдиний пропущений — `receive` polling, бо `subscribeReceive` є його кращою альтернативою; залишити з poll-API в окремому OpenSpec якщо хтось попросить).

### Cross-cutting infrastructure changes

- **`SignalCliOptions.EnableDestructiveOperations` (новий bool)** — defaults `false`. `account-lifecycle` service-методи перевіряють цей флаг у constructor'і; якщо `false`, throw `InvalidOperationException("destructive operations disabled; set SignalCliOptions.EnableDestructiveOperations = true")` при першому виклику. XMLDoc на кожному destructive-методі попереджає.
- **Нові typed exceptions** для семантичних signal-cli error-codes що зараз йдуть як generic `JsonRpcException`:
  - `GroupAdminRequiredException` (поверх `UserError = -1` з message-match на "admin")
  - `IdentityChangedException` (поверх `UntrustedIdentity = -4` для send-методів — детектити коли треба `trust` перед retry)
  - `CaptchaRequiredException` (поверх `CaptchaRejected = -6`)
- **`SignalJsonContext`** — додаються ~88 нових `[JsonSerializable]` entries (44 methods × 2 DTOs); `R01` regression guard (`JsonContextRegistrationTests`) автоматично каже коли забув.
- **Public API surface baseline** — `SignalCli.public-api.txt` (`R03`) оновлюється у кожному wave-PR.

## Capabilities

### New Capabilities

- `messaging-interactive`: бібліотека SHALL надавати typed-методи для `sendReaction`, `sendReceipt`, `sendTyping`, `remoteDelete` через `ISignalMessage`.
- `groups-crud`: бібліотека SHALL надавати typed-методи для `joinGroup`, `updateGroup`, `quitGroup` через новий `ISignalGroups` API (extends existing `ListGroupsAsync`).
- `contacts-identity`: новий interface `ISignalContacts` SHALL надавати typed-методи для `listContacts`, `listIdentities`, `trust`, `updateContact`, `removeContact`, `updateProfile`, `block`, `unblock`.
- `sticker-packs`: новий interface `ISignalStickers` SHALL надавати `listStickerPacks`, `uploadStickerPack`, `addStickerPack`.
- `binary-resource-fetch`: новий interface `ISignalResources` SHALL надавати `getAttachment`, `getAvatar`, `getSticker` з Base64→`byte[]` decoding helper'ом.
- `device-management`: розширення `ISignalDevices` SHALL додати `addDevice`, `listDevices`, `removeDevice`, `updateDevice` (existing: `StartLinkAsync`/`FinishLinkAsync` залишаються).
- `account-lifecycle`: розширення `ISignalAccounts` SHALL додати 8 destructive методів за opt-in `SignalCliOptions.EnableDestructiveOperations` гейтом.
- `polls`: extension до `ISignalMessage` SHALL додати `sendPollCreate`, `sendPollVote`, `sendPollTerminate` + decoding poll-events у `SignalEventService`.
- `messaging-power-user`: extension до `ISignalMessage` SHALL додати `sendAdminDelete`, `sendPinMessage`, `sendUnpinMessage`, `sendMessageRequestResponse`, `sendPaymentNotification`.
- `utility-rpc`: нові методи на existing services — `getUserStatus` (`ISignalAccounts`), `submitRateLimitChallenge` (`ISignalAccounts`), `sendContacts` (`ISignalAccounts`).

### Modified Capabilities

- `typed-rpc-errors` *(archived in `signal-cli-protocol-alignment`)*: розширюється трьома новими derived exceptions (`GroupAdminRequiredException`, `IdentityChangedException`, `CaptchaRequiredException`); existing `JsonRpcException`/`RateLimitException`/`UntrustedIdentityException` залишаються незмінними.
- `agent-friendly-api` *(archived in `agent-friendly-modernization`)*: existing `ISignalAccounts`, `ISignalDevices`, `ISignalGroups`, `ISignalMessage` отримують нові методи (additive — жодних breaking changes).
- `options-pattern` *(archived in `agent-friendly-modernization`)*: `SignalCliOptions` отримує новий `EnableDestructiveOperations: bool = false`; validator-source-gen перегенерується автоматично.

## Out of scope

- **`receive` polling RPC** — застаріле; `subscribeReceive` (event-driven) — кращий dual-API. Якщо consumer ASK'не, додамо окремим OpenSpec change як `messaging-polling-receive`.
- **CLI-only commands** (`register`, `verify`, `link`, `daemon`, `jsonRpc`) — поза JSON-RPC surface за дизайном upstream'у. Consumer запускає signal-cli з CLI вручну для bootstrap'у акаунта, далі лінкується через `startLink`/`finishLink`.
- **Реальні E2E integration tests для destructive методів** (unregister, deleteLocalAccountData, setPin, changeNumber) — потребують registered тестового номера + CAPTCHA solving; неможливо стабілізувати в CI. Тільки serialization + mock-RPC tests.
- **`SignalEventService` event decoding для нових event-типів** (poll-vote events, payment-notification-receive, sticker-pack-install-receive) — окремий follow-up `event-decoding-expansion` коли упевнимось у wire-shape'ах через E2E (signal-cli docs для `receive`-envelope не повні; треба capture'ити реальні payload'и спочатку).
- **Async-stream pairs для нових event-типів** — те ж саме, follow-up.
- **HealthChecks-адаптер** для нових методів — `SignalCli.NET.HealthChecks` ping'ає `version`; решта методів не входять у health-check semantics.
- **OpenTelemetry trace tags** з PII полів (recipient phone, message body, attachment paths) — critical rule #1 забороняє. Tag'и обмежені `method`, `status`, `trigger`, `event_type`; нові `event_type`-значення для polls/admin-delete/тощо додаються через `event_type` enum extension.
