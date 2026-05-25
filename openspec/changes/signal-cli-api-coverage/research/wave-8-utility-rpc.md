# Wave 8 — utility-rpc research notes

Source-of-truth research for 3 JSON-RPC utility methods. Pinned commit: `bda4e7fc`
("Prepare next release", 2026-05-24; same baseline as
`.claude/rules/signal-cli-protocol.md`).

Per `tasks.md §0.5` anti-hallucination protocol — every cell below is
**read** from upstream Java source, not inferred. Empty cells with
"(not found — recheck)" markers indicate the source did not pin the
answer.

---

### `getUserStatus` — Wave 8

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/GetUserStatusCommand.java @ bda4e7fc` (full file, lines 1–126)
- Manager API surface: `lib/src/main/java/org/asamk/signal/manager/Manager.java:101` (`getUserStatus`) and `:103` (`getUsernameStatus`)
- Manager impl: `lib/src/main/java/org/asamk/signal/manager/internal/ManagerImpl.java:258-298` (`getUserStatus`), `:301-…` (`getUsernameStatus`)
- API records: `lib/src/main/java/org/asamk/signal/manager/api/UserStatus.java` (full file, 6 lines) and `lib/src/main/java/org/asamk/signal/manager/api/UsernameStatus.java` (full file, 6 lines)
- Wire response record (json): `GetUserStatusCommand.java:119-125` — private nested `record JsonUserStatus`
- JSON-RPC key-translation: `src/main/java/org/asamk/signal/commands/JsonRpcNamespace.java:30-42` (plural fallback `recipient` → `recipients`)
- Error-code mapping: `src/main/java/org/asamk/signal/jsonrpc/SignalJsonRpcCommandHandler.java:39-43, 248-272`

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| Recipients (phone numbers) | `recipient` OR `recipients` | `List<String>` | no | `null` → treated as empty `Set.of()` | argparse `nargs("*")` ⇒ either key accepted via `JsonRpcNamespace.getList` plural fallback (`dest + "s"`). E.164 phone numbers; `PhoneNumberFormatter.formatNumber(n, account.getNumber())` canonicalises with account's number as fallback country code (`ManagerImpl.java:261`). Unparseable numbers don't throw — they yield empty `number` in response. |
| Usernames | `username` OR `usernames` | `List<String>` | no | `null` → treated as empty `Map.of()` | argparse `--username … nargs("*")` ⇒ JSON key `username` (singular) accepted; plural fallback yields `usernames`. May be the bare username or a username-link URL — `RecipientHelper.resolveRecipientByUsernameOrLink(username, true)`. |

**Mutual exclusion?** No — both `recipient` and `username` arrays may be present simultaneously, and the response merges entries (`Stream.concat` at `GetUserStatusCommand.java:81-97`). Both empty ⇒ empty JSON array `[]`.

**Result (response) wire shape:**

Top-level: a JSON **array** of `JsonUserStatus` objects.

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| Recipient (input key) | `recipient` | `String` | The raw input string the caller supplied — phone for `recipient` entries, username/link for `username` entries. **Never null.** |
| Number | `number` | `String?` | Canonicalised E.164 phone. Present **only** for entries from `recipient` input AND when canonicalisation succeeded. `@JsonInclude(NON_NULL)` ⇒ omitted from JSON when null (e.g. all `username`-input rows omit `number`). Also omitted when input was unparseable (canonicalisation returned `""` → mapped to `null`). |
| Username | `username` | `String?` | Present **only** for entries from `username` input. `@JsonInclude(NON_NULL)` ⇒ omitted for phone-input rows. |
| UUID | `uuid` | `String?` (Java `UUID` → `.toString()`) | The Signal account UUID (registered ServiceId). `null` when the user is not registered. **No `@JsonInclude` annotation** — always emitted (as JSON `null` when not registered). |
| Is registered | `isRegistered` | `boolean` | `true` ⇔ `uuid != null` (`GetUserStatusCommand.java:88, 96`). Always present (primitive). |

**Field omitted from wire** (present on Java side, NOT serialised):
- `UserStatus.unrestrictedUnidentifiedAccess` (`boolean`) and `UsernameStatus.unrestrictedUnidentifiedAccess` exist on Java records (used only by `PlainTextWriter` for the `(unrestricted sealed sender)` suffix at `GetUserStatusCommand.java:106, 113`) — **NOT** part of `JsonUserStatus` ⇒ never appears in JSON-RPC response.

**Validation rules** (UserErrorException throws):

None. The command never throws `UserErrorException`. Both arrays may be empty; phone numbers that fail `InvalidNumberException` parsing are silently mapped to `number=null, uuid=null, isRegistered=false` (`ManagerImpl.java:266-268`).

**Error codes specific to this method:**
- `-3 IO_ERROR` — wrapped `IOException` from `getRegisteredUsers` or `getUsernameStatus` (`GetUserStatusCommand.java:58-64, 70-75`). Message format: `"Unable to check if users are registered: <ex.getMessage()> (<ex.getClass().getSimpleName()>)"`.
- `-5 RATELIMIT_ERROR` — `RateLimitException` thrown by `getUserStatus` (CDSI lookup throttled). Message via `CommandUtil.getRateLimitMessage(e)` — typically `"Rate limit"` + retry-after info. Constructed from `CdsiResourceExhaustedException.getRetryAfterSeconds() * 1000L` (`ManagerImpl.java:280-283`).
- `-32603 INTERNAL_ERROR` — any other `Throwable` (`SignalJsonRpcCommandHandler.java:274-279`).

**Side-effects:**
- **Read-only**. Issues CDSI lookup against Signal server (`RecipientHelper.getRegisteredUsers`) and username lookup (`resolveRecipientByUsernameOrLink`). For unknown users, may also fetch profile (`ProfileHelper.getRecipientProfile`) which writes to local profile cache — so technically "warms cache" but no user-observable state change. No notifications, no contact sync.

**Enum values used:**

None on the wire. `Profile.UnidentifiedAccessMode.UNRESTRICTED` is consulted internally (`ManagerImpl.java:296`) but the resulting boolean is dropped before JSON-RPC serialisation (see "Field omitted from wire" above).

**Quirks / surprises:**
- **Two input keys, two output discriminator fields.** A row's `number` field tells you it came from `recipient` input; `username` field tells you it came from `username` input. They're mutually exclusive **per row** but both inputs may produce rows in the same response array.
- **Plural-fallback key naming.** `JsonRpcNamespace.getList(dest)` tries `dest`, then `dest + "s"` (`JsonRpcNamespace.java:31-42`) — so wire accepts both singular and plural. CLI help text says `"recipient"` and `"--username"`, but both `recipient`/`recipients` AND `username`/`usernames` deserialise. Implementation SHOULD send the singular forms (matches CLI argparse `dest`) for forward-compat.
- **`isRegistered` is derived, not authoritative.** Java does `uuid != null` at construction (`GetUserStatusCommand.java:88, 96`). Consumers can rely on the equivalence; we can skip the field entirely and compute from `uuid` if it saves wire bytes — but keep it for symmetry with the upstream wire shape.
- **Empty input ⇒ empty array.** Calling `getUserStatus` with no `recipient` and no `username` returns `[]`, not an error. Tests should pin this.
- **`number` omitted vs. `null`.** Because of `@JsonInclude(NON_NULL)`, the JSON literally omits the `number` key for username-input rows AND for phone-input rows that failed canonicalisation. Same for `username`. `uuid` is NOT annotated, so it appears as JSON `null` when not registered. .NET DTO needs nullable property + `JsonIgnoreCondition.WhenWritingNull` for `number`/`username`, but plain `string?` for `uuid`.

---

### `submitRateLimitChallenge` — Wave 8

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/SubmitRateLimitChallengeCommand.java @ bda4e7fc` (full file, lines 1–48)
- Manager API: `lib/src/main/java/org/asamk/signal/manager/Manager.java:154-157` (`submitRateLimitRecaptchaChallenge(String challenge, String captcha)`)
- API exception: `lib/src/main/java/org/asamk/signal/manager/api/CaptchaRejectedException.java` (full file, 17 lines)
- Error-code mapping: `src/main/java/org/asamk/signal/jsonrpc/SignalJsonRpcCommandHandler.java:43` (`CAPTCHA_REJECTED_ERROR = -6`) + `:263-266`

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| Challenge token | `challenge` | `String` | **yes (effectively)** | — | argparse `--challenge` ⇒ JSON key `challenge`. CLI declares `required(true)` (`SubmitRateLimitChallengeCommand.java:27`), **but `required(true)` is NOT enforced in JSON-RPC mode** — `JsonRpcNamespace` skips argparse validation, so missing key ⇒ `ns.getString("challenge")` returns `null` ⇒ propagated to `m.submitRateLimitRecaptchaChallenge(null, null)` ⇒ likely NPE caught at `SignalJsonRpcCommandHandler.java:274-279` ⇒ `INTERNAL_ERROR` (-32603). Token comes from a prior `"proof required"` error payload (typically `error.data` of a rate-limited `send`). |
| Captcha token | `captcha` | `String` | **yes (effectively)** | — | argparse `--captcha` ⇒ JSON key `captcha`. Same NPE-fallthrough as above when missing. Captcha solved at <https://signalcaptchas.org/challenge/generate.html> (per the CLI help string + signal-cli docs). |

**Result (response) wire shape:**

The command writes nothing on success. Per `SignalJsonRpcCommandHandler.java:281` (`Object output = result[0] == null ? Map.of() : result[0];`), the wire response is the empty JSON object `{}`. **Not `null`, not absent.**

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| (no fields) | — | — | Response is literal `{}` on success. |

**Validation rules** (UserErrorException throws):

None at the command level — `SubmitRateLimitChallengeCommand` does not throw `UserErrorException`. argparse's `required(true)` flags are CLI-only.

**Error codes specific to this method:**
- `-3 IO_ERROR` — wrapped `IOException` from `submitRateLimitRecaptchaChallenge` (network failure, server 5xx). Message format: `"Submit challenge error: <ex.getMessage()>"` (`SubmitRateLimitChallengeCommand.java:42`).
- `-6 CAPTCHA_REJECTED_ERROR` — `CaptchaRejectedException` from the underlying API call (`SubmitRateLimitChallengeCommand.java:43-45`). Message: `"Captcha rejected, it may be outdated, already used or solved from a different IP address."`. **This is the canonical site for the `-6` error code in signal-cli** — already typed as `CaptchaRejected` in `JsonRpcErrorCode` enum on .NET side.
- `-32603 INTERNAL_ERROR` — any other `Throwable` (likely path for missing `challenge`/`captcha` since argparse `required` is bypassed).

**Side-effects:**
- **Mutates server-side rate-limit state for this account.** On success, lifts the proof-required rate limit so subsequent `send` calls proceed. No local state changed (no notifications, no contact sync, no file writes).

**Enum values used:**

None.

**Quirks / surprises:**
- **`required(true)` on argparse is a no-op for JSON-RPC.** CLI users get `argparse4j` validation; JSON-RPC users get a downstream NPE → `INTERNAL_ERROR`. .NET wrapper SHOULD validate `challenge` and `captcha` non-empty client-side and throw `ArgumentException` before the RPC call — matches our "Typed/idempotent state errors" rule (#14) and avoids the protocol's `-32603` opacity.
- **Empty `{}` success response.** Don't model the response as `void`; model it as `EmptyResponse` (or whatever the established convention is for command-no-output methods in `Models/Signal`) so the `JsonElement`-decode pipeline doesn't choke. The actual JSON is `{}`, not `null`.
- **Workflow context matters.** The `challenge` token is opaque and short-lived (Signal server-issued). It travels with the proof-required error payload from a failed `send` (typically `RateLimit` `-5` with `error.data` carrying the challenge ID). Document this in XMLDoc on the .NET surface — consumers will be confused without the workflow.
- **The Manager method is named `submitRateLimitRecaptchaChallenge` (with "Recaptcha"); the RPC method is `submitRateLimitChallenge` (without).** Historical naming — Signal moved from reCAPTCHA to hCaptcha for the front-end, but the API name was kept. Don't surface "Recaptcha" in .NET naming; mirror the RPC name.

---

### `sendContacts` — Wave 8

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/SendContactsCommand.java @ bda4e7fc` (full file, lines 1–38)
- Manager API: `lib/src/main/java/org/asamk/signal/manager/Manager.java:372` (`void sendContacts() throws IOException`)
- Manager impl: `lib/src/main/java/org/asamk/signal/manager/internal/ManagerImpl.java:1539-1540` (`context.getSyncHelper().sendContacts()`)
- Error-code mapping: `src/main/java/org/asamk/signal/jsonrpc/SignalJsonRpcCommandHandler.java:40` (`IO_ERROR = -3`) + `:253-255`

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| (no params) | — | — | — | — | The command has zero `addArgument` calls (`SendContactsCommand.java:21-23`). Only the framework-level `account` parameter (handled by `SignalJsonRpcCommandHandler.getManagerFromParams`) is consumed — not part of the command's own surface. |

**Result (response) wire shape:**

Empty JSON object `{}` on success (same mechanism as `submitRateLimitChallenge` — command writes nothing ⇒ handler returns `Map.of()` at `SignalJsonRpcCommandHandler.java:281`).

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| (no fields) | — | — | Response is literal `{}` on success. |

**Validation rules** (UserErrorException throws):

None. The command does not throw `UserErrorException`.

**Error codes specific to this method:**
- `-3 IO_ERROR` — wrapped `IOException` from `syncHelper.sendContacts()` (`SendContactsCommand.java:32-34`). Message format: `"SendContacts error: <ex.getMessage()>"`. Common cause: no linked devices, or transient network failure on the sync-message send.
- `-32603 INTERNAL_ERROR` — any other `Throwable`.

**Side-effects:**
- **Sends a Signal `SyncMessage.Contacts` to all linked devices.** This is a write — produces network traffic and delivers a sync message that other linked clients (Signal Desktop, Signal-iOS-as-linked-device, etc.) will receive and parse into their contact list. No local state change. The receiving side may emit a sync notification (`sync_message` with `contacts` payload) — but that's on the *other* devices, not this one.
- **No-op safety:** if the account has no linked devices, the sync still attempts to send and may succeed silently (sync messages target the multi-device fan-out, not specific peers). Confirmed via `SyncHelper.sendContacts()` — not inspected in detail since it's transitive (out of scope per `tasks.md` "command + supporting types only").

**Enum values used:**

None.

**Quirks / surprises:**
- **Empty params object on the wire.** Request `params` should be `{}` (or `{"account": "..."}` for multi-account installs — the `account` key is framework-level, handled before `parseParamsAndRunCommand`). Don't send `null` params; signal-cli's JsonRpc reader is fine with empty object but not with missing-params for an account-scoped command.
- **One-way sync.** This does NOT *fetch* contacts from other devices — it *pushes* the local contact list to them. Inverse operation (fetching) is `syncContacts` (currently exists in our .NET surface as `ISignalAccounts.SyncAccountAsync`-family). Document this clearly in XMLDoc — easy to confuse.
- **Empty `{}` success response.** Same as `submitRateLimitChallenge` — model as `EmptyResponse`, not `void`.
- **No way to verify completion server-side from this RPC.** The IO_ERROR fires only if the *outbound send* fails. Whether linked devices actually *received* and *processed* the sync is invisible from here. Consumers needing confirmation must subscribe to sync notifications on the linked side.

---

## Verification

Java files read during this research pass (all `@ bda4e7fc` baseline):

1. `src/main/java/org/asamk/signal/commands/GetUserStatusCommand.java` — full file (126 lines)
2. `src/main/java/org/asamk/signal/commands/SubmitRateLimitChallengeCommand.java` — full file (48 lines)
3. `src/main/java/org/asamk/signal/commands/SendContactsCommand.java` — full file (37 lines)
4. `src/main/java/org/asamk/signal/commands/JsonRpcLocalCommand.java` — full file (30 lines) — to confirm `Map<String, Object>` deserialisation contract for `JsonRpcLocalCommand` family
5. `src/main/java/org/asamk/signal/commands/JsonRpcNamespace.java` — full file (43 lines) — to confirm plural-fallback (`recipient` → `recipients`) + dash-to-camelCase rules
6. `src/main/java/org/asamk/signal/jsonrpc/SignalJsonRpcCommandHandler.java` — partial (lines 1–60, 228–311) — to confirm error-code constants (-1/-3/-4/-5/-6), empty-result handling (`Map.of()` ⇒ `{}` on wire), and Throwable fallthrough → `INTERNAL_ERROR`
7. `lib/src/main/java/org/asamk/signal/manager/Manager.java` — targeted: `:101, :103` (`getUserStatus`/`getUsernameStatus` signatures), `:154-157` (`submitRateLimitRecaptchaChallenge`), `:372` (`sendContacts`)
8. `lib/src/main/java/org/asamk/signal/manager/internal/ManagerImpl.java` — partial (lines 258–308, 1539–1540) — to confirm CDSI rate-limit mapping (`CdsiResourceExhaustedException` → `RateLimitException`), phone-number canonicalisation, and SyncHelper delegation
9. `lib/src/main/java/org/asamk/signal/manager/api/UserStatus.java` — full file (6 lines)
10. `lib/src/main/java/org/asamk/signal/manager/api/UsernameStatus.java` — full file (6 lines)
11. `lib/src/main/java/org/asamk/signal/manager/api/RateLimitException.java` — full file (19 lines) — to confirm `retryAfterMilliseconds` is the only payload
12. `lib/src/main/java/org/asamk/signal/manager/api/CaptchaRejectedException.java` — full file (17 lines)

All file paths absolute (Windows). Pinned commit `bda4e7fc` matches the `signal-cli-protocol.md` baseline — no drift check required per `README.md`.
