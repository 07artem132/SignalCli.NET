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
| 3 | `contacts-identity` | listContacts, listIdentities, trust, updateContact, removeContact, updateProfile, block, unblock | **4.3.0** | low | mock + E2E listContacts/listIdentities *(env-gated)* |
| 4 | `sticker-packs` + `binary-resource-fetch` | uploadStickerPack, listStickerPacks, addStickerPack, getAttachment, getAvatar, getSticker | **4.4.0** | low | mock-only |
| 5 | `device-management` | addDevice, listDevices, removeDevice, updateDevice | **4.5.0** | medium | mock + E2E listDevices *(env-gated)* |
| 6 | `account-lifecycle` *(opt-in)* | updateAccount, unregister, deleteLocalAccountData, startChangeNumber, finishChangeNumber, updateConfiguration, setPin, removePin | **4.6.0** | **HIGH** | mock-only — гейт за `SignalCliOptions.EnableDestructiveOperations` |
| 7 | `polls` + `messaging-power-user` *(+ receive-side event decoders)* | sendPollCreate, sendPollVote, sendPollTerminate, sendAdminDelete, sendPinMessage, sendUnpinMessage, sendMessageRequestResponse, sendPaymentNotification | **4.7.0** | medium | mock-only |
| 8 | `utility-rpc` | getUserStatus, submitRateLimitChallenge, sendContacts | **4.8.0** | low | mock + E2E getUserStatus *(env-gated)* |

*E2E "env-gated"* означає: тест шукає environment variable `SIGNALCLI_TEST_ACCOUNT` (формат `+1234567890`); якщо змінна відсутня — тест skip'ається з reason `"No SIGNALCLI_TEST_ACCOUNT env var; integration test requires registered test account"`. На CI runners без registered тестового номера E2E ці тести пропускаються; локально (developer setup) — запускаються. Read-only методи (`listContacts`, `listIdentities`, `listDevices`, `getUserStatus`) — НЕ мутують state, тож безпечно ділити account між test runs.

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
- `event-decoding-expansion`: розширення `Envelope.cs` + `SignalEventService` 7 нових паралельних event-stream'ів (IObservable + IAsyncEnumerable, RG06-compliant) для receive-side подій що відповідають Wave-7 send-side методам. Wire-shape DTOs (`JsonPollCreate`/`JsonPollVote`/`JsonPollTerminate`/`JsonPayment`/`JsonPinMessage`/`JsonUnpinMessage`/`JsonAdminDelete`) ре-engineered з signal-cli source — upstream має stable Java records у `src/main/java/org/asamk/signal/json/Json*.java`, прочитано на pinned SHA `bda4e7fc` (див. `research/wave-7-polls-power-user.md`). Доставка: Wave 7 одним блоком (7 data-message decoders).
  - Sync-side `messageRequestResponse` decoder поки **не входить** у цю capability — upstream Java enum має 8 values (`Type` у `MessageEnvelope.Sync.MessageRequestResponse`), а send-side Wave-7 enum має лише 2 (`ACCEPT`/`DELETE`). Якщо потрібен receive-side stream, додамо окремим OpenSpec change щоб не змішувати 2-value send + 8-value receive у одному PR-cycle. Деталі — `research/SUMMARY.md` §F2.

### Modified Capabilities

- `typed-rpc-errors` *(archived in `signal-cli-protocol-alignment`)*: розширюється трьома новими derived exceptions (`GroupAdminRequiredException`, `IdentityChangedException`, `CaptchaRequiredException`); existing `JsonRpcException`/`RateLimitException`/`UntrustedIdentityException` залишаються незмінними.
- `agent-friendly-api` *(archived in `agent-friendly-modernization`)*: existing `ISignalAccounts`, `ISignalDevices`, `ISignalGroups`, `ISignalMessage` отримують нові методи (additive — жодних breaking changes).
- `options-pattern` *(archived in `agent-friendly-modernization`)*: `SignalCliOptions` отримує новий `EnableDestructiveOperations: bool = false`; validator-source-gen перегенерується автоматично.

## Out of scope

- **`stickerPackOperation` sync-event decoder** — upstream signal-cli auto-installs sticker packs locally without surfacing the operation у JSON-RPC layer (`IncomingMessageHandler.java:659-672` @ `bda4e7fc` swallows the data-message after side-effecting storage). `src/main/java/org/asamk/signal/json/JsonSyncMessage.java` has 6 fields (`sentMessage`/`sentStoryMessage`/`blockedNumbers`/`blockedGroupIds`/`readMessages`/`type`) — `stickerPackOperations` is **not among them**. Surfacing this event would require either an upstream PR adding the field to `JsonSyncMessage` + push for release, or directly wrapping signal-cli's storage layer — both significantly larger scope than this change targets. Verified by 2026-05-25 `research/wave-4-sticker-packs-binary-resource-fetch.md` §7. May revisit as a separate OpenSpec change `sticker-pack-events` if a consumer asks.
- **`receive` polling RPC** — застаріле; `subscribeReceive` (event-driven) — кращий dual-API. Якщо consumer ASK'не, додамо окремим OpenSpec change як `messaging-polling-receive`.
- **CLI-only commands** (`register`, `verify`, `link`, `daemon`, `jsonRpc`) — поза JSON-RPC surface за дизайном upstream'у. Consumer запускає signal-cli з CLI вручну для bootstrap'у акаунта, далі лінкується через `startLink`/`finishLink`.
- **Реальні E2E integration tests для destructive методів** (unregister, deleteLocalAccountData, setPin, changeNumber) — потребують registered тестового номера + CAPTCHA solving; неможливо стабілізувати в CI. Тільки serialization + mock-RPC tests.
- **Інтерактивні E2E (send-and-verify-receipt)** — потребують ДВА registered тестових номера що відправляють/отримують між собою. CI infrastructure не підтримує. Single-account env-gated E2E (read-only `list*`/`getUserStatus`) — у скоупі (див. §1.8 design.md).
- **HealthChecks-адаптер** для нових методів — `SignalCli.NET.HealthChecks` ping'ає `version`; решта методів не входять у health-check semantics.
- **OpenTelemetry trace tags** з PII полів (recipient phone, message body, attachment paths) — critical rule #1 забороняє. Tag'и обмежені `method`, `status`, `trigger`, `event_type`; нові `event_type`-значення для polls/admin-delete/тощо додаються через `event_type` enum extension.
