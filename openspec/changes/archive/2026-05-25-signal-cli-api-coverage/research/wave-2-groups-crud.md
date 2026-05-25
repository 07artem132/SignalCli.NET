# Wave 2 — groups CRUD

Per `tasks.md §0.5` anti-hallucination protocol — wire-shape extracted from
signal-cli upstream Java source @ pinned commit `bda4e7fc` ("Prepare next
release", 2026-05-24, tag `v0.14.4.1`).

**JSON-RPC param naming convention.** signal-cli CLI uses kebab-case
(`--group-id`, `--remove-member`) for argparse4j. The JSON-RPC layer reads via
`JsonRpcNamespace` (`src/main/java/org/asamk/signal/commands/JsonRpcNamespace.java
@ bda4e7fc`), which converts every `dest` → camelCase via
`Util.dashSeparatedToCamelCaseString` (`src/main/java/org/asamk/signal/util/Util.java:36-39
@ bda4e7fc`). So `--remove-member` becomes `removeMember` in JSON params; plural
list-args also accept a `+s` suffix via `JsonRpcNamespace.getList` (line 41).
For `member`/`remove-member`/`admin`/`remove-admin`/`ban`/`unban` (all
`nargs("*")` lists), consumers MAY pass either the singular key or
`members`/`removeMembers`/`admins`/`removeAdmins`/`bans`/`unbans` — both
resolve. **Implementation choice:** expose camelCase plural names
(`members`, `removeMembers`, `admins`, `removeAdmins`, `bans`, `unbans`) for
ergonomics — matches the convention from existing `ISignalGroups.UpdateGroup`
and aligns with the auto-pluralized JSON-RPC alias.

**Errors common to all three methods** (mapping in
`SignalJsonRpcCommandHandler.java:39-43, 250-273 @ bda4e7fc`):

- `-1 UserError` — `UserErrorException` (bad input / not registered / invalid
  link / unknown group).
- `-3 IoError` — `IOErrorException` (network / IO failure during send).
- `-32603 InternalError` — `UnexpectedErrorException` (uncaught IOException in
  `updateGroup`; uncaught `Throwable` anywhere).
- `-32600 InvalidRequest` — JSON-mapping failure on params parse.
- `-32602 InvalidParams` — missing/unknown `account` parameter in
  multi-account mode (`SignalJsonRpcCommandHandler.java:80-94, 132-134`).

`-4 UntrustedIdentity` / `-5 RateLimit` codes do NOT surface from these three
methods directly — group CRUD does not throw the corresponding outer
`UntrustedKeyErrorException` / `RateLimitErrorException`. Individual recipient
send-failures (untrusted key / rate-limit) are reported inline via the
per-result `type` field on `results[].type` (values: `SUCCESS`,
`NETWORK_FAILURE`, `UNREGISTERED_FAILURE`, `IDENTITY_FAILURE`,
`RATE_LIMIT_FAILURE`, `INVALID_PRE_KEY_FAILURE` — see
`src/main/java/org/asamk/signal/json/JsonSendMessageResult.java:41-48 @
bda4e7fc`).

---

### `joinGroup` — Wave 2

**Source citation:**

- Command: `src/main/java/org/asamk/signal/commands/JoinGroupCommand.java @ bda4e7fc`
- Manager API: `Manager.joinGroup(GroupInviteLinkUrl)` returns
  `Pair<GroupId, SendGroupMessageResults>` (called at `JoinGroupCommand.java:55`).
- Result record: `lib/src/main/java/org/asamk/signal/manager/api/SendGroupMessageResults.java
  @ bda4e7fc` — `record SendGroupMessageResults(long timestamp, List<SendMessageResult> results)`.
- JSON wire record per-result: `src/main/java/org/asamk/signal/json/JsonSendMessageResult.java @ bda4e7fc`.

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| account | `account` | `String` | yes (multi-account mode) | — | E.164 phone. Consumed + removed before command params parse (`SignalJsonRpcCommandHandler.java:127-130`). Single-account mode auto-resolves. |
| uri | `uri` | `String` | yes | — | `subparser.addArgument("--uri").required(true)` (line 31). Signal group invitation link (e.g. `https://signal.group/#…` or `sgnl://signal.group/#…`). |

**Result (response) wire shape:**

Result is a `Map<String, Object>` written via `JsonWriter.write` —
`JoinGroupCommand.java:58-77`. Shape varies based on whether the user was
auto-joined (full member) vs invite-pending (admin-approval required):

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| timestamp | `timestamp` | `long` | Send timestamp (epoch ms) from `SendGroupMessageResults.timestamp()`. |
| results | `results` | `List<JsonSendMessageResult>` | Per-recipient send outcomes from `SendMessageResultUtils.getJsonSendMessageResults`. See "JsonSendMessageResult shape" below. |
| groupId | `groupId` | `String` (base64) | New/joined group ID; `GroupId.toBase64()`. |
| onlyRequested | `onlyRequested` | `Boolean` (optional, `true` only if pending) | Present + `true` IFF `m.getGroup(newGroupId).isMember()` returned `false` — the group requires admin approval and user is in pending state. Absent entirely when user is a full member. |

**`JsonSendMessageResult` shape** (each entry in `results`):

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| recipientAddress | `recipientAddress` | `JsonRecipientAddress` | Object with `uuid`, `number`, `username` (nullables). |
| groupId | `groupId` | `String` (base64) | `@JsonInclude(NON_NULL)` — present only for group-scoped sends. |
| type | `type` | `Type` enum | `SUCCESS` \| `NETWORK_FAILURE` \| `UNREGISTERED_FAILURE` \| `IDENTITY_FAILURE` \| `RATE_LIMIT_FAILURE` \| `INVALID_PRE_KEY_FAILURE`. |
| token | `token` | `String` | `@JsonInclude(NON_NULL)` — proof-required CAPTCHA token (rare). |
| retryAfterSeconds | `retryAfterSeconds` | `Long` | `@JsonInclude(NON_NULL)` — for `RATE_LIMIT_FAILURE`. Computed via `Math.ceilDiv(retryAfterMs, 1000)`. |

**Validation rules** (UserErrorException throws):

- `"Group link is invalid: " + e.getMessage()` коли `GroupInviteLinkUrl.fromUri(uri)` кидає `InvalidGroupLinkException` (`JoinGroupCommand.java:44-45`).
- `"Group link was created with an incompatible version: " + e.getMessage()` коли кидає `UnknownGroupLinkVersionException` (`JoinGroupCommand.java:46-47`).
- `"Link is not a signal group invitation link"` коли `linkUrl == null` (`JoinGroupCommand.java:50-52`).
- `"Group link is not valid: " + e.getMessage()` коли `m.joinGroup` кидає `InactiveGroupLinkException` (`JoinGroupCommand.java:96-97`).
- `"Pending admin approval: " + e.getMessage()` коли кидає `PendingAdminApprovalException` (`JoinGroupCommand.java:98-99`).

**Error codes specific to this method:**

- `-1 UserError` — invalid link / unknown link version / null link / inactive link / pending-admin-approval.
- `-3 IoError` — `IOException` from `m.joinGroup` wrapped як `IOErrorException("Failed to send message: " + msg + " (" + className + ")", e)` (`JoinGroupCommand.java:90-95`).
- `-32603 InternalError` — uncaught `Throwable`.

**Side-effects:**

- **Mutates state:** додає account до групи (або в pending-state, або як повноправного member-а).
- **Triggers send:** sends "join group" change message to existing group members.
- **No notifications generated to consumer** beyond response.
- Subsequent `listGroups` reflects new membership.

**Enum values used:** None at command-input layer; result-layer
`JsonSendMessageResult.Type` enum is wire-formatted as UPPER_SNAKE_CASE
(`SUCCESS`, `NETWORK_FAILURE`, `UNREGISTERED_FAILURE`, `IDENTITY_FAILURE`,
`RATE_LIMIT_FAILURE`, `INVALID_PRE_KEY_FAILURE` —
`JsonSendMessageResult.java:42-47`).

**Quirks / surprises:**

- **`onlyRequested` is dimorphic in the response object** — present-and-`true` for pending-admin-approval joins, **completely absent** (not `false`!) for direct joins. Two different `Map.of(...)` branches in `JoinGroupCommand.java:61-76` enforce this. .NET DTO must use `bool? OnlyRequested { get; }` (nullable, default null) or `[JsonIgnoreCondition.WhenWritingNull]` — never default `false`.
- **`groupId` is base64-encoded byte[]** — `GroupId.toBase64()` is invoked explicitly (line 66, 75); this is a hand-formatted string, not Jackson's default `byte[]` → base64 conversion. .NET DTO uses `string GroupId`.
- **No partial-failure exception** — `IOException` triggers wholesale failure response; individual per-recipient failures (network / rate-limit) live in `results[].type`. Consumer parses `results[]` to detect partial failures.
- **`URI` value is treated as opaque** to the .NET layer — pass through unchanged.

---

### `updateGroup` — Wave 2

**Source citation:**

- Command: `src/main/java/org/asamk/signal/commands/UpdateGroupCommand.java @ bda4e7fc`
- Manager API builder: `lib/src/main/java/org/asamk/signal/manager/api/UpdateGroup.java @ bda4e7fc`
- Enum types: `lib/src/main/java/org/asamk/signal/manager/api/GroupLinkState.java`, `lib/src/main/java/org/asamk/signal/manager/api/GroupPermission.java @ bda4e7fc`
- Result record: `lib/src/main/java/org/asamk/signal/manager/api/SendGroupMessageResults.java @ bda4e7fc`
- Group ID parsing: `src/main/java/org/asamk/signal/util/CommandUtil.java:75-84 @ bda4e7fc`.

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| account | `account` | `String` | yes (multi-account) | — | E.164 phone; consumed by handler before params parse. |
| group-id (alias: `group`) | `groupId` | `String` (base64 of `byte[]`) | **no — when null, command CREATES a new group** | `null` | `subparser.addArgument("-g", "--group-id", "--group").help("Specify the group ID.")` (line 45). Decoded via `GroupId.fromBase64`. When `null`, command calls `m.createGroup(name, members, avatar)` first, then `m.updateGroup(...)` (`UpdateGroupCommand.java:137-145`). |
| name | `name` | `String` | no | `null` | New group name. Required-shaped on creation (when groupId null) but argparse4j does NOT enforce it as required. Backend `m.createGroup` may accept null. |
| description | `description` | `String` | no | `null` | New group description. |
| avatar | `avatar` | `String` (file path) | no | `null` | Local file path to avatar image. **PII concern:** file path — do not log at `Information+`. |
| member (alias plural: `members`) | `members` | `List<String>` | no | `null` (Java) / empty set | E.164 phones / UUIDs / usernames to add. `nargs("*")`. Parsed via `CommandUtil.getSingleRecipientIdentifiers`. |
| remove-member (alias plural: `removeMembers`) | `removeMembers` | `List<String>` | no | `null` / empty set | Members to remove. `nargs("*")`. |
| admin (alias plural: `admins`) | `admins` | `List<String>` | no | `null` / empty set | Members to promote to admin. `nargs("*")`. |
| remove-admin (alias plural: `removeAdmins`) | `removeAdmins` | `List<String>` | no | `null` / empty set | Members to demote from admin. `nargs("*")`. |
| ban (alias plural: `bans`) | `bans` | `List<String>` | no | `null` / empty set | Members to ban. `nargs("*")`. |
| unban (alias plural: `unbans`) | `unbans` | `List<String>` | no | `null` / empty set | Members to unban. `nargs("*")`. |
| reset-link | `resetLink` | `boolean` | no | `false` | `Arguments.storeTrue()` — flag-style. Resets group link + creates new link password. |
| link | `link` | `String` | no | `null` | Group link state. `choices("enabled", "enabled-with-approval", "disabled")`. Mapped via `getGroupLinkState` (line 82-92). **Accepts both kebab AND camelCase**: `"enabled-with-approval"` OR `"enabledWithApproval"` both → `GroupLinkState.ENABLED_WITH_APPROVAL` (line 88). |
| set-permission-add-member | `setPermissionAddMember` | `String` | no | `null` | `choices("every-member", "only-admins")`. Mapped via `getGroupPermission` (line 94-103). **Accepts both kebab AND camelCase**: `"every-member"`/`"everyMember"` → `GroupPermission.EVERY_MEMBER`; `"only-admins"`/`"onlyAdmins"` → `GroupPermission.ONLY_ADMINS`. |
| set-permission-edit-details | `setPermissionEditDetails` | `String` | no | `null` | Same `choices("every-member", "only-admins")` + same dual-form acceptance. |
| set-permission-send-messages | `setPermissionSendMessages` | `String` | no | `null` | Same `choices("every-member", "only-admins")` + same dual-form acceptance. **Implementation detail:** server-side this is normalized to `isAnnouncementGroup` boolean — `ONLY_ADMINS` → `true`, `EVERY_MEMBER` → `false`, null → null (`UpdateGroupCommand.java:163-165`). |
| expiration | `expiration` | `Integer` (seconds) | no | `null` | `subparser.addArgument("-e", "--expiration").type(int.class)` (line 77). Message-expiration timer in seconds. |
| member-label-emoji | `memberLabelEmoji` | `String` | no | `null` | Custom emoji for member label. |
| member-label | `memberLabel` | `String` | no | `null` | Custom string for member label. |

**Result (response) wire shape:**

Result is a `HashMap<String, Object>` written via `JsonWriter.write` —
`UpdateGroupCommand.java:206-217`. All fields are conditionally added (may be
absent):

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| timestamp | `timestamp` | `long` | Only present when `results != null` (i.e. when send actually happened). Send timestamp epoch ms. |
| results | `results` | `List<JsonSendMessageResult>` | Per-recipient send outcomes. Only present when `results != null`. See join's shape. **When updating a new group:** results concatenates `createGroup` + `updateGroup` send results into a single list (`UpdateGroupCommand.java:172-175`). |
| groupId | `groupId` | `String` (base64) | **ONLY present when a new group was just created** — i.e. request omitted `groupId`. Absent on plain updates of existing groups. Encoded via `GroupId.toBase64()`. |

**Validation rules** (UserErrorException + UnexpectedErrorException throws):

- `"Invalid group id: " + e.getMessage()` коли `GroupId.fromBase64(groupId)` кидає `GroupIdFormatException` (`CommandUtil.java:80-83`).
- `"Invalid phone number '" + s + "': " + e.getMessage()` коли елемент member/remove-member/admin/remove-admin/ban/unban — invalid number (`CommandUtil.java:106-108`).
- `"Invalid group link state: " + value` коли `link` value не входить у `{enabled, enabled-with-approval, enabledWithApproval, disabled}` (`UpdateGroupCommand.java:90`). **Note:** argparse4j's `.choices(...)` validates kebab-case forms (`enabled-with-approval`) at CLI level. But the JSON-RPC layer skips argparse `Subparser` execution (only the camelCase Namespace is built) so this server-side fallthrough catches camelCase aliases like `enabledWithApproval`.
- `"Invalid group permission: " + value` коли `setPermissionAddMember`/`setPermissionEditDetails`/`setPermissionSendMessages` value не у `{every-member, everyMember, only-admins, onlyAdmins}` (`UpdateGroupCommand.java:101`).
- `"Failed to add avatar attachment for group\": " + e.getMessage()` коли `m.updateGroup` кидає `AttachmentInvalidException` (`UpdateGroupCommand.java:178-179`). Note the literal-string typo `"group\""` (extra backslash-quote at end) — preserved verbatim from source.
- `e.getMessage()` (raw) коли `m.updateGroup` кидає `GroupNotFoundException` | `NotAGroupMemberException` | `GroupSendingNotAllowedException` (`UpdateGroupCommand.java:180-181`).
- `"The user " + sender.getIdentifier() + " is not registered."` коли `m.updateGroup` кидає `UnregisteredRecipientException` (`UpdateGroupCommand.java:182-183`).
- `"Failed to send message: " + msg + " (" + className + ")"` — UnexpectedErrorException — коли `m.updateGroup` кидає `IOException` (`UpdateGroupCommand.java:184-186`). **Note:** maps to `-32603 InternalError`, NOT `-3 IoError` — this differs from `joinGroup`/`quitGroup`.

**Error codes specific to this method:**

- `-1 UserError` — invalid group id format, invalid phone, invalid link/permission enum value, attachment invalid, group not found, not a group member, group sending not allowed, unregistered recipient.
- `-32603 InternalError` — IOException during send (wrapped in `UnexpectedErrorException` — note this differs from `joinGroup`/`quitGroup` which use `-3 IoError`).
- `-32600 InvalidRequest` — JSON-mapping failure on params.
- `-32602 InvalidParams` — missing/unknown account.

**Side-effects:**

- **Mutates group state.** Adds/removes members, admins, bans; updates name/description/avatar/permissions/expiration timer.
- **Triggers send** of group-update message to all existing group members.
- **Creates new group** when `groupId` omitted — calls `m.createGroup(name, members, avatar)` first, then `m.updateGroup(...)` with remaining fields (`UpdateGroupCommand.java:137-145`). On creation path `name`/`members`/`avatar` are zeroed out before the subsequent `updateGroup` call (lines 142-144) to prevent double-apply.
- **`resetLink = true` regenerates the link password.**
- Subsequent `listGroups` reflects new state.

**Enum values used:**

- **`GroupLinkState`** (`GroupLinkState.java:3-7 @ bda4e7fc`): `ENABLED` | `ENABLED_WITH_APPROVAL` | `DISABLED`.
  - Wire-input kebab-case: `enabled` | `enabled-with-approval` | `disabled` (per argparse `.choices`).
  - Wire-input camelCase alias: `enabledWithApproval` (also accepted server-side).
  - Expose as .NET enum `SignalCli.Models.Signal.Groups.GroupLinkState` with values `Enabled` | `EnabledWithApproval` | `Disabled`. JSON converter maps Java kebab-case ↔ .NET PascalCase via explicit string table (kebab is canonical wire form).
- **`GroupPermission`** (`GroupPermission.java:3-6 @ bda4e7fc`): `EVERY_MEMBER` | `ONLY_ADMINS`.
  - Wire-input kebab-case: `every-member` | `only-admins`.
  - Wire-input camelCase alias: `everyMember` | `onlyAdmins`.
  - Expose as .NET enum `SignalCli.Models.Signal.Groups.GroupPermission` with `EveryMember` | `OnlyAdmins`.
- `JsonSendMessageResult.Type` (result-side, same as `joinGroup`).

**Quirks / surprises:**

- **`groupId == null` triggers CREATE.** The same method does both CREATE (when groupId omitted) and UPDATE (when groupId provided). .NET surface should mirror this: either single `UpdateGroupAsync(groupId?, opts)` overload or split `CreateGroupAsync(opts)` + `UpdateGroupAsync(groupId, opts)`. Existing `ISignalGroups` already exposes `ListGroupsAsync`; pick a shape consistent with how send-side methods are split.
- **`sendMessages` permission is special** — translated to `isAnnouncementGroup` boolean by the command itself (`UpdateGroupCommand.java:163-165`); upstream `UpdateGroup` API record doesn't have a `sendMessagesPermission` field, it has `isAnnouncementGroup` (Boolean tri-state). DTO design: keep `sendMessagesPermission` as `GroupPermission?` to mirror the wire — the boolean conversion is upstream's concern.
- **Enum-value aliasing — server accepts BOTH kebab AND camelCase**, but argparse4j's CLI `.choices` only accepts kebab. .NET wire output SHOULD use kebab (canonical CLI form, future-stable). Receiving from signal-cli is not applicable here — these are input-only.
- **`expiration` is `Integer`, not `int`** (`UpdateGroup.java:20` — `private final Integer expirationTimer`). Null distinguishes "leave timer unchanged" from "set timer to 0 (disabled)". `ns.getInt("expiration")` in `UpdateGroupCommand.java:127` returns `Integer` (nullable).
- **`resetLink` is `boolean`, not `Boolean`** (`UpdateGroup.java:15`). `false` = "do not reset"; there's no tri-state. Reset is one-shot — sending `resetLink=true` regenerates link password each time.
- **All list fields are `Set<RecipientIdentifier.Single>`** in `UpdateGroup` builder (`UpdateGroup.java:9-14`) — duplicates dropped. JSON-array input order is not preserved server-side; if duplicates passed, only one applied.
- **`results[]` may be a concat of two send-batches** when creating: the create-group message AND the update-group message (`UpdateGroupCommand.java:172-175`). Consumers should iterate, not assume single batch.
- **`groupId` field in response is dimorphic in presence** — present only on creation, absent on update. Same `bool?` / nullable-string pattern as joinGroup's `onlyRequested`.
- **Literal-string typo `"group\""` in `AttachmentInvalidException` message** (`UpdateGroupCommand.java:179`) — verify against signal-cli before reporting upstream; do NOT correct in .NET test expectations.
- **`avatar` is a server-side file path**, not bytes / base64 / data-URI. signal-cli reads the file from the filesystem at the path. For .NET consumers running signal-cli in a separate process, the path MUST be accessible from the signal-cli process's working directory / FS scope. Document this as a quirk for the .NET surface (cannot pass remote bytes directly).
- **`groupId` is base64-encoded `byte[]`** — `GroupId.fromBase64` parses it (`CommandUtil.java:80`); `GroupId.toBase64()` writes it back in the response (`UpdateGroupCommand.java:214`). Wire is always `String`. Note: Jackson default for `byte[]` is base64, but here the conversion is explicit through `GroupId` helper, NOT Jackson's `byte[]` auto-conversion.

---

### `quitGroup` — Wave 2

**Source citation:**

- Command: `src/main/java/org/asamk/signal/commands/QuitGroupCommand.java @ bda4e7fc`
- Manager API: `Manager.quitGroup(GroupId, Set<RecipientIdentifier.Single>)` returns `SendGroupMessageResults` (called at `QuitGroupCommand.java:57`).
- Result writer: `src/main/java/org/asamk/signal/util/SendMessageResultUtils.java:29-41 @ bda4e7fc` — `outputResult(OutputWriter, SendGroupMessageResults)`.
- Group ID parsing: `src/main/java/org/asamk/signal/util/CommandUtil.java:75-84 @ bda4e7fc`.

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| account | `account` | `String` | yes (multi-account) | — | E.164 phone; consumed by handler before params parse. |
| group-id (alias: `group`) | `groupId` | `String` (base64) | yes | — | `subparser.addArgument("-g", "--group-id", "--group").required(true)` (line 36). Decoded via `GroupId.fromBase64`. |
| delete | `delete` | `boolean` | no | `false` | `Arguments.storeTrue()`. If true, locally delete group data after quitting (`m.deleteGroup` called — `QuitGroupCommand.java:63-65`). |
| admin (alias plural: `admins`) | `admins` | `List<String>` | no | `null` / empty set | New admin(s) to promote. **Required if currently the only admin** — server-side raises `LastGroupAdminException` if missing. `nargs("*")` (line 40-42). |

**Result (response) wire shape:**

Result is a `Map<String, Object>` via `SendMessageResultUtils.outputResult(OutputWriter, SendGroupMessageResults)` (`SendMessageResultUtils.java:29-41`). **Note:** when `NotAGroupMemberException` is thrown (user already not in group), the command logs `"User is not a group member"` and **emits NO response object** at all — the `result[0]` remains null and the JSON-RPC layer returns `{}` (empty object) — `SignalJsonRpcCommandHandler.java:281`.

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| timestamp | `timestamp` | `long` | Send timestamp (epoch ms). Always present when quit-message sent. |
| results | `results` | `List<JsonSendMessageResult>` | Per-recipient send outcomes for the quit message to remaining group members. Always present when quit-message sent. See join's `JsonSendMessageResult` shape. |

**Validation rules** (UserErrorException + log-only throws):

- `"Invalid group id: " + e.getMessage()` коли `GroupId.fromBase64(groupId)` кидає `GroupIdFormatException` (`CommandUtil.java:80-83`).
- `"Invalid phone number '" + s + "': " + e.getMessage()` коли елемент admin — invalid number (`CommandUtil.java:106-108`).
- `"Failed to send to group: " + e.getMessage()` коли `m.quitGroup` кидає `GroupNotFoundException` (`QuitGroupCommand.java:72-73`).
- `"You need to specify a new admin with --admin: " + e.getMessage()` коли кидає `LastGroupAdminException` (`QuitGroupCommand.java:74-75`).
- `"The user " + sender.getIdentifier() + " is not registered."` коли кидає `UnregisteredRecipientException` (`QuitGroupCommand.java:76-77`).
- `"Failed to send message: " + msg + " (" + className + ")"` — IOErrorException — коли кидає `IOException` (`QuitGroupCommand.java:66-71`).
- **No exception** — silent log `"User is not a group member"` when `NotAGroupMemberException` thrown (`QuitGroupCommand.java:59-61`). Returns empty `{}` JSON object.

**Error codes specific to this method:**

- `-1 UserError` — invalid group id, invalid admin phone, group not found, last admin without specified successors, unregistered admin.
- `-3 IoError` — IOException during send.
- `-32603 InternalError` — uncaught Throwable.
- `-32600 InvalidRequest` — JSON-mapping failure.
- `-32602 InvalidParams` — missing/unknown account.

**Side-effects:**

- **Removes self from group member list** (or no-op if already not a member — `NotAGroupMemberException` silently logged).
- **Promotes specified admin(s) before quitting** if `admins` provided and user is currently sole admin.
- **Triggers send** of quit message to remaining group members.
- **If `delete=true`:** locally deletes group data (`m.deleteGroup(groupId)`) after the quit message is sent. **Critical:** this happens INSIDE the `try` block (`QuitGroupCommand.java:62-65`) — local delete is attempted regardless of whether the quit-send raised `NotAGroupMemberException` (since that's silently caught at line 59). The local delete is NOT attempted if the outer `IOException`/`GroupNotFoundException`/`LastGroupAdminException`/`UnregisteredRecipientException` fires (control flows out of the try block before reaching the delete check).

**Enum values used:** None at input layer; `JsonSendMessageResult.Type` at result layer (same as joinGroup).

**Quirks / surprises:**

- **`NotAGroupMemberException` is silently swallowed** — only logged at `INFO` level via `logger.info("User is not a group member")` (`QuitGroupCommand.java:59-61`). The JSON-RPC response is then `{}` (empty object) — `SignalJsonRpcCommandHandler.java:281` (`result[0] == null ? Map.of() : result[0]`). **Implementation note:** .NET surface SHOULD treat this as idempotent success — do NOT throw `InvalidOperationException`; document that `QuitGroupAsync` on a group the user is not a member of returns a response with no `timestamp`/`results`. Match the same idempotent contract as `SignalEventService.SubscribeAsync` per CLAUDE.md rule #14.
- **`delete=true` triggers a SECOND local-only operation** after the quit-send — `m.deleteGroup(groupId)`. There is no separate "deleteLocalGroup" RPC; if a consumer wants only-delete-without-quit, signal-cli currently has no path for that. Document this when designing the .NET options shape.
- **`admins` parameter is conditionally required** — the argparse4j-level required flag is `false` (line 40-42 — no `.required(true)`), but the server-side `LastGroupAdminException` enforces it dynamically when the caller is the only admin. .NET wrapper should NOT statically require `admins` on the options DTO; surface the runtime error as a typed exception or pass-through `UserErrorException` with the original message verbatim so the user can react.
- **`groupId` is required** (unlike updateGroup) — `subparser.addArgument(...).required(true)` (line 36). Validation gate is argparse4j-level; missing groupId in JSON-RPC params manifests as a Jackson-mapping or null-handling path. The Java code uses `ns.getString("group-id")` and `CommandUtil.getGroupId(null)` returns `null` — server-side, `m.quitGroup(null, ...)` would likely NPE. Surface as a server-side `-32600 InvalidRequest` or `-32603 InternalError`; do NOT rely on .NET-side `Required` validation alone — quirk-document that signal-cli's behavior with `groupId == null` is implementation-defined (not explicitly checked in command).
- **No `groupId` in response** — unlike `updateGroup`, `quitGroup` never emits a `groupId` field in its response (`SendMessageResultUtils.outputResult` line 38 writes only `timestamp` + `results`).
- Same base64-`byte[]` quirk as updateGroup: `groupId` is `GroupId.fromBase64` parsed; wire is `String`.

---

## Verification

The following Java source files were read in full or in relevant ranges
at pinned commit `bda4e7fc`:

- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/JoinGroupCommand.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/UpdateGroupCommand.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/QuitGroupCommand.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/JsonRpcLocalCommand.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/JsonRpcNamespace.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/jsonrpc/SignalJsonRpcCommandHandler.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/json/JsonSendMessageResult.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/util/CommandUtil.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/util/SendMessageResultUtils.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/util/Util.java` (lines 30-50, `dashSeparatedToCamelCaseString`)
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/api/GroupLinkState.java`
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/api/GroupPermission.java`
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/api/UpdateGroup.java`
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/api/SendGroupMessageResults.java`

No file in the signal-cli directory was modified — read-only research.
