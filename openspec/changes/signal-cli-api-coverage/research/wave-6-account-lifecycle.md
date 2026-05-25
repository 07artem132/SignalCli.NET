# Wave 6 — Account lifecycle (DESTRUCTIVE)

Research notes for 8 JSON-RPC methods that mutate or destroy account state.
Pinned commit: **`bda4e7fc`** ("Prepare next release", 2026-05-24, tag `v0.14.4.1`).

> **Destructive-operation gating.** All 8 methods in this wave perform
> irreversible state changes (server-side unregister, local data wipe, phone
> number change, registration-lock PIN modification, etc.). In the .NET wrapper
> each call site MUST be gated behind `SignalCliOptions.EnableDestructiveOperations`
> (default `false`); attempted invocation without the opt-in throws
> `InvalidOperationException` BEFORE the RPC is dispatched. See `tasks.md` Wave 6
> for the guard implementation pattern.

> **JSON-RPC parameter naming.** signal-cli's `JsonRpcNamespace`
> (`src/main/java/org/asamk/signal/commands/JsonRpcNamespace.java:13-43`) accepts
> EITHER dashed argparse names (`device-name`) OR their camelCase equivalent
> (`deviceName`) — the dispatcher calls `Util.dashSeparatedToCamelCaseString(dest)`
> as a fallback. The .NET wrapper SHALL emit camelCase (idiomatic JSON) which
> matches the Java field-name on the receive side.

---

### `updateAccount` — Wave 6

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/UpdateAccountCommand.java @ bda4e7fc`
- Manager API: `lib/src/main/java/org/asamk/signal/manager/Manager.java:105-110 @ bda4e7fc` (`updateAccountAttributes` signature)
- Response record: `UpdateAccountCommand.java:94-97` (private `JsonAccountResponse`)

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | yes (multi-account daemon) | — | E.164 phone number, supplied by the JSON-RPC envelope, not the command body |
| `device-name` | `deviceName` | `String` (nullable) | no | `null` | If non-null, updates the device name visible in linked-devices list |
| `unrestricted-unidentified-sender` | `unrestrictedUnidentifiedSender` | `Boolean` (nullable) | no | `null` (no change) | Allow anyone to send unidentified-sender messages |
| `discoverable-by-number` | `discoverableByNumber` | `Boolean` (nullable) | no | `null` (no change) | Whether account is discoverable by phone number |
| `number-sharing` | `numberSharing` | `Boolean` (nullable) | no | `null` (no change) | Whether Signal shares its phone number when sending |
| `username` | `username` | `String` (nullable) | no | `null` | New username (`UpdateAccountCommand.java:42-43` — mutually exclusive with `delete-username`) |
| `delete-username` | `deleteUsername` | `Boolean` | no | `false` | Delete the existing username (`UpdateAccountCommand.java:43-45` — `storeTrue` flag) |

**Result (response) wire shape:**

Returned as JSON only when `username` is set (i.e. when a new username was successfully assigned, `UpdateAccountCommand.java:70-76`). Otherwise the call returns an empty result object.

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| `username` | `username` | `String` (nullable) | `@JsonInclude(NON_NULL)` — omitted when null |
| `usernameLink` | `usernameLink` | `String` (nullable) | The `UsernameLinkUrl.getUrl()` string; `@JsonInclude(NON_NULL)` — omitted when no link generated |

**Validation rules** (UserErrorException throws):
- `Invalid username: <reason>` when `m.setUsername(username)` throws `InvalidUsernameException` (`UpdateAccountCommand.java:79-80`). Underlying validation is server-side / `InvalidUsernameException`-internal — signal-cli doesn't enforce a length/format rule.

**Error codes specific to this method:**
- `-1 UserError` — invalid username format (`UpdateAccountCommand.java:79-80`).
- `-3 IoError` — `m.updateAccountAttributes` IO failure (`UpdateAccountCommand.java:61`), `m.setUsername` IO failure (`:77-78`), or `m.deleteUsername` IO failure (`:88-89`).

**Side-effects:**
- **DESTRUCTIVE — gated by `EnableDestructiveOperations`.**
- Mutates **server-side** account attributes (unidentified-sender policy, discoverability, number-sharing) and device name.
- `username` operation: assigns or deletes the account username at the Signal server (visible to contacts).
- The operation is NOT atomic across the four sub-operations: attribute-update runs first, then username-set, then username-delete in sequence — a failure at step 2 leaves step 1's effect applied.

**Enum values used:**
- _(none — all parameters are primitives or nullable booleans. The `PhoneNumberSharingMode` enum in `lib/src/main/java/org/asamk/signal/manager/api/PhoneNumberSharingMode.java` exists in the Manager API but is NOT exposed via this command — the CLI uses a simple Boolean `numberSharing` flag instead. The enum is consumed internally only.)_

**Quirks / surprises:**
- `username` and `delete-username` are declared mutually exclusive in argparse (`UpdateAccountCommand.java:41-45`) but signal-cli does NOT enforce mutual exclusivity for JSON-RPC clients — both fields can be sent in the same request and `handleCommand` will execute set-then-delete (`:64-91`), leaving the account with no username. Wrapper SHOULD validate mutual exclusivity client-side and throw `ArgumentException` to mirror the argparse contract.
- Response shape differs by codepath: `{}` for attribute-only updates, `{username, usernameLink}` for username-set, no JSON output for `delete-username`. Wrapper deserializes as `UpdateAccountResponse?` with both fields nullable; tolerate empty-object response.

---

### `unregister` — Wave 6

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/UnregisterCommand.java @ bda4e7fc`
- Manager API: `lib/src/main/java/org/asamk/signal/manager/helper/AccountHelper.java:603-625 @ bda4e7fc` (`unregister()` and `deleteAccount()` implementations)

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | yes (multi-account daemon) | — | E.164 phone number from envelope |
| `delete-account` | `deleteAccount` | `Boolean` | no | `false` | If `true`, calls `m.deleteAccount()` instead of `m.unregister()` — irreversibly deletes the account from Signal servers (`UnregisterCommand.java:36-37`) |

**Result (response) wire shape:**

Empty result (`{}`) on success — no fields returned.

**Validation rules** (UserErrorException throws):
- _(none in `UnregisterCommand` itself — only `IOErrorException` wrapped via `:42`)_

**Error codes specific to this method:**
- `-3 IoError` — `clearFcmToken` / `deleteAccount` API failure or `enableRegistrationLock`-cleanup failure (`UnregisterCommand.java:41-42`).

**Side-effects:**
- **DESTRUCTIVE — gated by `EnableDestructiveOperations`.**
- **Default (`delete-account=false`)** — clears the FCM token (`AccountHelper.java:607`) and marks `account.setRegistered(false)`. Server-side: incoming messages to this number stop being delivered to this device; other devices on the same number continue to work. Local account data, identity keys, and contacts remain on disk.
- **With `delete-account=true`** — additionally removes the registration-lock PIN, then calls `dependencies.getAccountApi().deleteAccount()` (`AccountHelper.java:613-624`). Server-side: the entire account is removed from Signal's servers; the phone number becomes available for re-registration by anyone. Local data still remains — use `deleteLocalAccountData` to wipe it.

**Enum values used:**
- _(none)_

**Quirks / surprises:**
- The flag name `delete-account` is opt-in by `Arguments.storeTrue()` (`UnregisterCommand.java:26`) — sending `{"deleteAccount": false}` is identical to omitting the field.
- "Unregister" without `delete-account` is **REVERSIBLE** in principle (re-register via `register`/`verify`) — but the wrapper should treat it as destructive because re-registration regenerates identity keys and breaks all pre-existing secure sessions with contacts.
- After `delete-account=true`, the typical follow-up is `deleteLocalAccountData` to clean up the local SignalAccount directory.

---

### `deleteLocalAccountData` — Wave 6

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/DeleteLocalAccountDataCommand.java @ bda4e7fc`
- Manager API: `lib/src/main/java/org/asamk/signal/manager/RegistrationManager.java:27 @ bda4e7fc` (`deleteLocalAccountData()`)

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | yes (multi-account daemon) | — | E.164 phone number from envelope |
| `ignore-registered` | `ignoreRegistered` | `Boolean` | no | `false` | Delete local data even if the account is still registered on Signal servers (`DeleteLocalAccountDataCommand.java:32-34, 40`) |

**Result (response) wire shape:**

Empty result (`{}`) on success.

**Validation rules** (UserErrorException throws):
- `Not deleting account, it is still registered. Use --ignore-registered to delete it anyway.` — when `m.isRegistered()` is true AND `ignore-registered` is false or absent (`DeleteLocalAccountDataCommand.java:41-43`).

**Error codes specific to this method:**
- `-1 UserError` — account still registered without `ignore-registered` flag.
- `-3 IoError` — filesystem failure during local-data deletion (`DeleteLocalAccountDataCommand.java:47-48`).

**Side-effects:**
- **DESTRUCTIVE — gated by `EnableDestructiveOperations`. CANNOT BE UNDONE.**
- **Removes the entire local account directory** under `<config>/data/<account>/` (typically `~/.local/share/signal-cli/data/<phone>/`) including: SignalAccount serialized state, SQLite stores (`account.db`, `signal.db`), identity keys, pre-keys, signed pre-keys, message store, attachment cache, group store, recipient store, sender-key store, sticker cache, profile avatar cache.
- This wipes the local *device identity* — the consumer cannot re-establish secure sessions with previous contacts even after re-registration; the new identity will be flagged as `safety-number-changed` for every contact.
- Goes through `RegistrationManager`, not `Manager` — operates without an active manager session (this is a registration-side command).

**Enum values used:**
- _(none)_

**Quirks / surprises:**
- This command implements `JsonRpcRegistrationCommand<Map<String, Object>>` (`DeleteLocalAccountDataCommand.java:21`) — request body is a generic `Map`, not a typed record. The wrapper SHALL still send typed JSON `{"account": "...", "ignoreRegistered": true}`.
- The validation runs BEFORE the deletion (`isRegistered()` check at `:41`) — the `--ignore-registered` flag is the only way to delete data while the account is still registered server-side. Use case: account-recovery scenarios where the user wants to re-link from scratch.
- The error message refers to `--ignore-registered` (CLI flag form) literally — this string is brittle for parsing. Wrapper exposes a typed `AccountStillRegisteredException` derived from `JsonRpcException` (`-1 UserError` + `KnownCode == UserError`) and matches on `KnownCode`, not message text.

---

### `startChangeNumber` — Wave 6

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/StartChangeNumberCommand.java @ bda4e7fc`
- Manager API: `lib/src/main/java/org/asamk/signal/manager/Manager.java:138-143 @ bda4e7fc`

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | yes (multi-account daemon) | — | Current E.164 phone number from envelope |
| `number` | `number` | `String` | yes | — | The NEW phone number in E.164 format (`StartChangeNumberCommand.java:32`) |
| `voice` | `voice` | `Boolean` | no | `false` | If true, request voice verification instead of SMS (`:33-35`, argparse `storeTrue` flag) |
| `captcha` | `captcha` | `String` (nullable) | no | `null` | Captcha token, required only if a previous call failed with `CaptchaRequired` (`:36-37`) |

**Result (response) wire shape:**

Empty result (`{}`) on success — no fields returned. signal-cli sends the verification SMS/voice call as a side effect; the verification code arrives out-of-band on the new phone.

**Validation rules** (UserErrorException throws):
- `Failed to change number: <e.getMessage()>` — `NonNormalizedPhoneNumberException` when `number` is not valid E.164 (`StartChangeNumberCommand.java:58-59`).
- `Captcha required for verification …` — `CaptchaRequiredException` (message built by `CommandUtil.getCaptchaRequiredMessage(e, captcha != null)`; `:55-57`).
- `This command doesn't work on linked devices.` — `NotPrimaryDeviceException` (`:60-61`).
- `Failed to register: <reason>: Before requesting voice verification you need to request SMS verification and wait a minute.` — `VerificationMethodNotAvailableException` with `voice=true` (`:65-71`); without `voice`, the trailing clause is omitted.

**Error codes specific to this method:**
- `-1 UserError` — `NonNormalizedPhoneNumberException`, `CaptchaRequiredException`, `NotPrimaryDeviceException`, `VerificationMethodNotAvailableException`.
- `-3 IoError` — generic `IOException` (`StartChangeNumberCommand.java:62-64`).
- `-5 RateLimit` — `RateLimitException` (`:52-54`) → wrapper surfaces as `RateLimitException` via `JsonRpcErrorCode.RateLimit`.
- `-6 CaptchaRejected` — _not raised by this command directly_; `CaptchaRequiredException` is mapped to `-1 UserError`, not `-6`. (The `-6 CaptchaRejected` code is for `CaptchaRejectedException` which is a separate exception type used by `register`/`verify`, not by `startChangeNumber`.)

**Side-effects:**
- **DESTRUCTIVE — gated by `EnableDestructiveOperations`.**
- Begins a phone-number-change flow: signal-cli stores a pending-change session locally and triggers Signal server to send an SMS or voice verification to `number`.
- Must be followed by `finishChangeNumber` with the verification code received OOB.
- If `finishChangeNumber` is never called, the local pending-change state persists; calling `startChangeNumber` again overwrites it.
- The current Signal account stays active and reachable on its current number until `finishChangeNumber` succeeds — `startChangeNumber` alone does NOT switch the number.

**Enum values used:**
- _(none — `voice` is a Boolean, not an enum)_

**Quirks / surprises:**
- Argparse declares the new number as a positional argument named `number` (`StartChangeNumberCommand.java:32`), not as `--new-number` or `--number`. JSON-RPC clients send `{"number": "+1..."}`.
- `account` (envelope) is the OLD number; `number` (body) is the NEW number. Wrapper API SHALL name the body param `NewNumber` to disambiguate.
- The `captcha` param is bound to the failure-mode of a previous `CaptchaRequiredException` — clients normally call `startChangeNumber` without it, catch the captcha-required error, prompt the user to solve a captcha at `https://signalcaptchas.org/staging/registration/generate.html`, and retry with the resulting token.

---

### `finishChangeNumber` — Wave 6

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/FinishChangeNumberCommand.java @ bda4e7fc`
- Manager API: `lib/src/main/java/org/asamk/signal/manager/Manager.java:144-148 @ bda4e7fc`

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | yes (multi-account daemon) | — | Current E.164 phone number from envelope (OLD number) |
| `number` | `number` | `String` | yes | — | The NEW phone number in E.164 format (must match the `number` passed to `startChangeNumber`) — `FinishChangeNumberCommand.java:28` |
| `verification-code` | `verificationCode` | `String` | yes | — | The 6-digit code received via SMS/voice (`:29-31`) |
| `pin` | `pin` | `String` (nullable) | no | `null` | Registration-lock PIN, only if the new number has one set on Signal servers (`:32`) |

**Result (response) wire shape:**

Empty result (`{}`) on success.

**Validation rules** (UserErrorException throws):
- `Verification failed! This number is locked with a pin. Hours remaining until reset: <hours>\nUse '--pin PIN_CODE' to specify the registration lock PIN` — `PinLockedException` (`FinishChangeNumberCommand.java:47-52`; computes hours from `e.getTimeRemaining() / 1000 / 60 / 60`).
- `Verification failed! Invalid pin, tries remaining: <count>` — `IncorrectPinException` (`:52-53`).
- `Account is pin locked, but pin data has been deleted on the server.` — `PinLockMissingException` (`:54-55`).
- `This command doesn't work on linked devices.` — `NotPrimaryDeviceException` (`:56-57`).

**Error codes specific to this method:**
- `-1 UserError` — all four exceptions above (`PinLockedException`, `IncorrectPinException`, `PinLockMissingException`, `NotPrimaryDeviceException`).
- `-3 IoError` — generic `IOException` with formatted message `"Failed to change number: <msg> (<simpleClassName>)"` (`FinishChangeNumberCommand.java:58-60`).

**Side-effects:**
- **DESTRUCTIVE — gated by `EnableDestructiveOperations`.**
- Completes the number-change flow started by `startChangeNumber`: the account's identifying phone number is updated server-side and locally.
- After success, the OLD number is no longer associated with this account; the device must be addressed via the NEW number for subsequent RPC calls. The `account` parameter in the envelope changes from OLD to NEW for all future commands.
- All contacts will see a "this number has changed" notification on next interaction.

**Enum values used:**
- _(none)_

**Quirks / surprises:**
- Same naming convention as `startChangeNumber`: `account` envelope = OLD, `number` body = NEW.
- `pin` is the registration-lock PIN of the NEW number (which the user must have set previously via `setPin` on that number's account, or be in possession of). It is NOT the PIN of the current/OLD account.
- The error-message texts reference CLI flag names (`'--pin PIN_CODE'`) — see same caveat as `deleteLocalAccountData`: wrapper matches on typed exception subclass (or `JsonRpcErrorCode.UserError`), not message text.

---

### `updateConfiguration` — Wave 6

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/UpdateConfigurationCommand.java @ bda4e7fc`
- Manager API: `lib/src/main/java/org/asamk/signal/manager/Manager.java @ bda4e7fc` (`updateConfiguration(Configuration)`)
- Wire record: `lib/src/main/java/org/asamk/signal/manager/api/Configuration.java:7-12 @ bda4e7fc`

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | yes (multi-account daemon) | — | E.164 phone number from envelope |
| `read-receipts` | `readReceipts` | `Boolean` (nullable) | no | `null` (no change) | Whether to send read receipts (`UpdateConfigurationCommand.java:25-27`) |
| `unidentified-delivery-indicators` | `unidentifiedDeliveryIndicators` | `Boolean` (nullable) | no | `null` (no change) | Show unidentified-delivery indicators in UI (`:28-30`) |
| `typing-indicators` | `typingIndicators` | `Boolean` (nullable) | no | `null` (no change) | Send/show typing indicators (`:31-33`) |
| `link-previews` | `linkPreviews` | `Boolean` (nullable) | no | `null` (no change) | Auto-generate link previews when sending URLs (`:34-36`) |

The four fields map 1:1 to `Configuration` record components (`Configuration.java:7-12`) wrapped in `Optional<Boolean>` — passing `null`/omitted leaves the corresponding setting unchanged.

**Result (response) wire shape:**

Empty result (`{}`) on success.

**Validation rules** (UserErrorException throws):
- `This command doesn't work on linked devices.` — `NotPrimaryDeviceException` (`UpdateConfigurationCommand.java:54-55`).

**Error codes specific to this method:**
- `-1 UserError` — `NotPrimaryDeviceException`.
- No `IOException` wrapping in the command body — Manager-side IO failures (if any) propagate as `RuntimeException`/`-32603 InternalError` instead.

**Side-effects:**
- **DESTRUCTIVE — gated by `EnableDestructiveOperations`.** (Marked destructive because it mutates synced account settings that propagate to all linked devices.)
- Updates the four configuration flags on the primary device and **syncs them to all linked devices** via a `SyncMessage` (this is the docstring "sync them to linked devices" from `:24`).
- Affects how Signal behaves at the per-message level for ALL subsequent message I/O — e.g. disabling `read-receipts` means the user no longer informs senders that messages have been read.

**Enum values used:**
- _(none — all four fields are `Optional<Boolean>` per the `Configuration` record)_

**Quirks / surprises:**
- Field names in the `Configuration` Java record (`readReceipts`, `unidentifiedDeliveryIndicators`, `typingIndicators`, `linkPreviews`) match camelCase JSON exactly when sent to JSON-RPC. The wrapper exposes them under the same names in `UpdateConfigurationParameters`.
- The "do nothing" sentinel is `Optional.empty()` on the Java side, which corresponds to JSON `null` or field-absence on the wire. The wrapper SHALL use C# nullable `bool?` and omit `null` fields via `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` to keep request payloads small.
- This is the only destructive command in the wave that does NOT call a `RegistrationManager` method — it's a pure Manager-API mutation.

---

### `setPin` — Wave 6

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/SetPinCommand.java @ bda4e7fc`
- Manager API: `lib/src/main/java/org/asamk/signal/manager/Manager.java:167 @ bda4e7fc` (`setRegistrationLockPin(Optional<String>)`)
- Implementation: `lib/src/main/java/org/asamk/signal/manager/internal/ManagerImpl.java:546-555` → `AccountHelper.setRegistrationPin` (`AccountHelper.java:584-593`) → `PinHelper.setRegistrationLockPin` (`PinHelper.java:24`)

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | yes (multi-account daemon) | — | E.164 phone number from envelope |
| `pin` | `pin` | `String` | yes | — | The new registration-lock PIN (`SetPinCommand.java:26-28`). Positional argparse argument, no flag prefix. |

**Result (response) wire shape:**

Empty result (`{}`) on success.

**Validation rules** (UserErrorException throws):
- `This command doesn't work on linked devices.` — `NotPrimaryDeviceException` (`SetPinCommand.java:41-42`).
- **No length validation in signal-cli's Java code.** `SetPinCommand` passes the raw string through to `m.setRegistrationLockPin(Optional.of(pin))` → `AccountHelper.setRegistrationPin` → `PinHelper.setRegistrationLockPin` → `secureValueRecovery.setPin(pin, masterKey)`. The actual length/strength enforcement happens at Signal's SVR (Secure Value Recovery) server, which rejects PINs shorter than 4 characters with a server-side error returned as `IOException`. The wrapper SHOULD enforce `pin.Length >= 4` client-side to fail fast with a clearer `ArgumentException`, but treat any client-side enforcement as advisory only — signal-cli itself does not enforce it. (Signal's documented PIN constraint: 4–20 characters numeric, OR 4+ characters alphanumeric.)

**Error codes specific to this method:**
- `-1 UserError` — `NotPrimaryDeviceException`.
- `-3 IoError` — wrapped `IOException` from PIN-helper / SVR-API failure (`SetPinCommand.java:39-40`).

**Side-effects:**
- **DESTRUCTIVE — gated by `EnableDestructiveOperations`.**
- Sets a Signal Registration Lock PIN that protects this account against unauthorized re-registration (`SetPinCommand.java:25` docstring).
- Stores the PIN via Signal's Secure Value Recovery (SVR) infrastructure — `PinHelper.setRegistrationLockPin` iterates over the SVR backends and writes the masked PIN+masterKey blob.
- Enables registration lock at the account-API level (`AccountHelper.java:588-589`) — subsequent re-registrations of this number from a different device will require the PIN.
- Updates the local account state to remember the PIN (`AccountHelper.java:591`) and triggers `updateAccountAttributes()` to sync.
- PIN automatically resets after 7 days of inactivity (per `SetPinCommand.java:27` docstring).

**Enum values used:**
- _(none)_

**Quirks / surprises:**
- The argparse arg is positional and named `pin` (no `--pin` flag prefix; `SetPinCommand.java:26`). JSON-RPC body: `{"pin": "1234"}`.
- No null-or-empty check in `SetPinCommand` — passing `{"pin": ""}` propagates to SVR with an empty string and may produce an opaque IO error. Wrapper SHALL reject empty/whitespace client-side.
- The signal-cli code does NOT distinguish "set a new PIN" from "update existing PIN" — same code path; existing PIN is silently overwritten.

---

### `removePin` — Wave 6

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/RemovePinCommand.java @ bda4e7fc`
- Manager API: `lib/src/main/java/org/asamk/signal/manager/Manager.java:167 @ bda4e7fc` (same `setRegistrationLockPin(Optional<String>)` — empty Optional means remove)
- Implementation: `lib/src/main/java/org/asamk/signal/manager/internal/ManagerImpl.java:552-554` → `AccountHelper.removeRegistrationPin` (`AccountHelper.java:595-601`)

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | yes (multi-account daemon) | — | E.164 phone number from envelope |

No other parameters — `RemovePinCommand.attachToSubparser` declares no arguments beyond the implicit `--account` envelope.

**Result (response) wire shape:**

Empty result (`{}`) on success.

**Validation rules** (UserErrorException throws):
- `This command doesn't work on linked devices.` — `NotPrimaryDeviceException` (`RemovePinCommand.java:38-39`).

**Error codes specific to this method:**
- `-1 UserError` — `NotPrimaryDeviceException`.
- `-3 IoError` — wrapped `IOException` from `PinHelper.removeRegistrationLockPin` / `AccountApi.disableRegistrationLock` (`RemovePinCommand.java:36-37`).

**Side-effects:**
- **DESTRUCTIVE — gated by `EnableDestructiveOperations`.**
- Removes the registration-lock PIN from Signal's SVR (`PinHelper.removeRegistrationLockPin`) and disables registration lock at the account API (`AccountHelper.java:597-598`).
- Clears the local PIN cache (`account.setRegistrationLockPin(null)`, `AccountHelper.java:600`).
- After this call, the account can be re-registered from any device WITHOUT a PIN — significantly weakens account security. The `EnableDestructiveOperations` gating is justified on these grounds, even though it's a "removal" operation.

**Enum values used:**
- _(none)_

**Quirks / surprises:**
- `setPin` and `removePin` are TWO SEPARATE JSON-RPC methods in the wrapper despite sharing one Java backend (`setRegistrationLockPin(Optional<String>)` with present/empty Optional). This is the established convention in signal-cli's JSON-RPC surface.
- `removePin` takes NO body parameters — wrapper sends `{}` (or `{"account": "..."}` from envelope only).
- Calling `removePin` when no PIN was previously set is a no-op at signal-cli level (idempotent) — the SVR remove call returns success, and `disableRegistrationLock` returns success even if it was already disabled.

---

## Verification

Java files read (all under `C:/Users/ivank/Нова папка/signal-cli/` @ commit `bda4e7fc`):

1. `src/main/java/org/asamk/signal/commands/UpdateAccountCommand.java` (98 lines).
2. `src/main/java/org/asamk/signal/commands/UnregisterCommand.java` (45 lines).
3. `src/main/java/org/asamk/signal/commands/DeleteLocalAccountDataCommand.java` (71 lines).
4. `src/main/java/org/asamk/signal/commands/StartChangeNumberCommand.java` (73 lines).
5. `src/main/java/org/asamk/signal/commands/FinishChangeNumberCommand.java` (63 lines).
6. `src/main/java/org/asamk/signal/commands/UpdateConfigurationCommand.java` (58 lines).
7. `src/main/java/org/asamk/signal/commands/SetPinCommand.java` (45 lines).
8. `src/main/java/org/asamk/signal/commands/RemovePinCommand.java` (42 lines).
9. `src/main/java/org/asamk/signal/commands/JsonRpcNamespace.java` (43 lines) — camelCase ↔ dashed conversion convention.
10. `lib/src/main/java/org/asamk/signal/manager/api/PhoneNumberSharingMode.java` (19 lines) — confirmed enum exists but is NOT exposed on `updateAccount` (which takes a Boolean `numberSharing` instead).
11. `lib/src/main/java/org/asamk/signal/manager/api/Configuration.java` (21 lines) — 4-tuple `Optional<Boolean>` record consumed by `updateConfiguration`.
12. `lib/src/main/java/org/asamk/signal/manager/Manager.java` lines 100–168 (focused on `updateAccountAttributes`, `setRegistrationLockPin`, `startChangeNumber`, `finishChangeNumber`, `setUsername`/`deleteUsername`/`getUsername`/`getUsernameLink` signatures).
13. `lib/src/main/java/org/asamk/signal/manager/RegistrationManager.java` lines 1–30 — confirms `deleteLocalAccountData()` is a registration-side API on `RegistrationManager`.
14. `lib/src/main/java/org/asamk/signal/manager/internal/ManagerImpl.java` lines 540–570 — `setRegistrationLockPin` implementation (primary-device check + dispatch to set-or-remove).
15. `lib/src/main/java/org/asamk/signal/manager/helper/AccountHelper.java` lines 580–625 — `setRegistrationPin`, `removeRegistrationPin`, `unregister`, `deleteAccount` implementations.
16. `lib/src/main/java/org/asamk/signal/manager/helper/PinHelper.java` lines 20–60 — SVR-backend dispatch for set/remove PIN.
17. `src/main/java/org/asamk/signal/commands/exceptions/CommandException.java` — sealed hierarchy confirming `UserErrorException` / `IOErrorException` / `RateLimitErrorException` / `CaptchaRejectedErrorException` are the only `CommandException` subtypes thrown by Wave 6 commands.

No invented fields. Where signal-cli source did not enforce a rule (e.g. PIN length, username format), this is explicitly stated rather than fabricated.
