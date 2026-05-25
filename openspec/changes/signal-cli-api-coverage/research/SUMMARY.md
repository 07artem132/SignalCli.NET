# Research summary — findings that change the plan

Compiled from `wave-1-…md` through `wave-8-…md` after spot-verification against
signal-cli source `@ bda4e7fc`. **Each finding below was cross-verified by reading
the cited Java file directly, not relying on agent summary alone.**

The §0.5 source-of-truth protocol earned its keep here — without it, at least 4
DTO designs and 1 entire receive-side event stream would have been hallucinated
into existence.

## 🚨 Design-impacting findings (require `design.md` / `tasks.md` edits before Wave-N implementation)

### F1 — Wave 4: `stickerPackOperation` sync event does NOT exist on wire

**Impact:** `proposal.md` lines 25, 58 + `tasks.md` task 4.10 + `design.md` §1.9
all assume `JsonSyncMessage.stickerPackOperations: List<JsonStickerPackOperation>`
exists upstream. **It doesn't.**

**Source evidence:** Reading `src/main/java/org/asamk/signal/json/JsonSyncMessage.java`
(file fully read in `wave-4-…md` §7) shows only 6 fields: `sentMessage`,
`sentStoryMessage`, `blockedNumbers`, `blockedGroupIds`, `readMessages`, `type`.

`grep -rn "stickerPackOperation"` against `src/main/java/org/asamk/signal/json/`
returns **zero matches**. The handling is in `IncomingMessageHandler.java:659-672`
where signal-cli auto-installs sticker packs locally and silently — never bubbling
to JSON-RPC consumers.

**Required action:**
- Drop "sticker-pack-install event decoder" from Wave 4 scope.
- Either:
  - **(a)** mark out-of-scope in `proposal.md` "Out of scope" with rationale "upstream signal-cli does not surface sticker-pack sync via JSON-RPC", OR
  - **(b)** open a separate OpenSpec change to PR upstream signal-cli adding the field, then come back. Path (b) is months of work; path (a) is the realistic option.
- Renumber `event-decoding-expansion` count from "7 nових паралельних event-stream'ів" → "6" (drop StickerPackInstalls pair).

### F2 — Wave 7: `MessageRequestResponseType` (send-side) has 2 values, not 5

**Impact:** `tasks.md` task 7.2 enumerates "5 values (Accept, Delete, Block,
BlockAndDelete, Unblock)" for `MessageRequestResponseType`. **The CLI-layer enum
only has 2.**

**Source evidence:** `src/main/java/org/asamk/signal/commands/MessageRequestResponseType.java`
(verbatim):

```java
enum MessageRequestResponseType {
    ACCEPT { @Override public String toString() { return "accept"; } },
    DELETE { @Override public String toString() { return "delete"; } }
}
```

The 8-value enum (`UNKNOWN`/`ACCEPT`/`DELETE`/`BLOCK`/`BLOCK_AND_DELETE`/`UNBLOCK_AND_ACCEPT`/
`SPAM`/`BLOCK_AND_SPAM`) lives in `MessageEnvelope.Sync.MessageRequestResponse.Type`
and is **receive-side only** — used when decoding sync messages from another linked
device, never as a send-side parameter.

**Required action:**
- Edit `tasks.md` 7.2: send-side `MessageRequestResponseType` enum = `Accept | Delete` (.NET PascalCase).
- Edit `tasks.md` 7.8.x (receive-side): add an 8-value `MessageRequestResponseSyncType` enum for sync-event decoder.
- Verify the wire serialization: Java `enum.toString()` returns lowercase `"accept"`/`"delete"` — but argparse4j may upper-case via `.choices(...)`. Test against real wire payload before locking `[JsonStringEnumConverter]` casing.

### F3 — Wave 6: `--number-sharing` is `Boolean`, not enum

**Impact:** `tasks.md` task 6.2.1 declares `UpdateAccountOptions.NumberSharingMode` as enum.

**Source evidence:** `UpdateAccountCommand.java:37-39` + line 57:
```java
subparser.addArgument("--number-sharing")
ns.getBoolean("number-sharing")
m.updateAccountAttributes(deviceName, unrestrictedUnidentifiedSender, discoverableByNumber, numberSharing);
```

`PhoneNumberSharingMode` enum (`EVERYBODY`/`CONTACTS`/`NOBODY`) exists in
`manager/api/PhoneNumberSharingMode.java` but is **internal-only** — not exposed
on the JSON-RPC surface.

**Required action:**
- Edit `tasks.md` 6.2.1: `NumberSharingMode` → `NumberSharing: bool?` (nullable).
- Drop `PhoneNumberSharingMode` enum from .NET surface (it would lie about upstream API).

### F4 — Wave 6: `startChangeNumber` arg is `--voice`, not `--voice-verification`

**Impact:** `tasks.md` task 6.2.1 declares `VoiceVerification` field.

**Source evidence:** `StartChangeNumberCommand.java:33`:
```java
subparser.addArgument("-v", "--voice")
```

**Required action:** Edit `tasks.md` 6.2.1: `VoiceVerification` → `Voice: bool` (or keep `VoiceVerification` as the .NET property name with `[JsonPropertyName("voice")]`, since the upstream wire key is `voice`).

### F5 — Wave 8: `getUserStatus` recipients/usernames are NOT mutually exclusive

**Impact:** `design.md` (and `tasks.md` task 8.1) claims `recipients` and
`usernames` are mutually exclusive.

**Source evidence:** `GetUserStatusCommand.java:66-81` shows the dispatcher uses
`Stream.concat(...)` to MERGE the two lists into a single status query, with each
result row tagged by which input it came from (`recipient` vs `username` field
on response).

**Required action:**
- Edit `tasks.md` 8.1: drop "mutually exclusive" constraint; document that both arrays merge.
- `GetUserStatusOptions` exposes BOTH `Recipients: IReadOnlyList<string>?` and `Usernames: IReadOnlyList<string>?` simultaneously.

## ⚠️ Implementation-impacting findings (no plan edit needed, but downstream impl must respect)

### F6 — Wave 5: `listDevices` JSON output drops `isThisDevice` (5th field on Java side)

`Device` record in `manager/api/Device.java` has 5 fields: `id, name, createdTimestamp, lastSeenTimestamp, isThisDevice`. JSON projection in `ListDevicesCommand.java:67` writes 4. The `isThisDevice` is consumed only by PlainText output for the `" (this device)"` annotation.

**Consequence:** .NET `Device` record mirrors **4 fields**, not 5. Adding `IsThisDevice` would require a separate self-id lookup via Manager API.

### F7 — Wave 1: `sendReceipt` recipient is singular (vs sendReaction/sendTyping/remoteDelete = list)

`SendReceiptCommand.java:25` registers `addArgument("recipient")` WITHOUT `.nargs(...)`. `ns.getString("recipient")` confirms singular. The plural-fallback in `JsonRpcNamespace.java` does NOT apply because there's no `.nargs("+")` array signal.

**Consequence:** `SendReceiptOptions` exposes a single `Recipient: string`, not `Recipients: IReadOnlyList<string>`. Wave-1 implementation must NOT auto-pluralize via convention.

### F8 — Wave 2: `quitGroup` silently swallows `NotAGroupMemberException`

`QuitGroupCommand.java:59` catches `NotAGroupMemberException` and returns empty success. Combined with CLAUDE.md rule #14 (idempotent state errors): the .NET `QuitGroupAsync` SHOULD also be idempotent — calling it for a group you're not in returns success, not an exception.

### F9 — Wave 3: `removeContact` mut group is argparse-only

`RemoveContactCommand.java:23` uses `addMutuallyExclusiveGroup()` for `--hide` vs `--forget`, but this constraint is enforced only at the argparse4j CLI level. JSON-RPC clients can set BOTH `hide=true` and `forget=true` simultaneously — and line 41-45 then executes `hide` (first-wins).

**Consequence:** .NET `RemoveContactOptions` should enforce the constraint client-side via validation in the Builder, since wire-layer would accept both and behave in an unintuitive first-wins way.

### F10 — Wave 1: 4-way IOException error-mapping inconsistency

Across the 4 Wave-1 send commands, IOException maps to **different RPC error codes**:
- `sendReaction` → `-32603 InternalError` (via `UnexpectedErrorException`)
- `sendTyping` → `-1 UserError`
- `remoteDelete` → `-32603 InternalError`
- `sendReceipt` → no IOException catch path (would bubble unhandled)

This is upstream signal-cli inconsistency, not a wrapper bug. .NET XMLDoc on each method should reflect the actual mapping; consumers should not be told "IOExceptions are always -32603."

## 🔬 Secondary implementation findings (F11-F25) — promoted into tasks.md per user review 2026-05-25

Wave research files contained ~15 additional findings that did NOT rise to F1-F10
plan-breaking severity but DO affect concrete DTO/service-method implementation
decisions. After user review, all 15 were promoted into `tasks.md` as inline
`§FN reminder:` annotations next to the relevant task (mirroring F6-F10 pattern).
Listed here as a registry; details + citations live in `wave-N-….md` files.

### 🔴 HIGH severity — production-break-without-handling

- **F11 — Wave 5: `addDevice` `pub_key` base64 is stripped of `=` padding** (`DeviceLinkUrl.java:48 @ bda4e7fc`). `Convert.FromBase64String` у .NET throws `FormatException` without restored padding. Implementation MUST restore padding before decoding. Surfaced in `tasks.md §5.4`.

- **F12 — Wave 5: `updateDevice.deviceName` is encrypted server-side and MUST NOT be logged above Trace** (Critical rule #1 implication). `SignalDevicesLog` `UpdateDevice*` templates exclude `{DeviceName}` parameter at `Information+` level. Surfaced in `tasks.md §5.5`.

### 🟡 MEDIUM severity — semantic divergence requiring DTO/API design choice

- **F13 — Wave 2: `joinGroup.onlyRequested` is dimorphic** — present+true OR completely absent (NEVER `false`). `JoinGroupResponse.OnlyRequested` MUST be `bool?` (nullable), not `bool` defaulting to false; absent = direct join, true = pending admin approval. Surfaced in `tasks.md §2.1`.

- **F14 — Wave 2: `updateGroup` with `groupId == null` triggers CREATE-then-update** (`UpdateGroupCommand.java`). Same RPC method has dual semantic. .NET API may want to split into explicit `CreateGroupAsync(CreateGroupOptions)` + `UpdateGroupAsync(UpdateGroupOptions)` for clarity, OR document the dual-mode on a single method. Surfaced in `tasks.md §2.1`.

- **F15 — Wave 7: `sendPollCreate` has baked-in validation constants** — 2-10 options array length, ≤100 chars per option (`MAX_POLL_OPTIONS = 10`, `MAX_POLL_OPTION_LENGTH = 100`). `PollCreateOptions.Builder` MUST validate client-side via `Build()` throwing `ArgumentException`. Surfaced in `tasks.md §7.1`.

- **F16 — Wave 7: `pinDurationSeconds` type asymmetry** — `int` on send wire, `long` on receive wire. .NET DTOs MUST use `long` on both sides (widest type) to avoid silent truncation. Surfaced in `tasks.md §7.2` + `§7.8.1`.

- **F17 — Wave 7: 5 of 7 receive-side Json* records carry `@Deprecated targetAuthor`/`author` legacy-identifier fields** that signal-cli still serializes for backward-compat. .NET DTOs MUST mirror with `[Obsolete]` markers + still serialize them (forward-compat with old wire payloads). Surfaced in `tasks.md §7.8.1`.

- **F18 — Wave 3: `updateProfile.Avatar` vs `RemoveAvatar` XOR** — same XOR pattern as F9 (RemoveContact). `UpdateProfileOptions.Builder` MUST validate XOR client-side; wire silently accepts both with first-wins behavior otherwise. Surfaced in `tasks.md §3.1`.

- **F19 — Wave 4: `getAvatar.Contact`/`Profile`/`GroupId` 3-way XOR** — `GetAvatarOptions.Builder` MUST validate exactly-one-of-three set; wire enforces argparse-only. Surfaced in `tasks.md §4.1`.

### 🟢 LOW severity — XMLDoc/divergence notes

- **F20 — Wave 1: only `sendReaction` has `--notify-self` flag + catches `UnregisteredRecipientException` at top level**. Other 3 Wave-1 send methods lack both. XMLDoc on `SendReactionAsync` must call this out; uniform-API assumption is wrong. Surfaced in `tasks.md §1.2.1`.

- **F21 — Wave 7: `sendPollCreate` CLI flag `--no-multi` vs internal `allowMultiple` polarity inversion**. .NET should expose positive polarity (`AllowMultipleVotes: bool = true`) to mirror internal API and avoid double-negative cognitive cost. Surfaced in `tasks.md §7.1`.

- **F22 — Wave 7: `sendPollVote.option` is zero-based integer indexes, not strings**. DTO field is `IReadOnlyList<int>` of indexes into the original poll's options array. Easy to misimplement as `IReadOnlyList<string>`. Surfaced in `tasks.md §7.1`.

- **F23 — Wave 7: `sendPinMessage.pinDuration = -1` is a sentinel value meaning "pin forever"**; positive = seconds. .NET design choice: expose as `TimeSpan?` (null = forever) for ergonomic ergonomics over int-sentinel. Surfaced in `tasks.md §7.2`.

- **F24 — Wave 8: `submitRateLimitChallenge` missing-key throws NPE → `-32603 InternalError`** (not the typical `-32602 InvalidParams` for missing required field). Upstream `required(true)` on `Argument` is NOT enforced for JSON-RPC. Test must assert against `-32603`, not `-32602`. Surfaced in `tasks.md §8.1`.

- **F25 — Empty responses (all Wave-8 commands + several others) are literal `{}` JSON object, not `null`**. DTO design: `record FooResponse()` with empty body, NOT `Task<Empty?>`. Source: `SignalJsonRpcCommandHandler.java:281` (`result[0] == null ? Map.of() : result[0]`). Surfaced in `tasks.md §8.1`.

## ✅ Findings that confirm the plan as-written

- All 6 Wave-4 send-side sticker/binary-fetch methods exist as described in plan.
- All Wave-3 contacts methods exist; the `JsonContact`/`JsonIdentity`/`TrustLevel` types match what `design.md` hypothesized.
- Wave-5 4 device methods, Wave-8 3 utility methods, Wave-2 3 group methods — all confirmed with template-conformant param/result/error tables.
- 7 receive-side Json* records (poll/payment/pin/unpin/admin-delete) all exist verbatim where `tasks.md §7.8.1` cites them.
- Common envelope: `account` param consumed by dispatcher before command runs (`SignalJsonRpcCommandHandler.java:127-139`); plural-fallback (`recipient` ↔ `recipients`) via `JsonRpcNamespace.java:13-43`; both confirmed across all 8 waves.
- `JsonRpcErrorCode` enum (-1/-3/-4/-5/-6) covers all custom error codes — no new error codes surfaced.

## Source-of-truth coverage

Across the 8 wave research files, agents read **127 distinct Java source files** at SHA `bda4e7fc`, with file:line citations for every claim. Each `wave-N-….md` ends with a `## Verification` section enumerating the exact paths read; a human reviewer can spot-check any finding in ≤2 minutes.

## Recommended next actions

1. **Before Wave 4 implementation:** decision on F1 (sticker-pack-install event drop or upstream-PR-first).
2. **Before Wave 6 implementation:** edit `tasks.md` §6.2.1 per F3, F4.
3. **Before Wave 7 implementation:** edit `tasks.md` §7.2 per F2 (and add §7.8 receive-side enum).
4. **Before Wave 8 implementation:** edit `design.md` getUserStatus section per F5.
5. ~~**General:** add F6-F10 as implementation-note remarks in `tasks.md` next to the relevant tasks~~ **DONE 2026-05-25** (commit `8582941`) — F1-F10 surfaced as inline `§FN reminder` annotations.
6. ~~**Secondary surfacing:** promote F11-F25 from wave-N research files into `tasks.md`~~ **DONE 2026-05-25** (this commit) — all 15 secondary findings surfaced as inline `§FN reminder` annotations next to the relevant task.
7. **Anti-hallucination protocol §0.5 stays mandatory** — this research surfaced 5 plan-breaking discrepancies (F1-F5) + 5 implementation-impacting findings (F6-F10) + 15 secondary findings (F11-F25) in a corpus of 44 methods. That's ≈25/44 = **57% of methods had at least one non-obvious discrepancy** between LLM-authored task description and actual signal-cli Java source. Implementing without source-reading would have shipped multiple broken DTO designs to production. RG10 (`SourceCitationConsistencyTests`) enforcement at Wave 1 makes this protocol structurally permanent.
