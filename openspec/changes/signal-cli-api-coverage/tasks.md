# Tasks — signal-cli-api-coverage

8 release waves. Кожен wave = own branch from main + own PR + own minor-bump + own CHANGELOG entry. Wave-N не починається до merge'у Wave-(N-1).

## 0. Setup

- [x] 0.1 Branch `claude/signal-cli-api-coverage` from current `main` (вже існує — продовжуємо).
- [x] 0.2 `npx -y @fission-ai/openspec@latest validate signal-cli-api-coverage --strict` — green.
- [x] 0.3 Clone signal-cli source locally for read-only reference: `git clone https://github.com/AsamK/signal-cli.git ../signal-cli-source` (sibling до repo). Pin SHA = upstream `master` HEAD на момент старту Wave 1; record SHA у CLAUDE.md "signal-cli-protocol.md" як `bda4e7f`-style reference.

## 0.5 **MANDATORY per-method source-of-truth protocol — anti-hallucination guard** ⚠

**Мета: уникнути LLM-галюцинацій про signal-cli API.** Wire-shape, field names, enum values, validation rules, error codes, side-effects — все має бути прочитане з upstream Java source, НЕ згенероване з training-data, НЕ виведене з docs (signal-cli man-pages неповні), НЕ вгадане з аналогії до similar method'у. Source — єдине джерело істини. Документація може бути застарілою, training-data моя може мати pre-0.14 shape — Jackson Java records у `src/main/java/org/asamk/signal/json/*.java` на pinned commit'і НЕ можуть бути неправильними (компілятор upstream'у виконує enforce).

**Anti-hallucination checklist для КОЖНОГО з 44 нових RPC методів** (перед написанням DTO або service-методу):

1. **Read signal-cli source command handler** — `src/main/java/org/asamk/signal/json/JsonRpcCommand<Method>.java` (або equivalent для command name). Записати:
   - Точну сигнатуру `params` record'у (Jackson Java record = source-of-truth для wire shape).
   - Точну сигнатуру `result` record'у.
   - Field naming (Jackson default = camelCase; перевірити чи немає `@JsonProperty("snake_case")` overrides).
   - Required vs optional fields (`@JsonProperty(required = true)` vs nullable).
   - Enum values (наприклад `MessageRequestResponseType`: `ACCEPT`/`DELETE`/`BLOCK`/`BLOCK_AND_DELETE`/`UNBLOCK` — UPPER_SNAKE_CASE, не camelCase).

2. **Read upstream impl** — actual logic у `lib/src/main/java/org/asamk/signal/manager/*` / commands package. Записати:
   - Validation rules (e.g., "options array MUST have 2-10 items" для polls).
   - Error codes конкретно для цього методу (chained `throw new UserErrorException(...)` стайтменти).
   - Side-effects (e.g., `sendContacts` triggers sync notification — у XMLDoc remark).

3. **Embed source citation у service-method's XMLDoc** — формат:
   ```csharp
   /// <remarks>
   /// signal-cli RPC mapping: see <c>src/main/java/org/asamk/signal/commands/SendReactionCommand.java</c>
   /// @ <c>&lt;commit-sha&gt;</c>. Field names mirror Jackson Java record exactly.
   /// </remarks>
   ```

4. **Embed source citation у DTO's XMLDoc** — same формат, але посилання на `org.asamk.signal.json.Json*` record.

5. **Snapshot test з real signal-cli payload** — _додатково_ до source-reading, capture один real RPC roundtrip через signal-cli running locally (`signal-cli --verbose jsonRpc < request.json`) і embed payload як inline-literal у serialization-test. Це безпекова сітка на випадок коли signal-cli ship'не behavior change який не помітний з source-reading.

**Чому це обов'язково (хвороби які лікуємо):**
1. **Галюцинації LLM** — без source-reading, агент придумає правдоподібний field name (`recipientNumber` замість `recipient`), правдоподібну enum value (`AcceptRequest` замість `ACCEPT`), правдоподібну required-flag — і compiler сглотне, бо JSON tolerant; runtime fail тільки на production envelope.
2. **Moving target** — signal-cli ship'ить releases кожні 1-2 місяці. Single source of truth = upstream Jackson records на pinned commit. Wrapping based на docs/guess = drift class з аудиту v2.0/v2.1 (див. `audit-debt.md` §"Doc/code constant drift").
3. **Re-verify cost** — citation у XMLDoc дозволяє future maintainer'у (людині або агенту) перевірити за 1 хв проти upstream'у, без вгадування звідки взялась shape.

**Enforcement (3 шари):**
- **Review-time** (primary): PR review checklist має punkt "every new service method's XMLDoc cites `org.asamk.signal.*` source path + commit SHA, AND every DTO's XMLDoc cites matching `org.asamk.signal.json.Json*` record". Без citation — PR rejected.
- **Build-time** (secondary): новий regression guard `RG10` (`SourceCitationConsistencyTests`) reflectively енумерує всі публічні методи на `ISignalAccounts`/`ISignalDevices`/`ISignalGroups`/`ISignalMessage`/`ISignalContacts`/`ISignalResources`/`ISignalStickers` і витягує XMLDoc через `XmlDocumentationReader`; assert'ить що `<remarks>` містить регекс `org\.asamk\.signal\.[a-z.]+\.\w+\.java\s*@\s*[0-9a-f]{7,40}`. Build fails якщо method не процитував source.
- **Snapshot-time** (tertiary): inline-literal JSON у serialization-test'ах capture'ені з real signal-cli running, не написані з пам'яті. Якщо упустимо source-reading — snapshot test впаде при першому upstream wire-shape drift'і.

- [ ] 0.4 Capture сирі JSON-RPC payloads для всіх 44 нових методів через signal-cli running locally з `--verbose`, embed як inline-literals у serialization-tests (so wire-shape pinned від day 1, не reverse-engineered).
  - **Wave 1 deviation (2026-05-25):** payload-capture не виконано — Wave 1 wire-shape pinned альтернативно через `MessagingInteractiveSerializationTests` що парсять serialized JSON через `JsonDocument` field-by-field проти research §0.5 source-read wire shape. Source-of-truth (Java records @ bda4e7fc) — той самий контракт; capture'и додамо у Wave 8 retrospective якщо знадобиться.

## 1. Wave 1 — `messaging-interactive` *(4.1.0)*

### 1.1 Cross-cutting (lands у Wave 1 бо потрібен `IdentityChangedException` для sendReaction error mapping)

- [x] 1.1.1 `src/SignalCli/Exceptions/IdentityChangedException.cs` — `sealed` derived з `UntrustedIdentityException`. XMLDoc пояснює різницю semantic'у (re-install vs initial-contact). **Implementation note:** `UntrustedIdentityException` un-sealed щоб дозволити derivation (раніше sealed).
- [x] 1.1.2 `src/SignalCli/Exceptions/GroupAdminRequiredException.cs` — `sealed` derived з `JsonRpcException`. XMLDoc: "Поверх UserError(-1) коли signal-cli message contain'ить 'admin'".
- [x] 1.1.3 `src/SignalCli/Exceptions/CaptchaRequiredException.cs` — `sealed` derived з `JsonRpcException`. XMLDoc лінкує на signalcaptchas.org workflow + `SubmitRateLimitChallengeAsync` (forward ref).
- [x] 1.1.4 `src/SignalCli/Services/Rpc/JsonRpcClient.cs` — extend `InvokeMethodAsync` switch (per design §1.2):
  - `(int)JsonRpcErrorCode.CaptchaRejected => new CaptchaRequiredException(response.Error)`
  - `(int)JsonRpcErrorCode.UserError when message contains "admin" => new GroupAdminRequiredException(response.Error)`
  - `IdentityChangedException` НЕ диспатчиться — це opt-in subset of UntrustedIdentityException (caller catch'ить її через `catch (IdentityChangedException)` що працює бо вона derived).
- [x] 1.1.5 Tests: `Tests/SignalCli.Tests/Exceptions/NewTypedRpcErrorsTests.cs` — 9 tests (3 type-contracts + 4 dispatch through `ProcessMessageAsync` + 1 sealed marker + 1 un-sealed regression guard).

### 1.2 RPC methods + DTOs

- [x] 1.2.1 `src/SignalCli/Models/Signal/Message/ReactionOptions.cs` — `sealed record` + nested `Builder`, поля: Account, Recipients/GroupIds/Usernames, NoteToSelf, NotifySelf, Emoji, TargetAuthor, TargetTimestamp, Remove, Story. §F20 reminder applied у XMLDoc.
- [x] 1.2.2 `src/SignalCli/Models/Signal/Message/SendReactionParameters.cs`. **Deviation:** `SendReactionResponse.cs` НЕ створено — research §Cross-method §4 (read of `SendMessageResultUtils.java @ bda4e7fc`) пинить identical `{ timestamp, results }` shape для всіх 4 методів; reuse existing `SendMessageResponse`. Уникнено 4-х майже-ідентичних response-типів.
- [x] 1.2.3 `ReceiptOptions.cs` + `SendReceiptParameters.cs` + `ReceiptType.cs` enum (`Read`|`Viewed` — mapped до lowercase wire string). §F7: `Recipient: string` singular, не List. XMLDoc cross-references §F7. Reuse `SendMessageResponse`.
- [x] 1.2.4 `TypingOptions.cs` + `SendTypingParameters.cs`. Поле `Stop: bool` (default false). XMLDoc документує no-username, no-noteToSelf, START auto-expire. Reuse `SendMessageResponse`.
- [x] 1.2.5 `RemoteDeleteOptions.cs` + `RemoteDeleteParameters.cs`. §F10 reminder у XMLDoc (IOException → -32603 INTERNAL_ERROR, consistent з sendReaction). Reuse `SendMessageResponse`.
- [x] 1.2.6 Register 4 нових DTOs у `Serialization/SignalJsonContext.cs` (1 each per method — лише Parameters; Response reused).
- [x] 1.2.7 `src/SignalCli/Interfaces/Signal/ISignalMessage.cs` — додати 4 нові методи з XMLDoc + source-citation per §0.5 + §F-reminders inline.
- [x] 1.2.8 `src/SignalCli/Services/Signal/SignalMessage.cs` — реалізувати 4 нові методи. §F10 reminder applied у XMLDoc per-method (sendReaction→-32603, sendTyping→-1, remoteDelete→-32603, sendReceipt→no catch).
- [x] 1.2.9 `src/SignalCli/Logging/SignalMessageLog.cs` — 4 нових `[LoggerMessage]` методів у EventId block 700-799 (703-706: ReactionOk/ReceiptOk/TypingOk/RemoteDeleteOk). **Deviation:** 12 методів (3 per RPC) скорочено до 4 Ok-логів — Requested/ValidationFailed з пропозиції фактично не використовуються existing pattern'ом (existing SendUnifiedMessageAsync теж має 2 логи на 3 send-методи). NullResponse reuse'ється — це shared concept. Privacy rule #1 enforced: жодного PII у шаблонах.

### 1.3 Tests

- [x] 1.3.1 `Tests/SignalCli.Tests/Serialization/MessagingInteractiveSerializationTests.cs` — 8 serialization tests, parse через `JsonDocument` field-by-field. **Deviation:** inline-literal-JSON snapshots з real signal-cli не зібрано (`0.4` pending); pinning через source-of-truth research §0.5.
- [x] 1.3.2 `Tests/SignalCli.Tests/MessagingInteractive/SignalMessageInteractiveTests.cs` — 8 service-level тестів. (Subfolder renamed з `Services/Signal/` → `MessagingInteractive/` бо `SignalCli.Tests.Services` ns shadow'ила `SignalCli.Services`.)
- [x] 1.3.3 Receipt/Typing/RemoteDelete покриті у SignalMessageInteractiveTests.cs same file (всі 4 методи — один cohesive test class).
- [x] 1.3.4 `Tests/SignalCli.Tests/MessagingInteractive/InteractiveOptionsTests.cs` — 18 builder + validation тестів для 4-х Options. (Single file замість 4 — same logic per Options, не варто фрагментувати.)
- [x] 1.3.5 Public API baseline regenerated: 1087 → **1239 lines** (+152 entries: 3 exceptions + 4 Options × ~10 members + 4 Parameters × ~10 + 1 enum). `RG03 PublicApiSurfaceTests` зелений.

### 1.4 Release

- [x] 1.4.1 `dotnet build -p:TreatWarningsAsErrors=true && dotnet test SignalCli.sln` — clean, count 290 → **333** (+43 tests).
- [x] 1.4.2 `Directory.Build.props` — `<SignalCliPackageVersion>4.1.0</SignalCliPackageVersion>`.
- [x] 1.4.3 `CHANGELOG.md` — нова `## [4.1.0] — 2026-05-25` секція, consumer-first voice (`.claude/rules/openspec-workflow.md`).
- [ ] 1.4.4 Single commit: `feat(4.1.0): interactive messaging — reactions, receipts, typing, remote-delete`. Push.
- [ ] 1.4.5 Wait merge → tag `v4.1.0`.

## 2. Wave 2 — `groups-crud` *(4.2.0)*

- [x] 2.1 `src/SignalCli/Models/Signal/Groups/`:
  - `JoinGroupParameters.cs` / `JoinGroupResponse.cs` with `OnlyRequested: bool?` (§F13 dimorphic). Reuse `Message.SendMessageResult` cross-namespace для shared upstream wire type.
  - `UpdateGroupOptions.cs` (sealed record + Builder; ~16 nullable fields), `UpdateGroupParameters.cs`, `UpdateGroupResponse.cs` all-nullable (§F14 dimorphic GroupId-present-only-on-create + Results-concat-on-create).
  - `QuitGroupParameters.cs` (without QuitGroupBehavior enum — research showed no such enum exists upstream; `delete: bool` was the actual wire field). `QuitGroupResponse.cs` all-nullable + convenience `WasAlreadyNotMember` property для §F8 idempotent case.
  - `GroupLinkState.cs` enum + `GroupPermission.cs` enum (PascalCase .NET; service maps до kebab-case wire string).
- [x] 2.2 Register 6 нових DTOs у `SignalJsonContext`.
- [x] 2.3 `ISignalGroups` — 3 нові методи + XMLDoc з source-citation per §0.5 + inline §F8/F13/F14/F10 reminders.
- [x] 2.4 `SignalGroups.cs` — implementations. §F8 idempotent (NotAGroupMember → `WasAlreadyNotMember=true`, no throw), §F14 dual-mode XMLDoc, enum→kebab mapping у helpers.
- [x] 2.5 `SignalGroupsLog.cs` — 5 нових `[LoggerMessage]` методів у block **815-819** (НЕ 550-599 як вказано у task — той block належить SignalEventServiceLog за `.claude/rules/patterns.md`; Groups має block 810-819 within shared 800-899 range з Accounts/Devices). 5 методів замість 9 — pragmatic-only (existing Wave-1 pattern теж використовує 1 Ok-log per method).
- [x] 2.6 Serialization tests (9) + service tests (9) + options-validation tests (5) = **23 unit**. (Task plan: ~18; 23 cover'ять more edge cases — §F13 absent-not-false, §F14 create vs update both wire & service, §F8 idempotent contract.)
- [x] 2.7 Update `SignalCli.public-api.txt` baseline: 1239 → **1375** lines (+136 entries).
- [x] 2.8 Build + test: 333 → **358** (+25 у Wave 2). `TreatWarningsAsErrors=true` зелений.
- [x] 2.9 Bump 4.1.0 → 4.2.0 + CHANGELOG consumer-first voice.
- [ ] 2.10 Commit `feat(4.2.0): group CRUD — join/update/quit`. (User asked для local-only commit; push/merge/tag — окремий етап на user authorization.)

## 3. Wave 3 — `contacts-identity` *(4.3.0)*

- [x] 3.1 Нова папка `src/SignalCli/Models/Signal/Contacts/` — 16 DTOs (8 methods × 2):
  - `ListContactsParameters/Response.cs` + nested `Contact` record (number, profile-key, name, given/family-name, expirationSeconds, blocked, archived)
  - `ListIdentitiesParameters/Response.cs` + nested `Identity` record (number, fingerprint, safety-number, scannableSafetyNumber, trustLevel, addedTimestamp)
  - `TrustParameters` + `TrustOptions` (sealed record) + `TrustMode` enum (`TrustAllKnown` | `VerifiedSafetyNumber`)
  - `UpdateContactParameters` + `UpdateContactOptions`
  - `RemoveContactParameters` + `RemoveContactBehavior` enum (`Hide` | `Forget`). **§F9 reminder:** `RemoveContactCommand.java:23 @ bda4e7fc` uses `addMutuallyExclusiveGroup()` but the mutex is enforced ONLY at argparse4j CLI level — JSON-RPC clients can set BOTH `hide=true` and `forget=true` simultaneously, and upstream then executes `hide` first-wins. The .NET `RemoveContactOptions` Builder MUST validate XOR client-side (throw `ArgumentException` if both flags set) so wire never receives an ambiguous payload. See `research/wave-3-contacts-identity.md`.
  - `UpdateProfileParameters` + `UpdateProfileOptions` (sealed record, nullable: GivenName, FamilyName, About, AboutEmoji, MobileCoinAddress, AvatarPath, RemoveAvatar). **§F18 reminder:** `AvatarPath` vs `RemoveAvatar` XOR — same pattern as F9 (RemoveContact). Builder MUST validate XOR client-side (throw `ArgumentException` if both set); upstream `UpdateProfileCommand.java @ bda4e7fc` mutex is argparse-only — wire silently accepts both with undefined first-wins behavior. See `research/wave-3-contacts-identity.md` updateProfile section.
  - `BlockParameters` + `UnblockParameters` (shape identical, separate types для type-safety)
- [x] 3.2 Register 16 нових DTOs у `SignalJsonContext` (+ `List<JsonContact>`, `List<JsonIdentity>` wrapper-collections per critical rule N10).
- [x] 3.3 `src/SignalCli/Interfaces/Signal/ISignalContacts.cs` — NEW interface, **9** methods (Trust split на TrustAllKnownKeysAsync + TrustVerifiedAsync для type-safe XOR).
- [x] 3.4 `src/SignalCli/Services/Signal/SignalContacts.cs` — NEW service. Void-методи використовують JsonElement як response-тип (signal-cli emit'ить `"result": null` для void); §F9 RemoveContactMode→XOR mapping; §F18 defense-in-depth XOR guard.
- [x] 3.5 `src/SignalCli/Logging/SignalContactsLog.cs` — NEW, 8 `[LoggerMessage]` методів у **EventId block 830-849** (within shared 800-899 з Accounts/Devices/Groups per `.claude/rules/patterns.md`; **task plan мав помилку — вказував 600-649, виправлено** — 600-699 належить SignalServiceLog).
- [x] 3.6 `src/SignalCli/Extensions/ServiceCollectionExtensions.cs` — `services.TryAddSingleton<ISignalContacts, SignalContacts>()` у `AddSignalCli`.
- [x] 3.7 Serialization (9) + service (10) + builder (10) tests = **29 unit**.
- [x] 3.8 Створити `Tests/SignalCli.Tests.Integration/TestAccountFixture.cs` — `EnvVar = "SIGNALCLI_TEST_ACCOUNT"`, `TryGet()`, `TryGetOrSkip()`.
- [x] 3.8.1 E2E: `Tests/SignalCli.Tests.Integration/SignalCliE2EContactsTests.cs` — 2 env-gated тести (ListContacts + ListIdentities). Skip clean якщо env var відсутній.
- [x] 3.9 Update `SignalCli.public-api.txt` baseline: 1375 → **1642** lines (+267 entries: ISignalContacts + nested record types + 2 enums + 8 Parameters + 2 Response wrappers).
- [x] 3.10 Update `R02` (`EventIdBlockTests`) — додано reservation `[typeof(SignalContactsLog), 800, 899]`.
- [x] 3.11 Build + test: 358 → **387** (+29 unit) + 2 E2E env-gated. `TreatWarningsAsErrors=true` зелений.
- [x] 3.12 Bump 4.2.0 → 4.3.0 + CHANGELOG consumer-first voice.
- [ ] 3.13 Commit `feat(4.3.0): contacts & identities — list/trust/update/remove/profile/block`. (Local-only per user instruction; push/merge/tag — окремий етап.)

## 4. Wave 4 — `sticker-packs` + `binary-resource-fetch` *(4.4.0)*

- [x] 4.1 Нові папки `src/SignalCli/Models/Signal/Stickers/` + `Resources/`.
  - Stickers: 6 DTOs (Upload/List/Add Parameters + Upload/List Response + JsonStickerPack + **JsonStickerPackItem** [renamed з JsonSticker щоб уникнути колізії з existing event-side type `SignalCli.Models.Signal.JsonSticker`]).
  - Resources: 5 DTOs (GetAttachment/Avatar/Sticker Parameters + GetAvatarOptions Builder + shared JsonAttachmentData envelope). §F19 enforced у Builder + service defense-in-depth.
- [x] 4.2 Register 11 нових DTOs у `SignalJsonContext` (+ `List<JsonStickerPack>` wrapper-collection).
- [x] 4.3 `ISignalStickers` + `SignalStickers.cs` (NEW), `ISignalResources` + `SignalResources.cs` (NEW). Resources service декодує base64 у `byte[]`; invalid base64 = `InvalidOperationException` з diagnostic method-name. `GetStickerAsync` робить client-side hex-validation packId (32-char lowercase) щоб уникнути upstream `-32603 INTERNAL_ERROR`.
- [x] 4.4 Logging: `SignalStickersLog.cs` (block **850-859**, 3 methods), `SignalResourcesLog.cs` (block **860-869**, 4 methods). **Deviation:** task plan вказував blocks 650-679/680-699, але ті range'и належать SignalServiceLog per `.claude/rules/patterns.md`. Stickers/Resources розміщені у shared 800-899 разом з іншими signal-protocol facades.
- [x] 4.5 DI registration двох нових services у `ServiceCollectionExtensions`.
- [x] 4.6 Serialization (7) + service (4 Stickers + 10 Resources) + builder (7 GetAvatar) tests = **28 unit**. Включно з invalid-base64 edge case + hex-validation (3 negative tests).
- [x] 4.7 Update `SignalCli.public-api.txt` baseline: 1642 → **1785** lines (+143 entries).
- [x] 4.8 Update `RG02` — додано `[typeof(SignalStickersLog), 800, 899]` + `[typeof(SignalResourcesLog), 800, 899]`.
- [x] 4.9 Build + test: 387 → **416** (+29 unit). `TreatWarningsAsErrors=true` зелений.
- [x] ~~4.10 Receive-side sticker-pack-install event decoder~~ Out of scope per §F1 (no upstream wire field).
- [x] 4.11 Bump 4.3.0 → 4.4.0 + CHANGELOG.
- [ ] 4.12 Commit `feat(4.4.0): sticker packs + binary resource fetch`. (Local-only per user instruction.)

## 5. Wave 5 — `device-management` *(4.5.0)*

- [x] 5.1 `src/SignalCli/Models/Signal/Devices/`: AddDeviceParameters, Device record (§F6 — 4 fields only), ListDevicesParameters/Response (wrapper), RemoveDeviceParameters (int), UpdateDeviceParameters.
- [x] 5.2 Register у `SignalJsonContext` + `List<Device>` wrapper.
- [x] 5.3 `ISignalDevices` — 4 нові методи з XMLDoc що чітко differentiate primary-перспективу (Add/List/Remove/Update) vs existing secondary-перспективу (StartLink/FinishLink).
- [x] 5.4 `SignalDevices.cs` — implementations. **§F11 deviation:** padding-restoration helper НЕ доданий — `AddDeviceAsync` pure pass-through URI без декодування `pub_key`, тож padding не потрібен (Java Base64 lenient до padding'у). Documented як non-issue у XMLDoc.
- [x] 5.5 `SignalDevicesLog.cs` — 5 нових `[LoggerMessage]` у block **820-829** (existing Devices range; **task plan мав помилку — вказував 500-549, виправлено**; 500-549 належить SignalEventServiceLog). §F12 enforced: жодних `{DeviceName}` у Information+ шаблонах — лише `{DeviceId}`.
- [x] 5.6 Serialization (8) + service (10) tests = **18 unit** (Plan: ~12).
- [x] 5.7 E2E: `SignalCliE2EDevicesTests.ListDevices_ReturnsAtLeastSelf` — env-gated.
- [x] 5.8 Update `SignalCli.public-api.txt` baseline: 1785 → **1853** lines (+68 entries).
- [x] 5.9 Build + test: 416 → **434** (+18 unit). `TreatWarningsAsErrors=true` зелений.
- [x] 5.10 Bump 4.4.0 → 4.5.0 + CHANGELOG.
- [ ] 5.11 Commit `feat(4.5.0): device management — add/list/remove/update from primary perspective`. (Local-only.)

## 6. Wave 6 — `account-lifecycle` *(4.6.0)* — **opt-in gated**

### 6.1 Options-pattern extension

- [x] 6.1.1 `src/SignalCli/Models/SignalCliOptions.cs` — додано `EnableDestructiveOperations: bool = false` property з XMLDoc-попередженням про irreversible operations.
- [x] 6.1.2 `SignalCliOptionsValidator.cs` — нічого не міняється (простий bool, без cross-field rules).
- [x] 6.1.3 Test contract incorporated у DestructiveOpsGatedTests (default-false → throw для всіх 8 destructive).

### 6.2 RPC methods + DTOs

- [x] 6.2.1 `src/SignalCli/Models/Signal/Accounts/` — 13 нових DTOs (Update/StartChange/FinishChange/UpdateConfiguration Options + 8 Parameters + UpdateAccountResponse — решта 7 methods використовують JsonElement для void responses):
  - `UpdateAccountParameters/Response.cs` + `UpdateAccountOptions` (Builder; DeviceName, DiscoverableByNumber, UnrestrictedUnidentifiedSender, **`NumberSharing: bool?`** — per §F3, upstream `UpdateAccountCommand.java:37-39 @ bda4e7fc` registers `--number-sharing` as `type(Boolean.class)`; NOT an enum. Internal `PhoneNumberSharingMode` enum exists у `manager/api/` але **не** експонується через JSON-RPC). XMLDoc lists username/delete-username as optional too — see `research/wave-6-account-lifecycle.md`.
  - `UpdateConfigurationParameters/Response.cs` + `UpdateConfigurationOptions` (4 nullable bool: ReadReceipts, UnidentifiedDeliveryIndicators, TypingIndicators, LinkPreviews)
  - `SetPinParameters/Response.cs`, `RemovePinParameters/Response.cs`
  - `UnregisterParameters/Response.cs` (поле `delete: bool`)
  - `DeleteLocalAccountDataParameters/Response.cs` (поле `ignoreRegistered: bool`)
  - `StartChangeNumberParameters/Response.cs` + `StartChangeNumberOptions` (NewNumber, **`Voice: bool`** with `[JsonPropertyName("voice")]` — per §F4, upstream `StartChangeNumberCommand.java:33 @ bda4e7fc` registers `"-v", "--voice"`; .NET property name MAY stay `VoiceVerification` if `[JsonPropertyName("voice")]` aligns the wire), Captcha)
  - `FinishChangeNumberParameters/Response.cs` + `FinishChangeNumberOptions` (NewNumber, VerificationCode, Pin)
- [x] 6.2.2 Register у `SignalJsonContext` — 9 нових типів (parameters + UpdateAccountResponse).
- [x] 6.2.3 `ISignalAccounts` — додано 8 нових destructive методів з XMLDoc що explicitly попереджає про gate + irreversibility.
- [x] 6.2.4 `SignalAccounts.cs`: ctor приймає `IOptions<SignalCliOptions>`, кешує `_destructiveOpsEnabled` per CLAUDE.md rule #10. Helper `EnsureDestructiveAllowed([CallerMemberName])` логує + throw'ить. 8 destructive методів дзвонять його першим.
- [x] 6.2.5 `SignalAccountsLog.cs` — +9 `[LoggerMessage]` у block **870-879** (within shared 800-899; **task plan мав помилку — вказував 450-499 (JsonRpcClientHostedServiceLog)**, виправлено). Включає `DestructiveOperationBlocked(method)` Warning.

### 6.3 Tests

- [x] 6.3.1 Serialization tests для DTOs (10 unit including Options shape pinning).
- [x] 6.3.2 `DestructiveOpsGatedTests` — 10 тестів: 8 methods × default-false → InvalidOperationException + ListAccounts non-destructive не affected + enabled=true → RPC dispatches.
- [x] 6.3.3 `AccountLifecycleOptionsTests` — 5 builder тестів (UpdateAccountOptions XOR + edge cases).
- [x] 6.3.4 `RG02` `EventIdBlockTests` уже covers SignalAccountsLog (800-899) — нові EventIds 870-879 within range.
- [x] 6.3.5 Update `SignalCli.public-api.txt` baseline: 1853 → **2020** lines (+167 entries: 8 methods + 16 DTOs + EnableDestructiveOperations flag).

### 6.4 Documentation

- [x] 6.4.1 CHANGELOG entry — Destructive operations section з warning + opt-in example.
- [ ] 6.4.2 CLAUDE.md "Critical rules" — додавання rule #19 deferred (можна додати у follow-up commit; rule встановлено за фактом у service implementation).
- [ ] 6.4.3 RG09 `DestructiveOpsGatingTests` reflection guard — deferred (наразі DestructiveOpsGatedTests pin'ить contract через explicit-per-method tests; reflection guard може бути follow-up).

### 6.5 Release

- [x] Build + test: 434 → **459** (+25 unit). `TreatWarningsAsErrors=true` зелений.
- [x] Bump 4.5.0 → 4.6.0 + CHANGELOG consumer-first voice з ⚠ warnings.
- [ ] Commit `feat(4.6.0): destructive account lifecycle — opt-in gated`. (Local-only.)

### 6.5 Release

- [ ] 6.5.1 Build + test (count ~408 → ~456 unit + 3 E2E).
- [ ] 6.5.2 Bump 4.5.0 → 4.6.0 + CHANGELOG з prominent **⚠ DESTRUCTIVE OPS** warning у consumer-first voice.
- [ ] 6.5.3 Commit `feat(4.6.0): account lifecycle — destructive ops behind EnableDestructiveOperations opt-in`. Push, merge, tag.

## 7. Wave 7 — `polls` + `messaging-power-user` *(4.7.0)*

- [ ] 7.1 Polls DTOs (6 new): `PollCreateOptions` (Builder; question, options array 2-10, allowMultipleVotes) + `SendPollCreateParameters/Response.cs`. Repeat для `PollVote`, `PollTerminate`.
  - **§F15 reminder:** Validation constants від upstream — `MAX_POLL_OPTIONS = 10`, `MAX_POLL_OPTION_LENGTH = 100`. `PollCreateOptions.Builder.Build()` MUST throw `ArgumentException` if `Options.Count < 2 || Options.Count > 10` OR any option string longer than 100 chars. Constants baked into `SendPollCreateCommand.java @ bda4e7fc`, не configurable. See `research/wave-7-polls-power-user.md` sendPollCreate section.
  - **§F21 reminder:** Polarity inversion — upstream CLI flag is `--no-multi` (negative); internal Java API is `allowMultiple` (positive). .NET MUST expose positive polarity (`PollCreateOptions.AllowMultipleVotes: bool = true` default), with wire mapping handled in serialization (`[JsonPropertyName("allowMultiple")]`). Don't mirror CLI naming — double-negative cognitive cost.
  - **§F22 reminder:** `sendPollVote.option` field — IS zero-based integer indexes into the original poll's options array, NOT strings. DTO field: `Options: IReadOnlyList<int>` (NOT `IReadOnlyList<string>`). Easy to misimplement; explicit XMLDoc warning + Builder validation that indexes are 0 ≤ x < originalPollOptionCount.
- [ ] 7.2 Power-user DTOs (10 new): SendAdminDeleteParameters/Response + AdminDeleteOptions; SendPinMessageParameters/Response + PinMessageOptions (**§F16 reminder:** `pinDurationSeconds` має type asymmetry — `int` на send wire, `long` на receive wire. .NET MUST use `long` на обох sides (widest type) для уникання silent truncation у corner-case'ах де upstream eventually збільшить duration cap. **§F23 reminder:** Upstream sentinel value `pinDurationSeconds = -1` = "pin forever"; positive seconds = limited duration. .NET ergonomic design: expose як `PinDuration: TimeSpan?` (null = forever, otherwise duration); serialize null → `-1`, TimeSpan → total seconds. Hide sentinel implementation detail.); SendUnpinMessageParameters/Response + UnpinMessageOptions; SendMessageRequestResponseParameters/Response + MessageRequestResponseOptions + `MessageRequestResponseType` enum **`{ Accept, Delete }` — 2 values only** per §F2, upstream `src/main/java/org/asamk/signal/commands/MessageRequestResponseType.java @ bda4e7fc` declares lone `ACCEPT`/`DELETE`. Java `.toString()` returns lowercase (`"accept"`/`"delete"`) — pin wire casing via `[JsonStringEnumConverter(JsonNamingPolicy.CamelCase)]` or explicit `[JsonStringEnumMemberName("accept")]` after verifying real wire payload. The 8-value enum (Unknown, Accept, Delete, Block, BlockAndDelete, UnblockAndAccept, Spam, BlockAndSpam) lives у `MessageEnvelope.Sync.MessageRequestResponse.Type` and is **receive-side only** — out of scope here (`research/SUMMARY.md` §F2); SendPaymentNotificationParameters/Response + PaymentNotificationOptions.
- [x] 7.3 Register 9 DTOs у `SignalJsonContext` (8 Parameters; reuse SendMessageResponse + JsonElement).
- [x] 7.4 `ISignalMessage` — додано 8 нових методів (3 polls + 5 power-user) з XMLDoc + §F-citations.
- [x] 7.5 `SignalMessage.cs` — implementations. §F21 polarity inversion, §F23 -1 sentinel pass-through, §F2 enum→lowercase mapping.
- [x] 7.6 `SignalMessageLog.cs` — +8 `[LoggerMessage]` у block **707-714** (within existing 700-799; **task plan мав помилку — вказував 400-449 (JsonRpcClientHostedServiceLog)**, виправлено).
- [x] 7.7 Tests: 6 serialization + 9 builder = **15 unit**. Wave 7 plan мав ~32; pragmatic scope для send-side.

### 7.8 Receive-side event decoders — **DEFERRED to Wave 7b (4.7.1)**

Decision (2026-05-25, per user scope choice "Send-side only"): receive-side decoders shipped as окремий 4.7.1 patch-release замість 4.7.0. Rationale: send-side та receive-side ortogonal value-add'и; refactor SignalEventService dispatch (682 lines existing + 7 нових Subject/Channel pairs + 7 emission blocks) — окрема велика capability що варта окремого PR-cycle. Send-side itself — 51/54 = 94% RPC coverage; receive-side додає 0% RPC coverage (це event surface, не RPC) — низький blocking-priority.

- [ ] 7.8.1 Re-engineer 7 DTOs з upstream'у (deferred 7b) (per source-of-truth protocol §0.5):
  - `src/SignalCli/Models/Signal/Envelope.cs` — extend `JsonDataMessage` 7-ма nullable полями: `PollCreate`, `PollVote`, `PollTerminate`, `Payment`, `PinMessage`, `UnpinMessage`, `AdminDelete`.
  - Add 7 nested records `JsonPollCreate`/`JsonPollVote`/`JsonPollTerminate`/`JsonPayment`/`JsonPinMessage`/`JsonUnpinMessage`/`JsonAdminDelete` — кожен з citation у XMLDoc на `src/main/java/org/asamk/signal/json/Json<X>.java @ <pinned-sha>`.
  - **§F17 reminder:** 5 з 7 receive-side Json* records (per agent's wave-7 research) carry `@Deprecated targetAuthor`/`author` legacy-identifier field що upstream **досі serializes** для backward-compat з консумерами що читали older wire payloads. .NET DTOs MUST mirror з `[Obsolete]` marker на властивостях AND still serialize them (forward-compat з old wire payloads — strict-deserialization кине exception, ми НЕ хочемо forward-compat'у поламати). Перевірити кожен з 7 на `@Deprecated` annotations: spawn окремий research subtask якщо потрібно. See `research/wave-7-polls-power-user.md` finding #11.
  - **§F16 reminder (receive-side):** `JsonPinMessage.pinDurationSeconds` на receive — `long`; на send (§7.2) — `int`. Use `long` для DTO consistency. Same `[JsonPropertyName("pinDurationSeconds")]`.
- [ ] 7.8.2 New event-args в `src/SignalCli/Models/Signal/Events/`:
  - `PollCreateEventArgs`, `PollVoteEventArgs`, `PollTerminateEventArgs`, `PaymentNotificationEventArgs`, `PinMessageEventArgs`, `UnpinMessageEventArgs`, `AdminDeleteEventArgs`. Кожен містить `Envelope` + relevant nested payload (no PII у XMLDoc).
- [ ] 7.8.3 Register 7 нових `Json*` + 7 `*EventArgs` у `SignalJsonContext`.
- [ ] 7.8.4 `ISignalEventService` — додати 7 пар `IObservable<T> Foo { get; }` + `IAsyncEnumerable<T> FoosAsync(CancellationToken ct = default)`. RG06 `EventApiSymmetryTests` auto-enforce.
- [ ] 7.8.5 `SignalEventService.cs` — додати 7 `Subject<T>` + 7 `Channel<T>` (bounded capacity = existing `NotificationChannelCapacity`), 7 emission блоків у `DispatchDataMessage` (per critical rule #4: presence-based union, NO early return).
- [ ] 7.8.6 `SignalEventServiceLog.cs` — +14 `[LoggerMessage]` (Received + DroppedFromChannel per event type, у block 300-399).
- [ ] 7.8.7 Tests:
  - 7 serialization-roundtrip tests з inline-literal JSON envelopes crafted to match Jackson Java-record output.
  - 1 union-test: envelope з `text + reaction + pollVote` → assert всі три emit'ять (regression for critical rule #4).
  - 1 back-pressure test для одного з нових channels (mirror existing pattern).
- [ ] 7.8.8 **Manual live-capture sanity check** (5-min procedure, NOT automated CI):
  - Developer з 2 пристроями того ж акаунта.
  - Викликати `await message.SendPollCreateAsync(...)` з пристрою A.
  - Subscribe на `events.PollCreates.Subscribe(arg => ...)` на пристрої B.
  - Capture raw JSON envelope через `_logger.LogTrace`.
  - Diff проти inline-literal-snapshot у `Tests/SignalCli.Tests/Serialization/EventDecodersSerializationTests.cs`.
  - Якщо drift — оновити snapshot ДО Wave 7 merge.
  - Document procedure step-by-step у `docs/dev-event-decoder-sanity-check.md` (новий файл — лише розробницька внутрішня doc).

- [ ] 7.9 Update `SignalCli.public-api.txt` (включно з 14 нових ISignalEventService API surface members).
- [ ] 7.10 Build + test (~456 → ~497 unit, +9 тестів від event-decoders).
- [ ] 7.11 Bump 4.6.0 → 4.7.0 + CHANGELOG.
- [ ] 7.12 Commit `feat(4.7.0): polls + power-user messaging — send-side + receive-side event decoders`, push, merge, tag.

## 8. Wave 8 — `utility-rpc` *(4.8.0)*

- [ ] 8.1 Нова папка `src/SignalCli/Models/Signal/Utility/` — 6 DTOs (3 methods × 2):
  - **§F25 reminder:** Empty RPC responses (Wave-8 `submitRateLimitChallenge`/`sendContacts` + Wave-3 mutating methods + Wave-6 destructive methods + Wave-7 `sendMessageRequestResponse`) are literal JSON `{}` object, NOT `null`. Source: `SignalJsonRpcCommandHandler.java:281 @ bda4e7fc` (`result[0] == null ? Map.of() : result[0]`). DTO design: each new Response type with no fields = `public sealed record FooResponse()` (empty body record), NOT `record FooResponse(SomeNullable? x = null)` or `Task<Empty?>`. Service-method signature: `Task SubmitRateLimitChallengeAsync(...)` (no return value) — caller doesn't care, but wire still produces `{}` not `null`.
  - **§F24 reminder:** `submitRateLimitChallenge` missing-key throws NPE upstream → maps to `-32603 InternalError` (NOT typical `-32602 InvalidParams` for missing required field). Upstream `required(true)` on argparse4j `Argument` is NOT enforced for JSON-RPC payloads. Test serialization with deliberately-missing `challenge` field → assert `JsonRpcException.KnownCode == JsonRpcErrorCode.InternalError` (-32603), not `InvalidParams` (-32602). See `research/wave-8-utility-rpc.md` submitRateLimitChallenge section.
  - `GetUserStatusParameters/Response.cs` + `GetUserStatusOptions` (account, **`Recipients` AND `Usernames` arrays — NOT mutually exclusive** per §F5). Upstream `GetUserStatusCommand.java:66-81 @ bda4e7fc` merges both via `Stream.concat`; response is a flat list of `JsonUserStatus { recipient, number?, username?, uuid?, isRegistered }` where `recipient` echoes the caller's input, `number` is populated only for phone inputs, `username` only for username inputs, `isRegistered` derived as `uuid != null`. Wrapper-record + `[JsonConverter]` pattern (per critical rule #N10) for the top-level array response. `SubmitRateLimitChallengeParameters/Response.cs` — params `{challenge, captcha}` both required (upstream NPE → `-32603` if missing per §F-supplement, not `-32602`); response empty `{}`.
  - `SendContactsParameters/Response.cs` — empty params shape (just account); empty `{}` response.
- [ ] 8.2 Register у `SignalJsonContext`.
- [x] 8.3 `ISignalAccounts` — додано 3 utility-методи (non-destructive — без gating).
- [x] 8.4 `SignalAccounts.cs` — implementations. §F5 AND/OR pass-through, §F24 CaptchaRequired auto-dispatch через JsonRpcClient (Wave-1 infra), §F25 empty {} → JsonElement reuse.
- [x] 8.5 `SignalAccountsLog.cs` — +3 `[LoggerMessage]` у block **879-881** (within shared 800-899; **task plan мав помилку — 450-499 (JsonRpcClientHostedServiceLog)**, виправлено).
- [x] 8.6 Tests: 5 serialization + 7 service = **12 unit**.
- [x] 8.7 E2E: `SignalCliE2EUtilityRpcTests.GetUserStatus_Self_ReturnsRegistered` — env-gated.
- [x] 8.8 Update `SignalCli.public-api.txt` baseline: 2300 → **2359** lines (+59 entries).
- [x] 8.9 Build + test: 474 → **486** (+12 unit, +1 E2E).
- [x] 8.10 Bump 4.7.0 → 4.8.0 + CHANGELOG (consumer-first з 🎯 GOAL REACHED milestone).
- [ ] 8.11 Commit `feat(4.8.0): utility RPC — getUserStatus, submitRateLimitChallenge, sendContacts`. (Local-only.)

## 9. Final cleanup

- [ ] 9.1 Перевірити що actual coverage: `grep -c '"' src/SignalCli/Services/Signal/*.cs | grep -E 'send|list|get|update|remove|add|trust|block|unblock|join|quit|set|unregister|submit|finish|start'` → ≥49 unique RPC method literals.
- [ ] 9.2 Verify RG10 (`SourceCitationConsistencyTests`) green — every public service method on the 7 facades carries `org.asamk.signal.*.java @ <sha>` citation у `<remarks>`. Build fail без exhaustive coverage.
- [ ] 9.3 Update root CLAUDE.md "Implemented, merged, archived" — додати entry `signal-cli-api-coverage (4.1.0–4.8.0, archived YYYY-MM-DD)` з summary "raised JSON-RPC coverage 18% → 98%; introduced §0.5 source-of-truth anti-hallucination protocol; added RG10 regression guard".
- [ ] 9.4 Update root CLAUDE.md "Critical rules" — додати rule #20: "Every new RPC service-method MUST cite `org.asamk.signal.*.java @ <commit-sha>` у XMLDoc `<remarks>`. Enforced by RG10 build-time test. Rationale: anti-LLM-hallucination guard — wire shapes come from source-reading, not training-data inference. See §0.5 у `signal-cli-api-coverage` proposal."
- [ ] 9.5 Update root CLAUDE.md "Audit baseline → Тестова база": **unit tests ≥ 506** (від 290), **E2E tests ≥ 6** (від 2; env-gated через `SIGNALCLI_TEST_ACCOUNT`).
- [ ] 9.6 Update `.claude/rules/signal-cli-protocol.md` — додати entry "Wire shapes for receive-side payload types (poll/payment/pin/admin-delete/sticker-pack-install) sourced from `org.asamk.signal.json.Json*.java` records @ pinned SHA (see proposal §1.9)".
- [ ] 9.7 `npx -y @fission-ai/openspec@latest archive signal-cli-api-coverage --yes --skip-specs` (after all waves merged).
- [ ] 9.8 Commit `chore(openspec): archive signal-cli-api-coverage → YYYY-MM-DD`. Push.

## 10. Risk register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Wave 6 destructive op accidentally fires у production без `EnableDestructiveOperations` | low (default `false`) | **catastrophic** (data loss) | Gate test 6.3.2 + RG09 regression guard + opt-in flag + XMLDoc warning + README warning + CLAUDE.md critical rule #19 |
| **LLM-галюцинації про signal-cli API surface** — field name guessed замість source-read | **high without protocol** | **catastrophic** (runtime serialization fail на production envelope; bug invisible до first real RPC) | §0.5 mandatory source-of-truth protocol: read upstream Java record per кожен method, cite path+SHA у XMLDoc, RG10 build-time guard (`SourceCitationConsistencyTests`), snapshot tests з real signal-cli capture, review-time checkpoint |
| signal-cli wire-shape для нового method'у відрізняється від нашого DTO | medium (no schema upstream) | medium (runtime serialization fail) | Inline-literal JSON snapshots з real signal-cli `--verbose` capture (task 0.4) + source-citation (§0.5) — drift одразу видно при upstream release bump |
| EventId collision у `[LoggerMessage]` методах | low | medium (build fail) | `R02` EventIdBlockTests pin'ає blocks + per-wave reservation (600-649 contacts, 650-679 stickers, 680-699 resources) |
| Public API baseline drift не помічений | low | low (release with API surprise) | `R03` PublicApiSurfaceTests blocks build until baseline updated; task 1.3.5/2.7/3.9/4.7/5.8/6.3.5/7.8/8.8 explicit |
| Wave breaks existing 9 methods через cross-cutting change у Wave 1 (typed exceptions) | low | high | Existing tests covering 9 methods МАЮТЬ всі pass'ити; додати explicit regression test що `catch (JsonRpcException)` continues to catch all error codes (backward compat) |
| Test suite count drops between waves (flake від wall-clock-dependent test) | low | medium (false-pass) | CLAUDE.md rule #11 + audit-debt.md "wall-clock-independent suite"; всі нові tests з mocks (no FakeTimeProvider needed для serialization-only tests) |
| `account-lifecycle` destructive method'и створюють divergence у тестовому signal-cli account state між test runs | low (only fires при `EnableDestructiveOperations = true` + actual invoke) | low | mock-only testing — destructive methods НЕ викликаються against real signal-cli |
