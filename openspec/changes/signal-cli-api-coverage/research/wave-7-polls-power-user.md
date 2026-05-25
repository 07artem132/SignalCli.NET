# Wave 7 — Polls + Power-user Messaging + Receive-side Decoders

Per `tasks.md §0.5` anti-hallucination protocol. Pinned reference: `bda4e7fc`
("Prepare next release", 2026-05-24) of `C:\Users\ivank\Нова папка\signal-cli`.

This wave covers **8 send-side RPC methods** and **7 receive-side wire records** (Jackson
records consumed when `subscribeReceive` delivers a `dataMessage` from another client).
The receive-side records flow through `JsonDataMessage` as nullable fields — `.NET` decoders
must mirror Java record component order + Jackson naming exactly.

---

## Send-side methods

### `sendPollCreate` — Wave 7

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/SendPollCreateCommand.java @ bda4e7fc` (lines 1-111)
- Manager API: `m.sendPollCreateMessage(question, allowMultiple, options, recipientIdentifiers, notifySelf)` (line 94)

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | yes | — | E.164 phone number (standard JsonRpcLocalCommand `account` param, injected by framework) |
| `recipient` | `recipient` | `List<String>` | no | `[]` | E.164 phone numbers (nargs="*") |
| `group-id` / `group` | `groupId` / `group` | `List<String>` | no | `[]` | Group IDs (nargs="*"); aliases `-g`, `--group-id`, `--group` |
| `username` | `username` | `List<String>` | no | `[]` | Usernames or username links (nargs="*") |
| `note-to-self` | `noteToSelf` | `boolean` | no | `false` | Send to self (storeTrue) |
| `notify-self` | `notifySelf` | `boolean` | no | `false` | If self is part of recipients, send normal message instead of sync (storeTrue) |
| `question` | `question` | `String` | **yes** | — | Poll question text (`-q`, `--question`) |
| `no-multi` | `noMulti` | `boolean` | no | `false` | If true, only one option may be selected. **Internal logic inverts**: `allowMultiple = !noMulti` (line 94, so default API behavior is multi-vote allowed) |
| `option` | `option` | `List<String>` | **yes** | — | Poll option strings (`-o`, `--option`, nargs="+") |

**Result (response) wire shape:**

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| `timestamp` | `timestamp` | `long` | Standard `outputResult` send-message envelope from `SendMessageResultUtils.outputResult` — same shape as other `send*` methods (timestamp + results list of per-recipient `JsonSendMessageResult`) |
| `results` | `results` | `List<JsonSendMessageResult>` | Per-recipient delivery status |

**Validation rules** (UserErrorException throws — lines 74-91):
- `"Poll needs at least two options"` when `options.size() < 2`
- `"Poll cannot have more than 10 options"` when `options.size() > MAX_POLL_OPTIONS` (constant `MAX_POLL_OPTIONS = 10` at line 27)
- `"Poll options must not be empty"` when any `option.isEmpty()`
- `"Poll option \"<option>\" exceeds the maximum length of 100 characters"` when `option.length() > MAX_POLL_OPTION_LENGTH` (constant `MAX_POLL_OPTION_LENGTH = 100` at line 28)
- `"The user <identifier> is not registered."` on `UnregisteredRecipientException`
- Group exceptions (`GroupNotFoundException`, `NotAGroupMemberException`, `GroupSendingNotAllowedException`) propagate their messages

**Error codes specific to this method:**
- `-1 UserError` — see Validation rules
- `-3 IoError` — wrapped IOException → "Failed to send message: ... (IOException), maybe one of the devices of the recipient wasn't online for a while." (when message contains "No prekeys available") or generic "Failed to send message: ... (<ClassName>)"
- `-4 UntrustedIdentity` — possible during send (standard cross-cutting code, not handled in this command but at lower layers)
- `0` (no specific code, generic `UnexpectedErrorException`) for non-prekey IOExceptions

**Side-effects:**
- Send-side write: creates a new poll on the wire, delivers PollCreate `dataMessage` to recipients.
- Mutates conversation state (recipients receive new `pollCreate` notification).

**Enum values used:** none (no enum parameters).

**Quirks / surprises:**
- **Polarity inversion**: The CLI flag `--no-multi` means "do NOT allow multiple votes"; the Java internal API uses positive `allowMultiple`. .NET surface SHOULD expose `AllowMultipleVotes` (default `true`), NOT `NoMulti` — match the internal Java API polarity, not the CLI flag polarity.
- Hard option-count limits are baked-in constants (2–10).
- Hard option-length limit is 100 chars.
- `notify-self` flag works as in other send-side commands (Wave 1-6 pattern).

---

### `sendPollVote` — Wave 7

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/SendPollVoteCommand.java @ bda4e7fc` (lines 1-107)
- Manager API: `m.sendPollVoteMessage(pollAuthor, pollTimestamp, options, voteCount, recipientIdentifiers, notifySelf)` (lines 85-90)

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | yes | — | E.164 phone number |
| `recipient` | `recipient` | `List<String>` | no | `[]` | E.164 phone numbers (nargs="*") |
| `group-id` / `group` | `groupId` / `group` | `List<String>` | no | `[]` | Group IDs (nargs="*") |
| `username` | `username` | `List<String>` | no | `[]` | Usernames (nargs="*") |
| `note-to-self` | `noteToSelf` | `boolean` | no | `false` | storeTrue |
| `notify-self` | `notifySelf` | `boolean` | no | `false` | storeTrue |
| `poll-author` | `pollAuthor` | `String` | no | self number | Phone number of poll author. If null, defaults to `selfNumber` (line 79-80, `CommandUtil.getSingleRecipientIdentifier` substitutes self) |
| `poll-timestamp` | `pollTimestamp` | `long` | **yes** | — | Timestamp of the original poll-create message (identifies which poll) |
| `option` | `option` | `List<Integer>` | no | `[]` | **Option indexes** (zero-based) to vote for (nargs="*"). Empty list = clear vote |
| `vote-count` | `voteCount` | `int` | **yes** | — | Monotonic counter — increment by 1 each time you re-vote (used by Signal protocol for conflict resolution) |

**Result (response) wire shape:**

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| Standard `outputResult` envelope | — | — | Same `JsonSendMessageResult`-list shape |

**Validation rules** (UserErrorException throws):
- `"The user <identifier> is not registered."` on `UnregisteredRecipientException`
- Group exceptions propagate their messages

No CLI-layer validation on `option` indexes / `voteCount` range — bounds-check happens server-side or in Manager.

**Error codes specific to this method:**
- `-1 UserError` — unregistered recipient / group errors
- `-3 IoError` — wrapped IOException with "No prekeys available" detection
- `-4 UntrustedIdentity` — possible at lower layers

**Side-effects:**
- Send-side write: delivers PollVote `dataMessage`. Identified by `(pollAuthor, pollTimestamp)` pair on receiver side.
- Vote-count monotonicity is enforced by **caller** — signal-cli does not auto-increment.

**Enum values used:** none.

**Quirks / surprises:**
- **`pollAuthor` defaults to self** when null (CommandUtil substitutes selfNumber). This means "I'm voting on my own poll" works without explicit `--poll-author`.
- `vote-count` is **NOT a recipient count** — it's a per-voter monotonic counter for vote-update semantics (vote again = `voteCount + 1`). Document this clearly in .NET XMLDoc.
- `option` list = INDEXES (zero-based into the original poll's `options[]`), not the option strings themselves.

---

### `sendPollTerminate` — Wave 7

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/SendPollTerminateCommand.java @ bda4e7fc` (lines 1-88)
- Manager API: `m.sendPollTerminateMessage(pollTimestamp, recipientIdentifiers, notifySelf)` (line 71)

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | yes | — | E.164 phone number |
| `recipient` | `recipient` | `List<String>` | no | `[]` | nargs="*" |
| `group-id` / `group` | `groupId` / `group` | `List<String>` | no | `[]` | nargs="*" |
| `username` | `username` | `List<String>` | no | `[]` | nargs="*" |
| `note-to-self` | `noteToSelf` | `boolean` | no | `false` | storeTrue |
| `notify-self` | `notifySelf` | `boolean` | no | `false` | storeTrue |
| `poll-timestamp` | `pollTimestamp` | `long` | **yes** | — | Timestamp of the original poll to terminate |

**Result (response) wire shape:**

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| Standard `outputResult` envelope | — | — | Same shape as other send-side methods |

**Validation rules** (UserErrorException throws):
- `"The user <identifier> is not registered."` on `UnregisteredRecipientException`
- Group exceptions propagate

**Error codes specific to this method:**
- `-1 UserError`
- `-3 IoError` — with "No prekeys available" detection

**Side-effects:**
- Send-side write: delivers PollTerminate `dataMessage`. Receivers see `pollTerminate` decoder fire.

**Enum values used:** none.

**Quirks / surprises:**
- **NO `poll-author` argument** — sending `sendPollTerminate` against another author's poll is not supported by the CLI command. The terminate message is identified by `targetSentTimestamp` only, implying signal-cli (or the Signal protocol) restricts termination to the original author. The receive-side `JsonPollTerminate` record also has only `targetSentTimestamp` (no author field), confirming.
- No options or vote arrays — minimal payload.

---

### `sendAdminDelete` — Wave 7

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/SendAdminDeleteCommand.java @ bda4e7fc` (lines 1-89)
- Manager API: `m.sendAdminDelete(targetAuthorIdentifier, targetTimestamp, groupIdentifiers, notifySelf, isStory)` (lines 74-78)

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | yes | — | E.164 phone number |
| `group-id` / `group` | `groupId` / `group` | `List<String>` | **yes** (effectively) | — | nargs="+" — group IDs. Admin-delete is **group-only** (line 62-64) |
| `notify-self` | `notifySelf` | `boolean` | no | `false` | storeTrue |
| `target-author` | `targetAuthor` | `String` | **yes** | — | Phone number of author of message to admin-delete (`-a`, `--target-author`) |
| `target-timestamp` | `targetTimestamp` | `long` | **yes** | — | Timestamp of message to delete (`-t`, `--target-timestamp`) |
| `story` | `story` | `boolean` | no | `false` | If true, admin-delete a story instead of a normal message (storeTrue) |

**Result (response) wire shape:**

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| Standard `outputResult` envelope | — | — | |

**Validation rules** (UserErrorException throws):
- `"Admin delete requires group IDs"` when `groupIdentifiers.isEmpty()` (line 62-64)
- `"The user <identifier> is not registered."` on `UnregisteredRecipientException`
- Group exceptions propagate

**Error codes specific to this method:**
- `-1 UserError` — empty groupIds / unregistered recipient / group errors
- `-3 IoError` — generic IOException wrap (no "No prekeys available" branch in this command)

**Side-effects:**
- Send-side write: emits AdminDelete `dataMessage`. Receivers (other group members) see `adminDelete` field on data message.
- **Group-admin-only operation** — receivers may reject if sender is not group admin (enforced at lower layers).

**Enum values used:** none.

**Quirks / surprises:**
- **No `recipient` / `username` / `note-to-self` arguments** — this is a group-only operation. Unlike pin/unpin/poll which work in DM + group, admin-delete is exclusively for group moderation.
- `--story` flag toggles story-deletion vs message-deletion target.
- Despite "admin delete" naming, the wrapper does NOT pre-validate sender's admin status; that's enforced server-side / group-policy-side.

---

### `sendPinMessage` — Wave 7

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/SendPinMessageCommand.java @ bda4e7fc` (lines 1-104)
- Manager API: `m.sendPinMessage(pinDuration, targetAuthorIdentifier, targetTimestamp, recipientIdentifiers, notifySelf, isStory)` (lines 87-92)

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | yes | — | E.164 phone number |
| `recipient` | `recipient` | `List<String>` | no | `[]` | nargs="*" |
| `group-id` / `group` | `groupId` / `group` | `List<String>` | no | `[]` | nargs="*" |
| `username` | `username` | `List<String>` | no | `[]` | nargs="*" |
| `note-to-self` | `noteToSelf` | `boolean` | no | `false` | storeTrue |
| `notify-self` | `notifySelf` | `boolean` | no | `false` | storeTrue |
| `pin-duration` | `pinDuration` | `int` | no | **`-1`** | Pin duration in seconds. **`-1` means pin-forever** (line 43-44, `setDefault(-1)`). `-d`, `--pin-duration` |
| `target-author` | `targetAuthor` | `String` | **yes** | — | Phone number of message author (`-a`, `--target-author`). However: if null AND single recipient is present, recipient becomes `targetAuthor` (line 78-85 fallback) |
| `target-timestamp` | `targetTimestamp` | `long` | **yes** | — | Timestamp of message to pin (`-t`, `--target-timestamp`) |
| `story` | `story` | `boolean` | no | `false` | Pin a story instead of normal message (storeTrue) |

**Result (response) wire shape:**

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| Standard `outputResult` envelope | — | — | |

**Validation rules** (UserErrorException throws):
- `"The user <identifier> is not registered."` on `UnregisteredRecipientException`
- Group exceptions propagate

**Error codes specific to this method:**
- `-1 UserError` — unregistered recipient / group errors
- `-3 IoError` — wrapped IOException

**Side-effects:**
- Send-side write: delivers PinMessage `dataMessage`. Recipients see `pinMessage` field.
- Pin duration of `-1` indicates **forever** (no expiry). Positive values = seconds until auto-unpin.

**Enum values used:** none.

**Quirks / surprises:**
- **`pin-duration = -1` sentinel** means pin-forever (Java `int`, NOT nullable; default literal `-1`). .NET SHOULD expose this as `int PinDurationSeconds = -1` with XMLDoc, NOT `int?`.
- **`target-author` is marked `.required(true)` BUT** there's a fallback at lines 78-85 — if `targetAuthor == null` AND there's exactly 1 single recipient, that recipient becomes the targetAuthor. This argparse4j "required" check on the input string is enforced before the fallback runs, so calling without `--target-author` always errors out at the CLI layer. **However**, JSON-RPC layer may bypass argparse validation — verify behavior in integration testing. Safest .NET surface: make `TargetAuthor` required.
- `--story` toggles pinning of a story vs message.

---

### `sendUnpinMessage` — Wave 7

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/SendUnpinMessageCommand.java @ bda4e7fc` (lines 1-100)
- Manager API: `m.sendUnpinMessage(targetAuthorIdentifier, targetTimestamp, recipientIdentifiers, notifySelf, isStory)` (lines 84-88)

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | yes | — | E.164 phone number |
| `recipient` | `recipient` | `List<String>` | no | `[]` | nargs="*" |
| `group-id` / `group` | `groupId` / `group` | `List<String>` | no | `[]` | nargs="*" |
| `username` | `username` | `List<String>` | no | `[]` | nargs="*" |
| `note-to-self` | `noteToSelf` | `boolean` | no | `false` | storeTrue |
| `notify-self` | `notifySelf` | `boolean` | no | `false` | storeTrue |
| `target-author` | `targetAuthor` | `String` | **yes** | — | Phone number of original message author (same fallback-on-single-recipient logic as pin) |
| `target-timestamp` | `targetTimestamp` | `long` | **yes** | — | Timestamp of message to unpin |
| `story` | `story` | `boolean` | no | `false` | Unpin a story instead of normal message (storeTrue) |

**Result (response) wire shape:**

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| Standard `outputResult` envelope | — | — | |

**Validation rules** (UserErrorException throws):
- `"The user <identifier> is not registered."` on `UnregisteredRecipientException`
- Group exceptions propagate

**Error codes specific to this method:**
- `-1 UserError` — unregistered recipient / group errors
- `-3 IoError` — wrapped IOException

**Side-effects:**
- Send-side write: delivers UnpinMessage `dataMessage`. Recipients see `unpinMessage` field.

**Enum values used:** none.

**Quirks / surprises:**
- **No `pin-duration` argument** — symmetric pin/unpin, but unpin doesn't need duration.
- Same `target-author` single-recipient fallback logic as `sendPinMessage` (lines 75-82).
- `--story` toggles story-unpin vs message-unpin.

---

### `sendMessageRequestResponse` — Wave 7

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/SendMessageRequestResponseCommand.java @ bda4e7fc` (lines 1-54)
- Enum (CLI-layer, restricted): `src/main/java/org/asamk/signal/commands/MessageRequestResponseType.java @ bda4e7fc` (lines 1-16)
- Enum (full internal): `lib/src/main/java/org/asamk/signal/manager/api/MessageEnvelope.java @ bda4e7fc:810-832` (Sync.MessageRequestResponse.Type)
- Manager API: `m.sendMessageRequestResponse(type, recipientIdentifiers)` (lines 51-52)

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | yes | — | E.164 phone number |
| `recipient` | `recipient` | `List<String>` | no | `[]` | nargs="*" |
| `group-id` / `group` | `groupId` / `group` | `List<String>` | no | `[]` | nargs="*" |
| `username` | `username` | `List<String>` | no | `[]` | nargs="*" |
| `type` | `type` | `MessageRequestResponseType` (CLI enum, string-typed) | **yes** | — | Argparse4j `enumStringType`. **CLI-level accepts ONLY `"accept"` or `"delete"`** (lowercase via `toUpperCase()` parse at line 44) |

**Result (response) wire shape:**

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| (no `outputResult` call) | — | — | **This command does NOT call `outputResult`** (line 51-52: `m.sendMessageRequestResponse(...)` is invoked but its result is NOT written). Wire response will be `null` / empty / just the success indicator from the JSON-RPC envelope. .NET should expose `Task` (not `Task<Response>`) or `Task<EmptyResponse>`. |

**Validation rules** (UserErrorException throws):
- None visible at CLI layer (no explicit validation in `handleCommand`; argparse4j validates `--type` against the enum)

**Error codes specific to this method:**
- `-1 UserError` — invalid type / group errors propagate from lower layers
- `-3 IoError` — possible from lower layers (not caught at this command level)

**Side-effects:**
- Sync-message write: **NOT a data-message**. Sends a `Sync.MessageRequestResponse` to linked devices to record the accept/delete state.
- Mutates conversation request state (accepting unblocks the conversation; deleting removes it from message-request inbox).

**Enum values used:**

**TWO enums in play — CRITICAL DISTINCTION:**

1. **`MessageRequestResponseType` (CLI-layer wrapper enum, this command's `--type` parameter):** ONLY 2 values from `src/main/java/org/asamk/signal/commands/MessageRequestResponseType.java`:
   - `ACCEPT` (toString = `"accept"`)
   - `DELETE` (toString = `"delete"`)

   That is, the `--type` CLI argument accepts **only** `accept` or `delete` (case-insensitive via `toUpperCase()` at line 44). The CLI-layer enum is intentionally restricted.

2. **`MessageEnvelope.Sync.MessageRequestResponse.Type` (full internal enum, used for receive-side decoding):** 8 values from `MessageEnvelope.java:810-832`:
   - `UNKNOWN`
   - `ACCEPT`
   - `DELETE`
   - `BLOCK`
   - `BLOCK_AND_DELETE`
   - `UNBLOCK_AND_ACCEPT`
   - `SPAM`
   - `BLOCK_AND_SPAM`

   This is the **full** Signal protocol enum used when DECODING incoming sync messages (`JsonSyncMessage.messageRequestResponse`). When .NET wraps `sendMessageRequestResponse`, the SEND surface should expose only `Accept` and `Delete` (mirroring upstream restriction); a separate enum / shared enum with more values should be used for receive-side decoding (Wave 7+ scope: also implement `JsonSyncMessage.messageRequestResponse` decoder).

**Quirks / surprises:**
- **The send-side enum is artificially restricted** to ACCEPT/DELETE — even though the Signal protocol supports 8 values for sync-message decoding, signal-cli's `--type` CLI parameter does NOT let you send BLOCK/BLOCK_AND_DELETE/etc. directly. Block-style operations go through `block` / `unblock` commands (Wave 3).
- **NO `outputResult` invocation** — response is empty.
- Note: at line 51 the code uses internal enum `Type.ACCEPT` / `Type.DELETE` (from `MessageEnvelope.Sync.MessageRequestResponse.Type`) — it maps CLI enum to internal enum 1-to-1.

---

### `sendPaymentNotification` — Wave 7

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/SendPaymentNotificationCommand.java @ bda4e7fc` (lines 1-53)
- Manager API: `m.sendPaymentNotificationMessage(receipt, note, recipientIdentifier)` (line 46)

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | yes | — | E.164 phone number |
| `recipient` | `recipient` | `String` | **yes** | — | E.164 phone number — **single recipient only**, NOT a list (unlike most other send-commands). No `--group-id` / `--username` support |
| `receipt` | `receipt` | `String` (base64) → `byte[]` | **yes** | — | Base64-encoded MobileCoin receipt blob. Decoded server-side via `Base64.getDecoder().decode(receiptString)` (line 42) |
| `note` | `note` | `String` | no | `null` | Optional plain-text note to accompany the payment |

**Result (response) wire shape:**

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| Standard `outputResult` envelope | — | — | Single-recipient `JsonSendMessageResult` |

**Validation rules** (UserErrorException throws):
- None at CLI layer — `Base64.getDecoder().decode` throws `IllegalArgumentException` on malformed base64 (propagates as untyped error)

**Error codes specific to this method:**
- `-3 IoError` — wrapped IOException → "Failed to send message: ... (<ClassName>)"
- (No `-1 UserError` branch — no `try-catch (UnregisteredRecipientException)`, no `try-catch (UserErrorException)` in this command)

**Side-effects:**
- Send-side write: delivers Payment `dataMessage` containing the MobileCoin receipt envelope. Receiver sees `payment` field on data message.
- **Does NOT execute any payment** — this is a *notification* of a payment receipt, not the transfer itself. MobileCoin transfer happens out-of-band.

**Enum values used:** none.

**Quirks / surprises:**
- **Single recipient only** — no group / username / multi-recipient support. The CLI argspec is `addArgument("recipient")` (no `nargs("*")`, no `"+"`).
- **`receipt` is BASE64 INPUT, byte[] internally.** .NET wrapper should expose `string Receipt` (base64) and decode/validate before send, OR `byte[] Receipt` and encode at send. Recommend: `byte[] Receipt` on the .NET surface (typed, more discoverable) with internal base64 encoding before JSON-RPC send.
- **NO `note-to-self` / `notify-self` flags** — payment notifications are specifically for an external recipient. Sending payment notification to self is not a supported flow.
- **Base64 decode happens server-side, MAY throw IllegalArgumentException** — bad base64 from caller is NOT caught at command level; will surface as non-typed error to the JSON-RPC client.

---

## Receive-side wire records (CRITICAL — .NET DTO must mirror exactly)

All 7 records use `@JsonSchema(title = ...)` (Micronaut JSON Schema annotation, NOT a Jackson naming annotation). Java record component names = Jackson camelCase JSON keys verbatim (default Jackson record naming). None of the 7 records carries a `@JsonProperty` rename. None of the 7 records carries `@JsonInclude(JsonInclude.Include.NON_NULL)` at the record level — null-inclusion is controlled at the parent `JsonDataMessage` field level instead.

### `JsonPollCreate` — Wave 7

**Source citation:** `src/main/java/org/asamk/signal/json/JsonPollCreate.java @ bda4e7fc` (lines 1-22)

**Wire record fields:**

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| `question` | `question` | `String` | Poll question text. Non-null. |
| `allowMultiple` | `allowMultiple` | `boolean` (primitive) | True if voters can pick multiple options. Primitive — never null on wire. |
| `options` | `options` | `List<String>` | Poll option strings, in original order. |

**Jackson naming overrides:** none (default record naming).
**`@JsonInclude` annotations on individual fields:** none.

---

### `JsonPollVote` — Wave 7

**Source citation:** `src/main/java/org/asamk/signal/json/JsonPollVote.java @ bda4e7fc` (lines 1-31)

**Wire record fields:**

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| `author` | `author` | `String` | **`@Deprecated`** (line 12). Legacy address identifier (`address.getLegacyIdentifier()`). Still on the wire. .NET DTO: include with `[Obsolete]` and `[JsonPropertyName("author")]`. |
| `authorNumber` | `authorNumber` | `String` | Author's E.164 phone number (nullable on wire — `.orElse(null)` at line 23). Possibly null if author is UUID-only. |
| `authorUuid` | `authorUuid` | `String` | Author's UUID as string (nullable on wire — `.orElse(null)` at line 24). Possibly null. |
| `targetSentTimestamp` | `targetSentTimestamp` | `long` (primitive) | Timestamp of the original poll-create message. |
| `optionIndexes` | `optionIndexes` | `List<Integer>` | Zero-based indexes of voted options. |
| `voteCount` | `voteCount` | `int` (primitive) | Vote-count counter (monotonic per voter). |

**Jackson naming overrides:** none. Note: `targetSentTimestamp` (NOT `pollTimestamp` as on the send side) — wire name MATCHES the field name verbatim.
**`@JsonInclude` annotations on individual fields:** none, but `authorNumber` / `authorUuid` may serialize as `null`. The parent `JsonDataMessage.pollVote` field IS `@JsonInclude(NON_NULL)`, so the whole `pollVote` object is omitted when there's no vote — but if present, all fields including nulls are included.
**Deprecated marker:** `author` field has `@Deprecated` Java annotation. This is documentation only — Jackson serializes it.

---

### `JsonPollTerminate` — Wave 7

**Source citation:** `src/main/java/org/asamk/signal/json/JsonPollTerminate.java @ bda4e7fc` (lines 1-15)

**Wire record fields:**

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| `targetSentTimestamp` | `targetSentTimestamp` | `long` (primitive) | Timestamp of the original poll to terminate. |

**Jackson naming overrides:** none.
**`@JsonInclude` annotations on individual fields:** none.
**Quirks:** Single-field record. NO `author` field (consistent with the send-side: terminator is implicit, must be original author).

---

### `JsonPayment` — Wave 7

**Source citation:** `src/main/java/org/asamk/signal/json/JsonPayment.java @ bda4e7fc` (lines 1-13)

**Wire record fields:**

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| `note` | `note` | `String` | Optional note text. Possibly null. |
| `receipt` | `receipt` | `byte[]` | MobileCoin receipt blob. **Jackson serializes `byte[]` as base64-encoded JSON string by default** ([Jackson behavior](https://github.com/FasterXML/jackson-databind#binary-data) — `byte[]` is treated as binary, encoded to base64). .NET DTO: expose as `byte[]` (deserialize from base64 string). |

**Jackson naming overrides:** none.
**`@JsonInclude` annotations on individual fields:** none.
**Quirks:** `byte[]` field — Jackson defaults to base64 (`BinaryNode`) when encoding to JSON. .NET `System.Text.Json` ALSO encodes `byte[]` as base64 by default, so the round-trip is symmetric.

---

### `JsonPinMessage` — Wave 7

**Source citation:** `src/main/java/org/asamk/signal/json/JsonPinMessage.java @ bda4e7fc` (lines 1-32)

**Wire record fields:**

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| `targetAuthor` | `targetAuthor` | `String` | **`@Deprecated`** (line 11). Legacy address identifier. |
| `targetAuthorNumber` | `targetAuthorNumber` | `String` | Pinned-message author E.164 (nullable). |
| `targetAuthorUuid` | `targetAuthorUuid` | `String` | Pinned-message author UUID-as-string (nullable). |
| `targetSentTimestamp` | `targetSentTimestamp` | `long` (primitive) | Timestamp of pinned message. |
| `pinDurationSeconds` | `pinDurationSeconds` | `long` (primitive) | **Pin duration in seconds.** `-1` = forever (sentinel value matches send-side default). |

**Jackson naming overrides:** none.
**`@JsonInclude` annotations on individual fields:** none.
**Deprecated marker:** `targetAuthor` field has `@Deprecated` Java annotation.
**Note:** `pinDurationSeconds` is `long` on the wire (not `int` as on the send side). Send accepts `int`, decode expects `long`. Likely 64-bit safe in both directions but worth noting.

---

### `JsonUnpinMessage` — Wave 7

**Source citation:** `src/main/java/org/asamk/signal/json/JsonUnpinMessage.java @ bda4e7fc` (lines 1-23)

**Wire record fields:**

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| `targetAuthor` | `targetAuthor` | `String` | **`@Deprecated`** (line 11). Legacy identifier. |
| `targetAuthorNumber` | `targetAuthorNumber` | `String` | Author E.164 (nullable). |
| `targetAuthorUuid` | `targetAuthorUuid` | `String` | Author UUID-as-string (nullable). |
| `targetSentTimestamp` | `targetSentTimestamp` | `long` (primitive) | Timestamp of message being unpinned. |

**Jackson naming overrides:** none.
**`@JsonInclude` annotations on individual fields:** none.
**Deprecated marker:** `targetAuthor` field has `@Deprecated`.
**Symmetric to `JsonPinMessage` minus `pinDurationSeconds`.**

---

### `JsonAdminDelete` — Wave 7

**Source citation:** `src/main/java/org/asamk/signal/json/JsonAdminDelete.java @ bda4e7fc` (lines 1-22)

**Wire record fields:**

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| `targetAuthor` | `targetAuthor` | `String` | **`@Deprecated`** (line 10). Legacy identifier. |
| `targetAuthorNumber` | `targetAuthorNumber` | `String` | Author E.164 (nullable). |
| `targetAuthorUuid` | `targetAuthorUuid` | `String` | Author UUID-as-string (nullable). |
| `targetSentTimestamp` | `targetSentTimestamp` | `long` (primitive) | Timestamp of admin-deleted message. |

**Jackson naming overrides:** none.
**`@JsonInclude` annotations on individual fields:** none.
**Deprecated marker:** `targetAuthor` has `@Deprecated`.
**Structurally identical to `JsonUnpinMessage` (4 fields, same names/types).** Differentiated only by which `JsonDataMessage` field carries the record (`adminDelete` vs `unpinMessage`).

---

## `JsonDataMessage` extension — where these 7 records appear

**Source citation:** `src/main/java/org/asamk/signal/json/JsonDataMessage.java @ bda4e7fc` (lines 1-107)

`JsonDataMessage` is the central wire record for `receive`-notification data-messages. ALL 7 of the receive-side decoders in this wave appear as nullable fields on it. Each is marked `@JsonInclude(JsonInclude.Include.NON_NULL)` at the field level — so the JSON key is OMITTED (not `null`) when the data-message doesn't carry that payload type.

**Field-name table** (JsonDataMessage → record routing):

| Java field | JSON name | Record type | Source line |
|---|---|---|---|
| `payment` | `payment` | `JsonPayment` | line 20 |
| `pollCreate` | `pollCreate` | `JsonPollCreate` | line 27 |
| `pollVote` | `pollVote` | `JsonPollVote` | line 28 |
| `pollTerminate` | `pollTerminate` | `JsonPollTerminate` | line 29 |
| `pinMessage` | `pinMessage` | `JsonPinMessage` | line 33 |
| `unpinMessage` | `unpinMessage` | `JsonUnpinMessage` | line 34 |
| `adminDelete` | `adminDelete` | `JsonAdminDelete` | line 35 |

All 7 fields share these properties:
- **Wrapped in `@JsonInclude(JsonInclude.Include.NON_NULL)`** at the field declaration — the JSON key is omitted when the value is null.
- **Default to `null`** in the `from()` factory when the source `MessageEnvelope.Data` doesn't carry the payload (see `.orElse(null)` patterns at lines 51, 72-74, 79-81).
- **Are PRESENCE-BASED UNIONS** (critical rule #4 from CLAUDE.md): a single `dataMessage` can carry multiple payload types simultaneously (e.g. `message` text + `pollCreate` + `mentions[]`). `.NET` event dispatch MUST emit observable + async-channel for EVERY present field, not just the first.

**.NET event-decoder routing implication:**

Each of these 7 receive-side records gets a paired `IObservable<T>` + `IAsyncEnumerable<T>` on `ISignalEventService`:
- `PollCreate` / `PollCreateAsync`
- `PollVote` / `PollVoteAsync`
- `PollTerminate` / `PollTerminateAsync`
- `Payment` / `PaymentAsync`
- `PinMessage` / `PinMessageAsync`
- `UnpinMessage` / `UnpinMessageAsync`
- `AdminDelete` / `AdminDeleteAsync`

This adds 7 new event-pair kinds to `SignalEventService` (was 10 in 4.0.x → 17 in this wave). EventApiSymmetry regression guard (`RG06`) will pin the pair-symmetry; each new pair MUST also extend `MeterTagValues_AreOnlyKnownEnumLiterals` if a new `event_type` tag value is introduced.

---

## Verification

Java source files read for this wave (all `@ bda4e7fc`):

**Send-side commands (8 files):**
1. `src/main/java/org/asamk/signal/commands/SendPollCreateCommand.java`
2. `src/main/java/org/asamk/signal/commands/SendPollVoteCommand.java`
3. `src/main/java/org/asamk/signal/commands/SendPollTerminateCommand.java`
4. `src/main/java/org/asamk/signal/commands/SendAdminDeleteCommand.java`
5. `src/main/java/org/asamk/signal/commands/SendPinMessageCommand.java`
6. `src/main/java/org/asamk/signal/commands/SendUnpinMessageCommand.java`
7. `src/main/java/org/asamk/signal/commands/SendMessageRequestResponseCommand.java`
8. `src/main/java/org/asamk/signal/commands/SendPaymentNotificationCommand.java`

**Send-side enum (1 file):**
9. `src/main/java/org/asamk/signal/commands/MessageRequestResponseType.java` — CLI-layer 2-value enum (ACCEPT, DELETE)

**Receive-side decoder records (7 files):**
10. `src/main/java/org/asamk/signal/json/JsonPollCreate.java`
11. `src/main/java/org/asamk/signal/json/JsonPollVote.java`
12. `src/main/java/org/asamk/signal/json/JsonPollTerminate.java`
13. `src/main/java/org/asamk/signal/json/JsonPayment.java`
14. `src/main/java/org/asamk/signal/json/JsonPinMessage.java`
15. `src/main/java/org/asamk/signal/json/JsonUnpinMessage.java`
16. `src/main/java/org/asamk/signal/json/JsonAdminDelete.java`

**Parent record where 7 receive-side records are routed:**
17. `src/main/java/org/asamk/signal/json/JsonDataMessage.java` — lines 20, 27-29, 33-35 confirm field-name routing for all 7 records, all with `@JsonInclude(NON_NULL)`.

**Internal enum (referenced from `sendMessageRequestResponse`):**
18. `lib/src/main/java/org/asamk/signal/manager/api/MessageEnvelope.java` lines 796-833 — full 8-value `Sync.MessageRequestResponse.Type` enum (UNKNOWN, ACCEPT, DELETE, BLOCK, BLOCK_AND_DELETE, UNBLOCK_AND_ACCEPT, SPAM, BLOCK_AND_SPAM).

**Total Java files read:** 18.

**Anti-hallucination note:** The user-supplied task description anticipated `MessageRequestResponseType` would expose values like `BLOCK`, `BLOCK_AND_DELETE`, `UNBLOCK`, etc. as the SEND-side enum. **Source contradicts this** — the CLI-layer `MessageRequestResponseType.java` is restricted to just 2 values (`ACCEPT` and `DELETE`). The fuller 8-value enum exists only for *receive-side* decoding (inside `MessageEnvelope.Sync.MessageRequestResponse.Type`). The .NET wave-7 implementation MUST reflect this split: send-side `MessageRequestResponseType` enum gets 2 values; receive-side (when `JsonSyncMessage.messageRequestResponse` decoder is implemented later) gets the full 8.
