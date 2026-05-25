# Tasks — signal-cli-api-coverage

8 release waves. Кожен wave = own branch from main + own PR + own minor-bump + own CHANGELOG entry. Wave-N не починається до merge'у Wave-(N-1).

## 0. Setup

- [ ] 0.1 Branch `claude/signal-cli-api-coverage` from current `main` (вже існує — продовжуємо).
- [ ] 0.2 `npx -y @fission-ai/openspec@latest validate signal-cli-api-coverage --strict` — green.
- [ ] 0.3 Capture сирі JSON-RPC payloads для всіх 44 нових методів через signal-cli running locally з `--verbose`, embed як inline-literals у serialization-tests (so wire-shape pinned від day 1, не reverse-engineered).

## 1. Wave 1 — `messaging-interactive` *(4.1.0)*

### 1.1 Cross-cutting (lands у Wave 1 бо потрібен `IdentityChangedException` для sendReaction error mapping)

- [ ] 1.1.1 `src/SignalCli/Exceptions/IdentityChangedException.cs` — `sealed` derived з `UntrustedIdentityException`. XMLDoc пояснює різницю semantic'у (re-install vs initial-contact).
- [ ] 1.1.2 `src/SignalCli/Exceptions/GroupAdminRequiredException.cs` — `sealed` derived з `JsonRpcException`. XMLDoc: "Поверх UserError(-1) коли signal-cli message contain'ить 'admin'".
- [ ] 1.1.3 `src/SignalCli/Exceptions/CaptchaRequiredException.cs` — `sealed` derived з `JsonRpcException`. XMLDoc лінкує на signalcaptchas.org workflow + `SubmitRateLimitChallengeAsync` (forward ref).
- [ ] 1.1.4 `src/SignalCli/Services/Rpc/JsonRpcClient.cs` — extend `InvokeMethodAsync` switch (per design §1.2):
  - `(int)JsonRpcErrorCode.CaptchaRejected => new CaptchaRequiredException(response.Error)`
  - `(int)JsonRpcErrorCode.UserError when message contains "admin" => new GroupAdminRequiredException(response.Error)`
  - `IdentityChangedException` НЕ диспатчиться — це opt-in subset of UntrustedIdentityException (caller catch'ить її через `catch (IdentityChangedException)` що працює бо вона derived).
- [ ] 1.1.5 Tests: `Tests/SignalCli.Tests/Exceptions/NewTypedRpcErrorsTests.cs` — 3 theory tests, кожен exception type roundtrips correctly з `JsonRpcClient`.

### 1.2 RPC methods + DTOs

- [ ] 1.2.1 `src/SignalCli/Models/Signal/Message/ReactionOptions.cs` — `sealed record` + nested `Builder`, поля: Account, Recipients/GroupIds, Emoji, TargetAuthor, TargetTimestamp, Remove.
- [ ] 1.2.2 `src/SignalCli/Models/Signal/Message/SendReactionParameters.cs` + `SendReactionResponse.cs`.
- [ ] 1.2.3 Repeat 1.2.1+1.2.2 pattern для `ReceiptOptions` / `SendReceiptParameters` / `SendReceiptResponse`. `ReceiptType` enum: `Read` | `Viewed`.
- [ ] 1.2.4 Repeat для `TypingOptions` / `SendTypingParameters` / `SendTypingResponse`. Поле `Stop: bool` (default false = start typing).
- [ ] 1.2.5 Repeat для `RemoteDeleteOptions` / `RemoteDeleteParameters` / `RemoteDeleteResponse`.
- [ ] 1.2.6 Register 8 нових DTOs у `Serialization/SignalJsonContext.cs`.
- [ ] 1.2.7 `src/SignalCli/Interfaces/Signal/ISignalMessage.cs` — додати 4 нові методи з XMLDoc.
- [ ] 1.2.8 `src/SignalCli/Services/Signal/SignalMessage.cs` — реалізувати 4 нові методи, дотримуючись existing pattern (`InvokeMethodAsync` + null-check + log).
- [ ] 1.2.9 `src/SignalCli/Logging/SignalMessageLog.cs` — додати 12 нових `[LoggerMessage]` методів (3 per RPC method: Requested, NullResponse, ValidationFailed) у EventId block 400-449.

### 1.3 Tests

- [ ] 1.3.1 `Tests/SignalCli.Tests/Serialization/MessagingInteractiveSerializationTests.cs` — 8 serialization roundtrip tests (4 params + 4 responses) з inline-literal-JSON snapshots з real signal-cli.
- [ ] 1.3.2 `Tests/SignalCli.Tests/Services/Signal/SignalMessageReactionTests.cs` — 3 tests (happy path + identity-changed catch + validation).
- [ ] 1.3.3 Те ж саме для Receipt, Typing, RemoteDelete = 9 more tests.
- [ ] 1.3.4 `Tests/SignalCli.Tests/Models/Signal/Message/ReactionOptionsTests.cs` — builder + validation. 4 files × ~4 tests = 16 tests.
- [ ] 1.3.5 Update `Tests/SignalCli.Tests/RegressionGuards/PublicApiSurface/SignalCli.public-api.txt` baseline.

### 1.4 Release

- [ ] 1.4.1 `dotnet build -p:TreatWarningsAsErrors=true && dotnet test SignalCli.sln` — clean, count ~287 → ~327.
- [ ] 1.4.2 `Directory.Build.props` — `<SignalCliPackageVersion>4.1.0</SignalCliPackageVersion>`.
- [ ] 1.4.3 `CHANGELOG.md` — нова `## [4.1.0] — YYYY-MM-DD` секція, consumer-first voice (`.claude/rules/openspec-workflow.md`).
- [ ] 1.4.4 Single commit: `feat(4.1.0): interactive messaging — reactions, receipts, typing, remote-delete`. Push.
- [ ] 1.4.5 Wait merge → tag `v4.1.0`.

## 2. Wave 2 — `groups-crud` *(4.2.0)*

- [ ] 2.1 `src/SignalCli/Models/Signal/Groups/`:
  - `JoinGroupParameters.cs` / `JoinGroupResponse.cs`
  - `UpdateGroupOptions.cs` (sealed record + Builder, ~12 nullable properties: Name, Description, AvatarPath, Members/Admins add/remove arrays, ExpirationSeconds, LinkState enum, Permission* enums) + `UpdateGroupParameters.cs` / `UpdateGroupResponse.cs`
  - `QuitGroupParameters.cs` + `QuitGroupBehavior` enum
- [ ] 2.2 Register 6 нових DTOs у `SignalJsonContext`.
- [ ] 2.3 `ISignalGroups` — 3 нові методи + XMLDoc.
- [ ] 2.4 `SignalGroups.cs` — implementations.
- [ ] 2.5 `SignalGroupsLog.cs` — 9 нових `[LoggerMessage]` методів у block 550-599.
- [ ] 2.6 Serialization tests + service tests + options-validation tests (~15 unit + 3 builder tests).
- [ ] 2.7 Update `SignalCli.public-api.txt` baseline.
- [ ] 2.8 Build + test (count ~327 → ~352).
- [ ] 2.9 Bump 4.1.0 → 4.2.0 + CHANGELOG.
- [ ] 2.10 Commit `feat(4.2.0): group CRUD — join/update/quit`, push, merge, tag.

## 3. Wave 3 — `contacts-identity` *(4.3.0)*

- [ ] 3.1 Нова папка `src/SignalCli/Models/Signal/Contacts/` — 16 DTOs (8 methods × 2):
  - `ListContactsParameters/Response.cs` + nested `Contact` record (number, profile-key, name, given/family-name, expirationSeconds, blocked, archived)
  - `ListIdentitiesParameters/Response.cs` + nested `Identity` record (number, fingerprint, safety-number, scannableSafetyNumber, trustLevel, addedTimestamp)
  - `TrustParameters` + `TrustOptions` (sealed record) + `TrustMode` enum (`TrustAllKnown` | `VerifiedSafetyNumber`)
  - `UpdateContactParameters` + `UpdateContactOptions`
  - `RemoveContactParameters` + `RemoveContactBehavior` enum (`Hide` | `Forget`)
  - `UpdateProfileParameters` + `UpdateProfileOptions` (sealed record, nullable: GivenName, FamilyName, About, AboutEmoji, MobileCoinAddress, AvatarPath, RemoveAvatar)
  - `BlockParameters` + `UnblockParameters` (shape identical, separate types для type-safety)
- [ ] 3.2 Register 16 нових DTOs у `SignalJsonContext` (+ `List<Contact>`, `List<Identity>` wrapper-collections per critical rule N10).
- [ ] 3.3 `src/SignalCli/Interfaces/Signal/ISignalContacts.cs` — NEW interface, 8 methods.
- [ ] 3.4 `src/SignalCli/Services/Signal/SignalContacts.cs` — NEW service, implementations.
- [ ] 3.5 `src/SignalCli/Logging/SignalContactsLog.cs` — NEW, ~24 `[LoggerMessage]` методів у NEW EventId block 600-649.
- [ ] 3.6 `src/SignalCli/Extensions/ServiceCollectionExtensions.cs` — `services.TryAddSingleton<ISignalContacts, SignalContacts>()` у `AddSignalCli`.
- [ ] 3.7 Serialization + service + validation tests (~24 unit).
- [ ] 3.8 E2E: `Tests/SignalCli.Tests.Integration/SignalCliE2EContactsTests.cs` — `ListContacts_Returns_Empty_Or_Populated`, `ListIdentities_Returns_Own_Identity_AtMinimum`. Both read-only.
- [ ] 3.9 Update `SignalCli.public-api.txt` baseline (new namespace + interface + 8 methods).
- [ ] 3.10 Update `R02` (`EventIdBlockTests`) — додати reservation 600-649 для `SignalContactsLog`.
- [ ] 3.11 Build + test (count ~352 → ~378 unit + 2 E2E).
- [ ] 3.12 Bump 4.2.0 → 4.3.0 + CHANGELOG.
- [ ] 3.13 Commit `feat(4.3.0): contacts & identities — list/trust/update/remove/profile/block`, push, merge, tag.

## 4. Wave 4 — `sticker-packs` + `binary-resource-fetch` *(4.4.0)*

- [ ] 4.1 Нові папки `src/SignalCli/Models/Signal/Stickers/` + `Resources/`.
  - Stickers: 6 DTOs (3 methods × 2) + nested `StickerPack` record (packId, packKey, url, title, author, installed, etc.).
  - Resources: 6 DTOs. Response містить base64-string `Data` field.
- [ ] 4.2 Register 12 нових DTOs у `SignalJsonContext` (+ `List<StickerPack>` wrapper).
- [ ] 4.3 `ISignalStickers` + `SignalStickers.cs` (NEW), `ISignalResources` + `SignalResources.cs` (NEW). Resources service декодує base64 у `byte[]` перед return; невалідний base64 = `InvalidOperationException("invalid base64 payload from signal-cli")`.
- [ ] 4.4 Logging: `SignalStickersLog.cs` (block 650-679, ~9 methods), `SignalResourcesLog.cs` (block 680-699, ~9 methods).
- [ ] 4.5 DI registration двох нових services у `ServiceCollectionExtensions`.
- [ ] 4.6 Serialization + service + base64-decoding tests (~18 unit), включаючи edge case "invalid base64 payload → throw".
- [ ] 4.7 Update `SignalCli.public-api.txt` baseline.
- [ ] 4.8 Update `R02` — додати reservations 650-679, 680-699.
- [ ] 4.9 Build + test (count ~378 → ~396).
- [ ] 4.10 Bump 4.3.0 → 4.4.0 + CHANGELOG.
- [ ] 4.11 Commit `feat(4.4.0): sticker packs + binary resource fetch — getAttachment/getAvatar/getSticker + list/upload/addStickerPack`, push, merge, tag.

## 5. Wave 5 — `device-management` *(4.5.0)*

- [ ] 5.1 `src/SignalCli/Models/Signal/Devices/`:
  - `AddDeviceParameters.cs` (просто account + URI)
  - `ListDevicesParameters/Response.cs` + nested `Device` record (id, name, created, lastSeen)
  - `RemoveDeviceParameters.cs`
  - `UpdateDeviceParameters.cs` (account, deviceId, deviceName)
- [ ] 5.2 Register у `SignalJsonContext` + `List<Device>` wrapper.
- [ ] 5.3 `ISignalDevices` — 4 нові методи з XMLDoc що clearly distinguish primary-perspective methods (`AddDevice`/`ListDevices`/`RemoveDevice`/`UpdateDevice`) vs existing secondary-perspective (`StartLink`/`FinishLink`).
- [ ] 5.4 `SignalDevices.cs` — implementations.
- [ ] 5.5 `SignalDevicesLog.cs` — +12 `[LoggerMessage]` у block 500-549.
- [ ] 5.6 Serialization + service tests (~12 unit).
- [ ] 5.7 E2E: `SignalCliE2EDevicesTests.cs.ListDevices_ReturnsAtLeastSelf` — read-only.
- [ ] 5.8 Update `SignalCli.public-api.txt` baseline.
- [ ] 5.9 Build + test (count ~396 → ~408 unit + 3 E2E).
- [ ] 5.10 Bump 4.4.0 → 4.5.0 + CHANGELOG.
- [ ] 5.11 Commit `feat(4.5.0): device management — add/list/remove/update from primary perspective`, push, merge, tag.

## 6. Wave 6 — `account-lifecycle` *(4.6.0)* — **opt-in gated**

### 6.1 Options-pattern extension

- [ ] 6.1.1 `src/SignalCli/Models/SignalCliOptions.cs` — додати property:
  ```csharp
  public bool EnableDestructiveOperations { get; set; } = false;
  ```
  з XMLDoc-warning per design §1.1.
- [ ] 6.1.2 `SignalCliOptionsValidator.cs` — нічого не міняється (флаг — простий bool, без cross-field rules).
- [ ] 6.1.3 Test: `SignalCliOptionsTests.EnableDestructiveOperations_DefaultsToFalse`.

### 6.2 RPC methods + DTOs

- [ ] 6.2.1 `src/SignalCli/Models/Signal/Accounts/` — 16 нових DTOs (8 methods × 2):
  - `UpdateAccountParameters/Response.cs` + `UpdateAccountOptions` (Builder; DeviceName, DiscoverableByNumber, UnrestrictedUnidentifiedSender, NumberSharingMode)
  - `UpdateConfigurationParameters/Response.cs` + `UpdateConfigurationOptions` (4 nullable bool: ReadReceipts, UnidentifiedDeliveryIndicators, TypingIndicators, LinkPreviews)
  - `SetPinParameters/Response.cs`, `RemovePinParameters/Response.cs`
  - `UnregisterParameters/Response.cs` (поле `delete: bool`)
  - `DeleteLocalAccountDataParameters/Response.cs` (поле `ignoreRegistered: bool`)
  - `StartChangeNumberParameters/Response.cs` + `StartChangeNumberOptions` (NewNumber, VoiceVerification, Captcha)
  - `FinishChangeNumberParameters/Response.cs` + `FinishChangeNumberOptions` (NewNumber, VerificationCode, Pin)
- [ ] 6.2.2 Register у `SignalJsonContext`.
- [ ] 6.2.3 `ISignalAccounts` — додати 8 нових destructive методів.
- [ ] 6.2.4 `SignalAccounts.cs`:
  - Constructor: `_destructiveOpsEnabled = options.Value.EnableDestructiveOperations;` (read once per critical rule #10).
  - Helper: `private void EnsureDestructiveAllowed([CallerMemberName] string? method = null) { if (!_destructiveOpsEnabled) throw new InvalidOperationException(...); }`.
  - Each of 8 destructive methods calls `EnsureDestructiveAllowed()` first.
- [ ] 6.2.5 `SignalAccountsLog.cs` — +24 `[LoggerMessage]` у block 450-499 (existing range). Include нові log message `DestructiveOperationBlocked(string method)` що логується ПЕРЕД throw'ом у `EnsureDestructiveAllowed`.

### 6.3 Tests

- [ ] 6.3.1 Serialization tests для всіх 16 DTOs (~24 unit including options-builders).
- [ ] 6.3.2 Service tests з focus на gating:
  - `Tests/SignalCli.Tests/Services/Signal/SignalAccountsDestructiveGateTests.cs` — для кожного з 8 методів: `default options (EnableDestructiveOperations = false) → InvokeAsync throws InvalidOperationException with method name in message`.
  - Те ж саме з `EnableDestructiveOperations = true` → метод проходить до RPC layer (mock'ed).
  - 8 × 2 = 16 tests.
- [ ] 6.3.3 Builder tests для compound options-records (UpdateAccountOptions, UpdateConfigurationOptions, StartChangeNumberOptions, FinishChangeNumberOptions) — ~8 unit.
- [ ] 6.3.4 `R02` `EventIdBlockTests` уже covers SignalAccountsLog — nothing нового.
- [ ] 6.3.5 Update `SignalCli.public-api.txt` baseline (8 new method signatures + new `SignalCliOptions.EnableDestructiveOperations` property).

### 6.4 Documentation

- [ ] 6.4.1 README.md — додати section "Destructive operations" з warning + opt-in example.
- [ ] 6.4.2 CLAUDE.md "Critical rules" — додати rule #19: "destructive ops gated by SignalCliOptions.EnableDestructiveOperations; default false; UnregisterAsync/DeleteLocalAccountDataAsync/SetPinAsync/RemovePinAsync/UpdateAccountAsync/UpdateConfigurationAsync/StartChangeNumberAsync/FinishChangeNumberAsync MUST call `EnsureDestructiveAllowed()` first".
- [ ] 6.4.3 Add regression guard RG09 (`DestructiveOpsGatingTests`) — reflectively enumerate ISignalAccounts methods, identify destructive subset by attribute or naming convention, assert each method calls EnsureDestructiveAllowed before InvokeMethodAsync (via IL analysis or naming convention with marker attribute).

### 6.5 Release

- [ ] 6.5.1 Build + test (count ~408 → ~456 unit + 3 E2E).
- [ ] 6.5.2 Bump 4.5.0 → 4.6.0 + CHANGELOG з prominent **⚠ DESTRUCTIVE OPS** warning у consumer-first voice.
- [ ] 6.5.3 Commit `feat(4.6.0): account lifecycle — destructive ops behind EnableDestructiveOperations opt-in`. Push, merge, tag.

## 7. Wave 7 — `polls` + `messaging-power-user` *(4.7.0)*

- [ ] 7.1 Polls DTOs (6 new): `PollCreateOptions` (Builder; question, options array 2-10, allowMultipleVotes) + `SendPollCreateParameters/Response.cs`. Repeat для `PollVote`, `PollTerminate`.
- [ ] 7.2 Power-user DTOs (10 new): SendAdminDeleteParameters/Response + AdminDeleteOptions; SendPinMessageParameters/Response + PinMessageOptions; SendUnpinMessageParameters/Response + UnpinMessageOptions; SendMessageRequestResponseParameters/Response + MessageRequestResponseOptions + `MessageRequestResponseType` enum (Accept, Delete, Block, BlockAndDelete, Unblock); SendPaymentNotificationParameters/Response + PaymentNotificationOptions.
- [ ] 7.3 Register 16 DTOs у `SignalJsonContext`.
- [ ] 7.4 `ISignalMessage` — додати 8 нових методів (3 polls + 5 power-user).
- [ ] 7.5 `SignalMessage.cs` — implementations.
- [ ] 7.6 `SignalMessageLog.cs` — +24 `[LoggerMessage]` у block 400-449.
- [ ] 7.7 Tests (~24 unit + 8 builder tests).
- [ ] 7.8 Update `SignalCli.public-api.txt`.
- [ ] 7.9 Build + test (~456 → ~488 unit).
- [ ] 7.10 Bump 4.6.0 → 4.7.0 + CHANGELOG.
- [ ] 7.11 Commit `feat(4.7.0): polls + power-user messaging — sendPollCreate/Vote/Terminate, sendAdminDelete, sendPin/Unpin, sendMessageRequestResponse, sendPaymentNotification`, push, merge, tag.

## 8. Wave 8 — `utility-rpc` *(4.8.0)*

- [ ] 8.1 Нова папка `src/SignalCli/Models/Signal/Utility/` — 6 DTOs (3 methods × 2):
  - `GetUserStatusParameters/Response.cs` + `GetUserStatusOptions` (account, recipients array OR usernames array; mutually exclusive).
  - `SendContactsParameters/Response.cs` — empty params shape (just account).
  - `SubmitRateLimitChallengeParameters/Response.cs`.
- [ ] 8.2 Register у `SignalJsonContext`.
- [ ] 8.3 `ISignalAccounts` — додати 3 utility-методи (non-destructive — без gating).
- [ ] 8.4 `SignalAccounts.cs` — implementations.
- [ ] 8.5 `SignalAccountsLog.cs` — +9 `[LoggerMessage]` (still block 450-499).
- [ ] 8.6 Tests (~9 unit).
- [ ] 8.7 E2E: `SignalCliE2EUserStatusTests.cs.GetUserStatus_KnownRegistered_ReturnsTrue` — використовує номер з registered тестового акаунта (E2E baseline вже має).
- [ ] 8.8 Update `SignalCli.public-api.txt` baseline (last update).
- [ ] 8.9 Build + test (~488 → ~497 unit + 4 E2E).
- [ ] 8.10 Bump 4.7.0 → 4.8.0 + CHANGELOG.
- [ ] 8.11 Commit `feat(4.8.0): utility RPC — getUserStatus, submitRateLimitChallenge, sendContacts`, push, merge, tag.

## 9. Final cleanup

- [ ] 9.1 Перевірити що actual coverage: `grep -c '"' src/SignalCli/Services/Signal/*.cs | grep -E 'send|list|get|update|remove|add|trust|block|unblock|join|quit|set|unregister|submit|finish|start'` → ≥49 unique RPC method literals.
- [ ] 9.2 Update root CLAUDE.md "Implemented, merged, archived" — додати entry `signal-cli-api-coverage (4.1.0–4.8.0, archived YYYY-MM-DD)` з summary "raised JSON-RPC coverage 18% → 98%".
- [ ] 9.3 Update root CLAUDE.md "Audit baseline → Тестова база": **unit tests ≥ 495** (від 290), **E2E tests ≥ 6** (від 2).
- [ ] 9.4 `npx -y @fission-ai/openspec@latest archive signal-cli-api-coverage --yes --skip-specs` (after all waves merged).
- [ ] 9.5 Commit `chore(openspec): archive signal-cli-api-coverage → YYYY-MM-DD`. Push.

## 10. Risk register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Wave 6 destructive op accidentally fires у production без `EnableDestructiveOperations` | low (default `false`) | **catastrophic** (data loss) | Gate test 6.3.2 + RG09 regression guard + opt-in flag + XMLDoc warning + README warning + CLAUDE.md critical rule #19 |
| signal-cli wire-shape для нового method'у відрізняється від нашого DTO | medium (no schema upstream) | medium (runtime serialization fail) | Inline-literal JSON snapshots з real signal-cli `--verbose` capture (task 0.3) |
| EventId collision у `[LoggerMessage]` методах | low | medium (build fail) | `R02` EventIdBlockTests pin'ає blocks + per-wave reservation (600-649 contacts, 650-679 stickers, 680-699 resources) |
| Public API baseline drift не помічений | low | low (release with API surprise) | `R03` PublicApiSurfaceTests blocks build until baseline updated; task 1.3.5/2.7/3.9/4.7/5.8/6.3.5/7.8/8.8 explicit |
| Wave breaks existing 9 methods через cross-cutting change у Wave 1 (typed exceptions) | low | high | Existing tests covering 9 methods МАЮТЬ всі pass'ити; додати explicit regression test що `catch (JsonRpcException)` continues to catch all error codes (backward compat) |
| Test suite count drops between waves (flake від wall-clock-dependent test) | low | medium (false-pass) | CLAUDE.md rule #11 + audit-debt.md "wall-clock-independent suite"; всі нові tests з mocks (no FakeTimeProvider needed для serialization-only tests) |
| `account-lifecycle` destructive method'и створюють divergence у тестовому signal-cli account state між test runs | low (only fires при `EnableDestructiveOperations = true` + actual invoke) | low | mock-only testing — destructive methods НЕ викликаються against real signal-cli |
