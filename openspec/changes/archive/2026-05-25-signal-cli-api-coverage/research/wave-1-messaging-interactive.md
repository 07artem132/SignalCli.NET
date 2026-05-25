# Wave 1 — messaging-interactive (research notes)

Pinned reference: signal-cli @ `bda4e7fc` (tag `v0.14.4.1` HEAD, "Prepare next release", 2026-05-24).
All citations carry `@ bda4e7fc` suffix per README convention.

## Cross-method conventions (apply to every method below)

Conventions extracted once so each per-method section can stay focused on *its* fields.

1. **JSON key convention** (`src/main/java/org/asamk/signal/commands/JsonRpcNamespace.java:13-43 @ bda4e7fc`).
   Every argparse `dest` (kebab-case by default — `target-author`, `note-to-self`, `group-id`, `target-timestamp`)
   accepts **either** the literal kebab-case key **or** its camelCase counterpart (`targetAuthor`,
   `noteToSelf`, `groupId`, `targetTimestamp`) on the wire. Conversion: `JsonRpcNamespace.get(String dest)`
   first tries the raw key, then `Util.dashSeparatedToCamelCaseString(dest)`
   (`src/main/java/org/asamk/signal/util/Util.java:36-39 @ bda4e7fc`).
2. **Plural-list fallback** (same file, lines 30-42). For list-typed args (`nargs("*")` or `nargs("+")`),
   `getList(dest)` falls back to `dest + "s"` if the singular key isn't present. So `recipient` accepts
   both `"recipient": [...]` and `"recipients": [...]`. **The C# wrapper's existing
   `SendMessageFullParameters` already uses singular `"recipient"` with `IEnumerable<string>` —
   match that pattern for consistency.**
3. **`account` param is consumed by the dispatcher** (`SignalJsonRpcCommandHandler.java:127-139 @ bda4e7fc`)
   before the command runs — it selects the `Manager` instance, then `params.remove("account")`. So
   `account` is a wire field on every JsonRpcLocalCommand but does NOT appear in the command's
   `attachToSubparser`. Required when there are multiple registered accounts; optional when only one.
4. **Result shape for every `send*`** is identical, produced by
   `SendMessageResultUtils.outputResult(JsonWriter writer, SendMessageResults results)`
   (`src/main/java/org/asamk/signal/util/SendMessageResultUtils.java:43-70 @ bda4e7fc`):
   `{ "timestamp": Long, "results": List<JsonSendMessageResult> }` written via
   `writer.write(Map.of("timestamp", ..., "results", ...))`.
5. **`JsonSendMessageResult` wire shape** (`src/main/java/org/asamk/signal/json/JsonSendMessageResult.java:10-49 @ bda4e7fc`):
   - `recipientAddress` — `JsonRecipientAddress { uuid, number, username }`
     (`src/main/java/org/asamk/signal/json/JsonRecipientAddress.java:10 @ bda4e7fc`); every field is
     nullable String.
   - `groupId` — base64 String, `@JsonInclude(NON_NULL)` (omitted when not group-targeted).
   - `type` — enum `SUCCESS | NETWORK_FAILURE | UNREGISTERED_FAILURE | IDENTITY_FAILURE |
     RATE_LIMIT_FAILURE | INVALID_PRE_KEY_FAILURE` (UPPER_SNAKE in JSON; map to PascalCase in .NET).
   - `token` — String, `@JsonInclude(NON_NULL)`; populated only on ProofRequired (CAPTCHA) failure.
   - `retryAfterSeconds` — Long, `@JsonInclude(NON_NULL)`; populated only on rate-limit failure
     (`Math.ceilDiv(rateLimitRetryAfterMilliseconds, 1000L)`).
6. **`SendMessageResults`** (`lib/src/main/java/org/asamk/signal/manager/api/SendMessageResults.java:7 @ bda4e7fc`):
   `record SendMessageResults(long timestamp, Map<RecipientIdentifier, List<SendMessageResult>> results)`.
7. **Post-write failure-promotion** (`SendMessageResultUtils.java:58-69 @ bda4e7fc`): if no recipient succeeded,
   `outputResult` throws — and `SignalJsonRpcCommandHandler.runCommand` (lines 248-273) catches it as the
   following JSON-RPC error codes:
   - `hasOnlyUntrustedIdentity()` → throws `UntrustedKeyErrorException` → JSON-RPC code **`-4`** (UNTRUSTED_KEY_ERROR).
   - `hasOnlyRateLimitFailure()` → throws `RateLimitErrorException` → JSON-RPC code **`-5`** (RATELIMIT_ERROR).
   - else (mixed / network / unregistered / network) → throws `UserErrorException` → JSON-RPC code **`-1`** (USER_ERROR).
   **Important quirk:** even when error is thrown, `error.data` carries `{ "response": <the per-recipient result map you'd
   have got on success> }` (`SignalJsonRpcCommandHandler.java:285-290 @ bda4e7fc`) — so consumers MAY inspect
   partial results inside the error payload. Already known to .NET wrapper as `JsonRpcException.Data`.

8. **Account-selection error path** (`SignalJsonRpcCommandHandler.java:101-119 @ bda4e7fc`): if `account`
   param is missing/invalid in a multi-account daemon, JSON-RPC error code **`-32602` `INVALID_PARAMS`**
   with message `"Method requires valid account parameter"` or `"Specified account does not exist"`.
   This is shared across ALL `JsonRpcLocalCommand` methods, including all 4 below; not repeated per-method.

---

### `sendReaction` — Wave 1

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/SendReactionCommand.java @ bda4e7fc`
- Manager API: `lib/src/main/java/org/asamk/signal/manager/Manager.java:221-229 @ bda4e7fc`
- Recipient resolution: `src/main/java/org/asamk/signal/util/CommandUtil.java:25-51 @ bda4e7fc`
- Result wire shape: `src/main/java/org/asamk/signal/util/SendMessageResultUtils.java:43-70 @ bda4e7fc`
  + `src/main/java/org/asamk/signal/json/JsonSendMessageResult.java @ bda4e7fc`

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| Account | `account` | `String` | conditional | — | Required iff multi-account daemon. Dispatched at `SignalJsonRpcCommandHandler.java:127-139 @ bda4e7fc`; not in `attachToSubparser`. |
| Recipients | `recipient` (or `recipients`) | `List<String>` | one-of family required | `null` | Argparse positional `nargs("*")`; phone numbers / UUIDs. `SendReactionCommand.java:34 @ bda4e7fc`. Resolved via `CommandUtil.getSingleRecipientIdentifier` (`CommandUtil.java:100-109 @ bda4e7fc`). |
| GroupIds | `group-id` (or `groupId`, `group`) | `List<String>` | one-of family required | `null` | Argparse `-g`/`--group-id`/`--group` `nargs("*")`. Base64 group ids. `SendReactionCommand.java:33 @ bda4e7fc`. Resolved via `CommandUtil.getGroupIdentifiers` (`CommandUtil.java:53-62 @ bda4e7fc`). |
| Usernames | `username` (or `usernames`, `u`) | `List<String>` | one-of family required | `null` | Argparse `-u`/`--username` `nargs("*")`. `SendReactionCommand.java:35 @ bda4e7fc`. |
| NoteToSelf | `note-to-self` (or `noteToSelf`) | `Boolean` | optional | `false` | `Arguments.storeTrue()`. `SendReactionCommand.java:36 @ bda4e7fc`. When `true`, adds `RecipientIdentifier.NoteToSelf` to recipient set (`CommandUtil.java:33-35 @ bda4e7fc`). |
| NotifySelf | `notify-self` (or `notifySelf`) | `Boolean` | optional | `false` | `Arguments.storeTrue()`. Controls whether a copy sent to self is regular message vs. sync message. `SendReactionCommand.java:37-39 @ bda4e7fc`. Forwarded to Manager as `notifySelf` boolean. |
| Emoji | `emoji` (or `e`) | `String` | **yes** | — | `required(true)`. **Single Unicode grapheme cluster** per help text. `SendReactionCommand.java:41-43 @ bda4e7fc`. |
| TargetAuthor | `target-author` (or `targetAuthor`, `a`) | `String` | **yes** | — | `required(true)`. Phone number / UUID of the author of the message being reacted to. `SendReactionCommand.java:44-46 @ bda4e7fc`. **Quirk:** if `null` AND there's exactly one recipient AND it's a `Single`, that single recipient is reused as targetAuthor (`SendReactionCommand.java:82-88 @ bda4e7fc`). |
| TargetTimestamp | `target-timestamp` (or `targetTimestamp`, `t`) | `Long` | **yes** | — | `required(true)`, `.type(long.class)`. Send-timestamp of the original message. `SendReactionCommand.java:47-50 @ bda4e7fc`. |
| Remove | `remove` (or `r`) | `Boolean` | optional | `false` | `Arguments.storeTrue()`. Set to `true` to remove a prior reaction (matched on author+timestamp). `SendReactionCommand.java:51 @ bda4e7fc`. |
| Story | `story` | `Boolean` | optional | `false` | `Arguments.storeTrue()`. React to a Story instead of a normal message. `SendReactionCommand.java:52-54 @ bda4e7fc`. |

**Result (response) wire shape:**

`{ "timestamp": Long, "results": List<JsonSendMessageResult> }` (see Cross-method conventions §4-§5).

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| Timestamp | `timestamp` | `long` | Send timestamp (epoch ms) of the reaction message itself, NOT `targetTimestamp`. |
| Results | `results` | `List<JsonSendMessageResult>` | One per resolved recipient. See §5 for entry shape. |

**Validation rules** (UserErrorException throws):
- `"No recipients given"` коли всі чотири recipient sources (`recipient`, `group-id`, `username`,
  `note-to-self`) пусті/null (`CommandUtil.java:47-49 @ bda4e7fc`).
- `"Invalid phone number '" + recipientString + "': " + e.getMessage()` коли recipient string не
  парситься (`CommandUtil.java:106-108 @ bda4e7fc`, через `RecipientIdentifier.Single.fromString`).
- `"Invalid group id: " + e.getMessage()` коли base64 group id невалідний
  (`CommandUtil.java:82 @ bda4e7fc`).
- `"The user " + e.getSender().getIdentifier() + " is not registered."` коли author/recipient
  is unregistered Signal user — `UnregisteredRecipientException` catch
  (`SendReactionCommand.java:103-104 @ bda4e7fc`).
- Group-related: messages from `GroupNotFoundException` / `NotAGroupMemberException` /
  `GroupSendingNotAllowedException` re-thrown verbatim as UserError
  (`SendReactionCommand.java:98-99 @ bda4e7fc`).

**Error codes specific to this method:**
- `-1 UserError` — recipient/group validation, unregistered user, group state errors.
- `-3 IoError` — `IOException` from network / storage during send
  (`SendReactionCommand.java:100-102 @ bda4e7fc` throws `UnexpectedErrorException` → maps to
  INTERNAL_ERROR -32603, NOT -3; see Quirk below).
- `-4 UntrustedIdentity` — promoted by `outputResult` when ALL recipients fail with identity-key
  mismatch (Cross-method §7).
- `-5 RateLimit` — promoted by `outputResult` when ALL recipients fail with server rate-limit
  (Cross-method §7).
- `-32602 INVALID_PARAMS` — account-resolution failure (Cross-method §8).

**Side-effects:**
- Sends a Signal `DataMessage` with `reaction` field set to (`emoji`, `targetAuthor`, `targetTimestamp`,
  `remove`, `isStory`) to each recipient. Network-side mutation (not local state). Sync message goes to
  linked devices too when `notifySelf=true` and self is among recipients.
- Read-only on local store (no `Contact` / `Group` updates).
- Does NOT trigger inbound notifications on local subscribe stream — reactions are sent, not received,
  by this RPC.

**Enum values used:**
- `SendMessageResult.Type` (from `JsonSendMessageResult.Type` enum,
  `src/main/java/org/asamk/signal/json/JsonSendMessageResult.java:41-48 @ bda4e7fc`):
  `SUCCESS | NETWORK_FAILURE | UNREGISTERED_FAILURE | IDENTITY_FAILURE | RATE_LIMIT_FAILURE |
  INVALID_PRE_KEY_FAILURE` (UPPER_SNAKE on wire; PascalCase `Success | NetworkFailure | …` in .NET).

**Quirks / surprises:**
- **`IOException` → `UnexpectedErrorException` (not `IOErrorException`).**
  `SendReactionCommand.java:100-102 @ bda4e7fc` wraps `IOException` as `UnexpectedErrorException`,
  which the dispatcher maps to JSON-RPC code `-32603 INTERNAL_ERROR`
  (`SignalJsonRpcCommandHandler.java:267-272 @ bda4e7fc`). This is **inconsistent with `remoteDelete`
  (which uses the same `UnexpectedErrorException` path) and `sendReceipt` / `sendTyping` (which use
  `UserErrorException` for IOException, code `-1`)**. Document the discrepancy in the .NET wrapper
  XMLDoc; don't try to "normalize" it — that's an upstream choice.
- **`targetAuthor` is optional in the Java field-set sense but the CLI marks `required(true)`.** The
  Java code resolves `targetAuthor=null` to "the single recipient" when there's exactly one recipient
  that is a `Single` (`SendReactionCommand.java:82-88 @ bda4e7fc`). So over the JSON-RPC wire, sending
  no `target-author` works iff there's exactly one Single recipient — but `required(true)` means
  argparse would refuse the omission in CLI mode. Mirror the **JSON-RPC behavior** in .NET (treat as
  optional with conditional validation).
- **`emoji` must be a single grapheme cluster** per help text but `SendReactionCommand` doesn't
  validate this locally — signal-cli forwards to the Signal server as-is, and malformed reactions
  silently fail on remote clients. Document as caveat; don't add client-side validation.

---

### `sendReceipt` — Wave 1

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/SendReceiptCommand.java @ bda4e7fc`
- Manager API: `lib/src/main/java/org/asamk/signal/manager/Manager.java:200-202 @ bda4e7fc`
  (`sendReadReceipt` / `sendViewedReceipt`)
- Result wire shape: `src/main/java/org/asamk/signal/util/SendMessageResultUtils.java:43-70 @ bda4e7fc`

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| Account | `account` | `String` | conditional | — | Same dispatcher convention as Cross-method §3. |
| Recipient | `recipient` | `String` | **yes** | — | Argparse positional, `required(true)`. **Single recipient, NOT a list** — `SendReceiptCommand.java:25 @ bda4e7fc`. Resolved via `CommandUtil.getSingleRecipientIdentifier` (`SendReceiptCommand.java:42-43 @ bda4e7fc`). |
| TargetTimestamps | `target-timestamp` (or `targetTimestamp`, `t`) | `List<Long>` | **yes** | — | Argparse `-t`/`--target-timestamp` `nargs("+")` (one or more), `.type(long.class)`, `required(true)`. List of send-timestamps of messages being acknowledged. `SendReceiptCommand.java:26-30 @ bda4e7fc`. |
| Type | `type` | `String` | optional | `"read"` | Argparse `--type` with `.choices("read", "viewed")`. `SendReceiptCommand.java:31-33 @ bda4e7fc`. When `null` or `"read"`, calls `m.sendReadReceipt`; when `"viewed"`, calls `m.sendViewedReceipt`; any other value throws UserError. |

**Result (response) wire shape:**

`{ "timestamp": Long, "results": List<JsonSendMessageResult> }` (see Cross-method §4-§5).

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| Timestamp | `timestamp` | `long` | Send timestamp of the receipt message itself. |
| Results | `results` | `List<JsonSendMessageResult>` | One entry for the single recipient (always size 1 on success). |

**Validation rules** (UserErrorException throws):
- `"Unknown receipt type: " + type` коли `type` ∈ { не `null`, не `"read"`, не `"viewed"` }
  (`SendReceiptCommand.java:54 @ bda4e7fc`). Note: argparse `.choices(...)` would normally enforce
  this in CLI mode but JSON-RPC bypasses argparse, so the runtime check matters here.
- `"Invalid phone number '" + recipientString + "': " + e.getMessage()` коли recipient unparseable
  (`CommandUtil.java:106-108 @ bda4e7fc`).
- **No `"No recipients given"` here** — recipient is single + required, not a list.

**Error codes specific to this method:**
- `-1 UserError` — Unknown receipt type, invalid phone number, AND post-send promotion of
  mixed/network/unregistered failures (Cross-method §7).
- `-4 UntrustedIdentity` — promotion when single recipient fails with identity-key mismatch.
- `-5 RateLimit` — promotion when single recipient fails rate-limited.
- `-32602 INVALID_PARAMS` — account-resolution (Cross-method §8).
- **No `-3 IoError` path** — `sendReadReceipt` / `sendViewedReceipt` signatures don't declare
  `throws IOException` (`Manager.java:200-202 @ bda4e7fc`); the command class has no try/catch for
  IOException. Receipts are best-effort fire-and-log on transport-error; the post-send failure
  promotion in `outputResult` is the only error gate.

**Side-effects:**
- Sends a Signal `ReceiptMessage` (DELIVERY / READ / VIEWED variant) to the sender of the original
  messages. Network-side mutation only — no local DB writes.
- Does NOT mark messages as read locally (signal-cli has no local "read state"); this is a
  notification to the *remote sender* that you've read their message.
- Multi-timestamp single request: one `ReceiptMessage` with `timestamps[]` containing all
  `target-timestamp` values (signal-protocol level batching, not signal-cli-level).

**Enum values used:**
- `receipt type` string (NOT an enum on the wire; argparse `.choices("read", "viewed")`):
  `read | viewed` (lowercase on wire — `SendReceiptCommand.java:33 @ bda4e7fc`). In .NET model as
  an enum with `[EnumMember(Value = "read")]` / `[EnumMember(Value = "viewed")]` (lowercase to match)
  or a `string` typed property — prefer enum.
- `SendMessageResult.Type` per Cross-method §5.

**Quirks / surprises:**
- **`type` lowercase on the wire** breaks the Java-enum-to-UPPER_SNAKE convention (because it's not a
  Java enum at all — it's a string literal in `.choices(...)`). Don't auto-UPPER it in .NET.
- **`recipient` is singular String, not a list** (uniquely among the four messaging-interactive
  methods). `SendReceiptCommand.java:25 @ bda4e7fc` uses no `nargs(...)` — argparse defaults to
  single value. The `JsonRpcNamespace.getList` plural-`s` fallback still applies if a consumer
  accidentally sends `"recipients": [...]`, but the canonical wire shape is a single string.
- **`target-timestamp` is `nargs("+")`** meaning at least one value required — semantically a
  non-empty list. Argparse rejects empty list in CLI mode; JSON-RPC parse path will accept an
  empty array but then `sendReadReceipt(recipient, List.of())` is a no-op send (no
  `target-timestamp` to acknowledge) — document as caveat.
- **No group support.** `SendReceiptCommand` has no `--group-id` / `--note-to-self`. Receipts are
  inherently per-(sender,timestamp) tuples; group receipts are emitted automatically by the receiving
  device when reading a group message, not via this RPC.

---

### `sendTyping` — Wave 1

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/SendTypingCommand.java @ bda4e7fc`
- Manager API: `lib/src/main/java/org/asamk/signal/manager/Manager.java:195-198 @ bda4e7fc`
- `TypingAction` enum: `lib/src/main/java/org/asamk/signal/manager/api/TypingAction.java @ bda4e7fc`
- Result wire shape: `src/main/java/org/asamk/signal/util/SendMessageResultUtils.java:43-70 @ bda4e7fc`

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| Account | `account` | `String` | conditional | — | Cross-method §3. |
| Recipients | `recipient` (or `recipients`) | `List<String>` | one-of family required | `null` | Argparse positional `nargs("*")`. `SendTypingCommand.java:35 @ bda4e7fc`. |
| GroupIds | `group-id` (or `groupId`, `group`) | `List<String>` | one-of family required | `null` | Argparse `-g`/`--group-id`/`--group` `nargs("*")`. `SendTypingCommand.java:34 @ bda4e7fc`. |
| Stop | `stop` (or `s`) | `Boolean` | optional | `false` | Argparse `-s`/`--stop`, `Arguments.storeTrue()`. `false` → `TypingAction.START`; `true` → `TypingAction.STOP`. `SendTypingCommand.java:36, 47 @ bda4e7fc`. |

**Result (response) wire shape:**

`{ "timestamp": Long, "results": List<JsonSendMessageResult> }` (see Cross-method §4-§5).

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| Timestamp | `timestamp` | `long` | Send timestamp of the typing-indicator message. |
| Results | `results` | `List<JsonSendMessageResult>` | One per resolved recipient. |

**Validation rules** (UserErrorException throws):
- `"No recipients given"` коли `recipientIdentifiers.isEmpty()` після обробки `recipient` +
  `group-id` (`SendTypingCommand.java:58-60 @ bda4e7fc`). **NB:** this is a local check in
  `SendTypingCommand`, NOT delegated to `CommandUtil.getRecipientIdentifiers` (unlike
  `sendReaction` / `remoteDelete`).
- `"Invalid phone number '" + recipientString + "': " + e.getMessage()`
  (`CommandUtil.java:106-108 @ bda4e7fc`).
- `"Invalid group id: " + e.getMessage()` (`CommandUtil.java:82 @ bda4e7fc`).
- `"Failed to send message: " + e.getMessage() + " (" + e.getClass().getSimpleName() + ")"` —
  `IOException` wrapped as `UserErrorException` here (NOT `UnexpectedErrorException` —
  `SendTypingCommand.java:65-67 @ bda4e7fc`).
- `"Failed to send to group: " + e.getMessage()` for `GroupNotFoundException` /
  `NotAGroupMemberException` / `GroupSendingNotAllowedException`
  (`SendTypingCommand.java:68-70 @ bda4e7fc`).

**Error codes specific to this method:**
- `-1 UserError` — most error paths including IOException (see Quirks).
- `-4 UntrustedIdentity` — promoted on identity failure (Cross-method §7).
- `-5 RateLimit` — promoted on rate-limit (Cross-method §7).
- `-32602 INVALID_PARAMS` — account-resolution (Cross-method §8).
- **No `-3 IoError` path** — `IOException` is mapped to `-1 UserError` here, unlike `sendReaction`
  / `remoteDelete` where it becomes `-32603 INTERNAL_ERROR`.

**Side-effects:**
- Sends a Signal `TypingMessage` (START or STOP variant) to each recipient. Per command help text
  (`SendTypingCommand.java:32-33 @ bda4e7fc`): "Indicator will be shown for 15 seconds unless a typing
  STOP message is sent first." — so START messages auto-expire on receivers; explicit STOP optional.
- No local state change.
- No `--username` / `--note-to-self` support — typing indicators don't apply to self or username-only
  recipients. Wire-side, sending one would just be ignored (or fail parse if `JsonRpcNamespace`
  strict-checks unknown args — verify in implementation tests).
- Does NOT have `--notify-self` flag (unlike `sendReaction` / `send` / `remoteDelete`).

**Enum values used:**
- `TypingAction` (`lib/src/main/java/org/asamk/signal/manager/api/TypingAction.java:5-8 @ bda4e7fc`):
  `START | STOP`. **But on the wire there's NO `action` field** — instead, the boolean `stop` flag
  controls it (false=START, true=STOP). Don't expose `TypingAction` as a public .NET enum unless the
  wrapper translates internally; otherwise mirror the Java boolean shape as `bool Stop` and document
  the semantic.
- `SendMessageResult.Type` per Cross-method §5.

**Quirks / surprises:**
- **No `--username` argument** unlike `sendReaction` / `remoteDelete`. Typing indicators require a
  resolved Signal address; usernames-only recipients aren't supported. `SendTypingCommand.java:34-36
  @ bda4e7fc` lists only `--group-id` and positional `recipient`.
- **No `--note-to-self` argument** — you can't trigger a "typing" indicator on a thread with
  yourself. Wire-side, sending `noteToSelf=true` would be silently ignored (JsonRpcNamespace is
  permissive about unknown keys).
- **No `--notify-self`** — there's no "send to self because self is among recipients" toggle, because
  there's no self-targeting in the first place.
- **IOException → `-1 UserError`, not `-32603 INTERNAL_ERROR`.** This is inconsistent with
  `sendReaction` / `remoteDelete` but consistent with how `SendTypingCommand` was historically
  written. Document as upstream choice; don't normalize.
- **Boolean-as-action.** The wire uses `stop: true` to mean STOP — there is no `action: "START"`
  string field. C# wrapper SHOULD expose either `bool Stop` matching the wire, OR a `TypingAction`
  enum that translates internally. Prefer the latter for ergonomics.

---

### `remoteDelete` — Wave 1

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/RemoteDeleteCommand.java @ bda4e7fc`
- Manager API: `lib/src/main/java/org/asamk/signal/manager/Manager.java:216-219 @ bda4e7fc`
- Recipient resolution: `src/main/java/org/asamk/signal/util/CommandUtil.java:25-51 @ bda4e7fc`
- Result wire shape: `src/main/java/org/asamk/signal/util/SendMessageResultUtils.java:43-70 @ bda4e7fc`

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| Account | `account` | `String` | conditional | — | Cross-method §3. |
| TargetTimestamp | `target-timestamp` (or `targetTimestamp`, `t`) | `Long` | **yes** | — | `required(true)`, `.type(long.class)`. Send-timestamp of the message to delete. `RemoteDeleteCommand.java:31-34 @ bda4e7fc`. **Single value, not a list** (unlike `sendReceipt`). |
| GroupIds | `group-id` (or `groupId`, `group`) | `List<String>` | one-of family required | `null` | Argparse `-g`/`--group-id`/`--group` `nargs("*")`. `RemoteDeleteCommand.java:35 @ bda4e7fc`. |
| Recipients | `recipient` (or `recipients`) | `List<String>` | one-of family required | `null` | Argparse positional `nargs("*")`. `RemoteDeleteCommand.java:36 @ bda4e7fc`. |
| Usernames | `username` (or `usernames`, `u`) | `List<String>` | one-of family required | `null` | Argparse `-u`/`--username` `nargs("*")`. `RemoteDeleteCommand.java:37 @ bda4e7fc`. |
| NoteToSelf | `note-to-self` (or `noteToSelf`) | `Boolean` | optional | `false` | `Arguments.storeTrue()`. `RemoteDeleteCommand.java:38 @ bda4e7fc`. **No help text on this arg** — likely an oversight upstream; behaves like `sendReaction`'s `--note-to-self`. |

**Result (response) wire shape:**

`{ "timestamp": Long, "results": List<JsonSendMessageResult> }` (see Cross-method §4-§5).

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| Timestamp | `timestamp` | `long` | Send timestamp of the *delete* message itself, NOT `targetTimestamp`. |
| Results | `results` | `List<JsonSendMessageResult>` | One per resolved recipient. |

**Validation rules** (UserErrorException throws):
- `"No recipients given"` коли всі чотири recipient-sources пусті (`CommandUtil.java:47-49 @ bda4e7fc`).
- `"Invalid phone number '" + recipientString + "': " + e.getMessage()` (`CommandUtil.java:106-108 @ bda4e7fc`).
- `"Invalid group id: " + e.getMessage()` (`CommandUtil.java:82 @ bda4e7fc`).
- Group-related: messages from `GroupNotFoundException` / `NotAGroupMemberException` /
  `GroupSendingNotAllowedException` re-thrown verbatim
  (`RemoteDeleteCommand.java:63-64 @ bda4e7fc`).

**Error codes specific to this method:**
- `-1 UserError` — recipient validation, group state errors, post-send promotion of mixed/network
  failures (Cross-method §7).
- `-32603 INTERNAL_ERROR` — `IOException` wrapped as `UnexpectedErrorException`
  (`RemoteDeleteCommand.java:65-67 @ bda4e7fc`). Same pattern as `sendReaction`, differs from
  `sendTyping`.
- `-4 UntrustedIdentity` — promoted on identity failure.
- `-5 RateLimit` — promoted on rate-limit.
- `-32602 INVALID_PARAMS` — account-resolution (Cross-method §8).
- **No `UnregisteredRecipientException` catch** (unlike `sendReaction`) — `Manager.sendRemoteDeleteMessage`
  doesn't declare it (`Manager.java:216-219 @ bda4e7fc`), so unregistered recipients surface as part of
  `SendMessageResult` per-recipient failures (UNREGISTERED_FAILURE type in the per-recipient result),
  NOT as a top-level error.

**Side-effects:**
- Sends a Signal `DataMessage` with `delete` field set to (`targetSentTimestamp`) to each recipient.
  Receivers' Signal clients delete the matching message from their local view; this is a remote
  request — receivers MAY refuse (e.g., outside the time-window the protocol allows for deletes;
  signal-cli doesn't enforce this client-side).
- Server-side: sends one Signal protocol message per recipient (or one group message for group
  recipients).
- Local-store: does NOT delete the local copy of the message in signal-cli's own DB (consumers
  should track sent messages independently if they need that).
- **`--notify-self` is NOT a flag here** — there's no toggle for "send normal vs. sync when self is
  among recipients". Compare `sendReaction` (`SendReactionCommand.java:37-39 @ bda4e7fc`) which has
  `--notify-self`; `remoteDelete` doesn't. Wire-side, `notifySelf` is not consumed.

**Enum values used:**
- `SendMessageResult.Type` per Cross-method §5.
- No method-specific enums.

**Quirks / surprises:**
- **`target-timestamp` is a single `Long`, not a list.** Compare `sendReceipt`'s
  `target-timestamp` which is `nargs("+")`. The semantic differs: a remote-delete refers to one
  message; a receipt acknowledges potentially several at once.
- **`--note-to-self` has no help text in `attachToSubparser`** (`RemoteDeleteCommand.java:38 @ bda4e7fc`
  — bare `.action(Arguments.storeTrue())`). This is a minor upstream documentation oversight;
  behavior matches the same flag on `sendReaction`.
- **No `--notify-self`** — Document on the .NET wrapper: deletes targeting self always go as sync
  messages; you can't force them to be sent as a regular message.
- **`IOException` → `-32603 INTERNAL_ERROR`** (via `UnexpectedErrorException`) consistent with
  `sendReaction` but inconsistent with `sendTyping` (`-1`) and `sendReceipt` (no IOException path).
- **Unregistered recipients are NOT a top-level error.** Per-recipient `UNREGISTERED_FAILURE` shows
  up inside `results[].type`; if ALL fail unregistered, post-send promotion makes it `-1 UserError`
  with message `"Failed to send message"` (Cross-method §7's "else" branch).

---

## Verification

Files read for this wave (each fully or with cited line range):

- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/SendReactionCommand.java` (full)
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/SendReceiptCommand.java` (full)
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/SendTypingCommand.java` (full)
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/RemoteDeleteCommand.java` (full)
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/util/SendMessageResultUtils.java` (full)
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/json/JsonSendMessageResult.java` (full)
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/json/JsonRecipientAddress.java` (full)
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/api/SendMessageResults.java` (full)
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/api/TypingAction.java` (full)
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/util/CommandUtil.java` (full)
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/JsonRpcLocalCommand.java` (full)
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/JsonRpcNamespace.java` (full)
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/util/Util.java` (lines 36-46, for `dashSeparatedToCamelCaseString`)
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/Manager.java` (lines 195-229, for `sendTypingMessage` / `sendReadReceipt` / `sendViewedReceipt` / `sendRemoteDeleteMessage` / `sendMessageReaction` signatures)
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/jsonrpc/SignalJsonRpcCommandHandler.java` (lines 1-310, for error-code mapping + account dispatch)
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/SendCommand.java` (lines 1-100, for argparse naming-convention cross-reference)
