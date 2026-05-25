# Wave 4 — Sticker Packs + Binary Resource Fetch

Per `tasks.md §0.5` anti-hallucination protocol. All citations against
`signal-cli @ bda4e7fc` (pinned in `research/README.md`).

Scope: 6 send-side RPCs + 1 receive-side sync-event decoder investigation.

---

### `uploadStickerPack` — Wave 4

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/UploadStickerPackCommand.java @ bda4e7fc` (lines 1–59)
- Manager API: `lib/src/main/java/org/asamk/signal/manager/Manager.java:331` — `StickerPackUrl uploadStickerPack(File path) throws IOException, StickerPackInvalidException;`
- Return wrapper: `lib/src/main/java/org/asamk/signal/manager/api/StickerPackUrl.java @ bda4e7fc` (lines 14, 48–60) — `record StickerPackUrl(StickerPackId packId, byte[] packKey)`; `getUrl()` builds `https://signal.art/addstickers/#pack_id=<hex>&pack_key=<hex>`.

**Params (request) wire shape:**

| Field    | JSON name | Java type | Required? | Default | Notes |
|----------|-----------|-----------|-----------|---------|-------|
| `path`   | `path`    | `String`  | yes       | —       | argparse positional argument (`subparser.addArgument("path")`, line 33). Path to manifest.json OR a `.zip` containing the sticker pack (line 34 help text). Resolved as `new File(ns.getString("path"))` line 43. |

**Result (response) wire shape:**

JsonWriter branch (line 49–51): `writer.write(Map.of("url", url.getUrl()))`. The Map is serialized by Jackson `valueToTree` → flat JSON object with single key:

| Field | JSON name | Java type | Notes |
|-------|-----------|-----------|-------|
| `url` | `url`     | `String`  | The full `https://signal.art/addstickers/#pack_id=<hex>&pack_key=<hex>` URL built from the returned `StickerPackUrl` via `getUrl().toString()` (via `Map.of` → Jackson serializes `URI` as its `toString()`). The packId + packKey are NOT separately returned — extract them from the URL fragment if needed. |

**Validation rules** (UserErrorException throws):
- `"Invalid sticker pack: " + e.getMessage()` (line 56) — when `Manager.uploadStickerPack` throws `StickerPackInvalidException` (e.g. manifest missing required fields, sticker count exceeds limit, sticker file too large).

**Error codes specific to this method:**
- `-1 UserError` — `StickerPackInvalidException` rethrown as `UserErrorException` (line 55–56).
- `-3 IoError` — `IOException` from `Manager.uploadStickerPack` wrapped as `IOErrorException("Upload error (maybe image size too large):" + e.getMessage(), e)` (lines 53–54). Covers network failures + image-size-too-large.

**Side-effects:**
- Network upload to Signal CDN (manifest + each sticker image as separate attachment). After success, the pack is **NOT auto-installed for the uploader**; the user must call `addStickerPack` with the returned URL to install it.

**Enum values used:**
- (none)

**Quirks / surprises:**
- Result is a flat `{ "url": "..." }` object, NOT the `StickerPackUrl` record's `(packId, packKey)` shape — the CLI deliberately collapses to URL-string. Consumer must parse the URL fragment if they need raw packId/packKey bytes.
- `path` is a filesystem path on the signal-cli host, not the calling client — works for local-mode signal-cli only. In daemon-mode where signal-cli runs on a separate machine, the file must be accessible to that machine.

---

### `listStickerPacks` — Wave 4

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/ListStickerPacksCommand.java @ bda4e7fc` (lines 1–80)
- Manager API: `lib/src/main/java/org/asamk/signal/manager/Manager.java:335` — `List<StickerPack> getStickerPacks();` (no throws).
- Domain record: `lib/src/main/java/org/asamk/signal/manager/api/StickerPack.java @ bda4e7fc` (lines 6–21).

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|-------|-----------|-----------|-----------|---------|-------|
|       |           |           |           |         | (no arguments — `attachToSubparser` only sets `subparser.help(...)`, no `addArgument` calls.) |

**Result (response) wire shape:**

`List<JsonStickerPack>` — JSON array of pack objects. `JsonStickerPack` is a private record defined in `ListStickerPacksCommand.java:53-78`:

| Field        | JSON name      | Java type            | Notes |
|--------------|----------------|----------------------|-------|
| `packId`     | `packId`       | `String`             | Lowercase-hex encoding of the 16-byte `StickerPackId.serialize()` via `Hex.toStringCondensed` (line 64). E.g. `"a1b2c3..."`. |
| `url`        | `url`          | `String`             | `stickerPack.url().getUrl().toString()` — full `https://signal.art/addstickers/#pack_id=...&pack_key=...` URL (line 65). |
| `installed`  | `installed`    | `boolean`            | Whether THIS account has installed the pack (line 66). |
| `title`      | `title`        | `String`             | Empty string if unknown (line 67; `StickerPack` ctor defaults to `""` line 17). |
| `author`     | `author`       | `String`             | Empty string if unknown (line 68; same default). |
| `cover`      | `cover`        | `JsonSticker \| null` | Cover sticker (line 69). `Optional<Sticker>` → `null` if absent. |
| `stickers`   | `stickers`     | `List<JsonSticker>`  | Full sticker list — empty `[]` if manifest not yet downloaded (line 70; ctor defaults to `List.of()` line 17). |

Nested `JsonSticker` (lines 73–78):

| Field         | JSON name     | Java type | Notes |
|---------------|---------------|-----------|-------|
| `id`          | `id`          | `int`     | Sticker index within pack (0-based). |
| `emoji`       | `emoji`       | `String`  | Emoji associated with this sticker (e.g. `"😀"`). |
| `contentType` | `contentType` | `String`  | MIME type, e.g. `"image/webp"`. |

**Validation rules** (UserErrorException throws):
- (none — `getStickerPacks` is a pure read of local sticker store)

**Error codes specific to this method:**
- (none beyond global `-32603 INTERNAL_ERROR` for unexpected throws)

**Side-effects:**
- Read-only.

**Enum values used:**
- (none)

**Quirks / surprises:**
- `title`/`author` can be empty strings (not `null`) per `StickerPack` constructor default (line 17 of `StickerPack.java`).
- `stickers` can be empty `[]` when the local store knows the pack exists (via incoming sync) but the manifest has not yet been fetched from CDN.
- `cover` is the only field that may be `null` (via `Optional<Sticker>::map ::orElse(null)`).

---

### `addStickerPack` — Wave 4

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/AddStickerPackCommand.java @ bda4e7fc` (lines 1–64)
- Manager API: `lib/src/main/java/org/asamk/signal/manager/Manager.java:333` — `void installStickerPack(StickerPackUrl url) throws IOException;`
- URL parser: `lib/src/main/java/org/asamk/signal/manager/api/StickerPackUrl.java:19-46` — `StickerPackUrl.fromUri(URI uri) throws InvalidStickerPackLinkException`.

**Params (request) wire shape:**

| Field | JSON name | Java type     | Required? | Default | Notes |
|-------|-----------|---------------|-----------|---------|-------|
| `uri` | `uri`     | `List<String>` (one or more) | yes | — | argparse: `--uri ... +` (line 31–34); `nargs("+")` means at least one. Format: `https://signal.art/addstickers/#pack_id=XXX&pack_key=XXX` (line 34 help). signal-cli loops through each (lines 43–62) — failure on ANY uri aborts the whole call without rollback of already-installed packs. |

**Result (response) wire shape:**

`null` / empty `{}` — the command does NOT write to `outputWriter` on success. `SignalJsonRpcCommandHandler.runCommand` (line 281–282) returns `Map.of()` when `result[0]` is null, so the JSON-RPC result is `{}`.

| Field | JSON name | Java type | Notes |
|-------|-----------|-----------|-------|
| —     | —         | —         | (empty object on success) |

**Validation rules** (UserErrorException throws):
- `"Sticker pack uri has invalid format: " + e.getMessage()` (line 49) — when `new URI(uri)` throws `URISyntaxException`.
- `"Invalid sticker pack link"` (line 60) — when `StickerPackUrl.fromUri(stickerUri)` throws `InvalidStickerPackLinkException` (raised by `StickerPackUrl.java:21,30,37,43` for: empty fragment, missing `pack_id`/`pack_key` query params, malformed hex in either value).

**Error codes specific to this method:**
- `-1 UserError` — both URL-parse failures (above) map to `UserErrorException`.
- `-3 IoError` — `IOException` from `Manager.installStickerPack` (manifest fetch failure) → `IOErrorException("Install sticker pack failed", e)` (line 57).

**Side-effects:**
- Marks the pack as `installed=true` in local sticker store.
- Fetches manifest + sticker metadata from CDN if not already cached.
- Sends a `stickerPackOperation` sync message to linked devices (so they auto-install too). The sync message has type `INSTALL`.

**Enum values used:**
- (none on wire — internal `StickerPackOperationMessage.Type.INSTALL` per `IncomingMessageHandler.java:668`)

**Quirks / surprises:**
- Accepts MULTIPLE URIs in a single call (`nargs("+")`). The .NET wrapper SHOULD expose either `string` (single) overload OR `IReadOnlyList<string>` — preferred shape is the list for parity. Note: signal-cli aborts on first failure, not all-or-nothing semantics.
- No matching `removeStickerPack` / `uninstallStickerPack` RPC exists in signal-cli (verified by `grep` of `commands/` directory — no such command class).

---

### `getAttachment` — Wave 4

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/GetAttachmentCommand.java @ bda4e7fc` (lines 1–61)
- Manager API: `lib/src/main/java/org/asamk/signal/manager/Manager.java:410` — `InputStream retrieveAttachment(final String id) throws IOException;`
- Result envelope: `src/main/java/org/asamk/signal/json/JsonAttachmentData.java @ bda4e7fc` — `record JsonAttachmentData(String data)`.

**Params (request) wire shape:**

| Field        | JSON name     | Java type | Required?    | Default | Notes |
|--------------|---------------|-----------|--------------|---------|-------|
| `id`         | `id`          | `String`  | yes          | —       | argparse `--id` `.required(true)` (line 30). The attachment's stored ID (filename within signal-cli's attachment cache). |
| `recipient`  | `recipient`   | `String`  | yes (XOR `group-id`) | — | argparse `--recipient` in a mutually-exclusive group `.required(true)` (lines 31–32). E.164 phone or UUID of the sender. NOTE: passed to argparse but the value is NOT actually used in `handleCommand` — `getAttachment` only takes `id`, the recipient/group-id args appear to be vestigial argparse-noise. |
| `groupId`    | `group-id` (Jackson alias) | `String` | yes (XOR `recipient`) | — | argparse `-g`/`--group-id` (line 33). Same vestigial-noise note as above. |

Note on Jackson naming: when called through JSON-RPC, argparse argument names with hyphens (`group-id`) are mapped via a Jackson-aware converter. Confirm wire-name when implementing — typical signal-cli convention is to send `groupId` (camelCase) on the JSON-RPC wire. The .NET wrapper should expose `GroupId` and serialize via `[JsonPropertyName("groupId")]`.

**Result (response) wire shape:**

`JsonAttachmentData` envelope:

| Field  | JSON name | Java type | Notes |
|--------|-----------|-----------|-------|
| `data` | `data`    | `String`  | Base64-encoded attachment bytes (RFC 4648 standard, NOT URL-safe — `Base64.getEncoder()` per line 50). Raw `InputStream.readAllBytes()` then base64-encoded. |

**Validation rules** (UserErrorException throws):
- `"Missing attachment id parameter"` (line 45) — when `ns.getString("id")` is null. (Defensive; argparse `.required(true)` should already enforce this.)
- `"Could not find attachment with ID: " + id` (line 56) — when `Manager.retrieveAttachment` throws `FileNotFoundException` (attachment not in local cache).

**Error codes specific to this method:**
- `-1 UserError` — missing id / attachment-not-found.
- `-32603 INTERNAL_ERROR` — generic `IOException` while reading the cached file → `UnexpectedErrorException("An error occurred reading attachment: " + id, ex)` (line 58) → mapped by `SignalJsonRpcCommandHandler.java:267-272` to `INTERNAL_ERROR`.

**Side-effects:**
- Read-only.
- The attachment must already exist in signal-cli's local attachment cache (downloaded during a prior `receive`). This RPC does NOT trigger a re-download — for re-fetch, the consumer must re-process the original `dataMessage.attachments[].id` reference (typically via the upstream message).

**Enum values used:**
- (none)

**Quirks / surprises:**
- The `recipient` / `group-id` arguments are required by argparse (mutually-exclusive group with `.required(true)`) but NOT read by `handleCommand` — the body only reads `id`. This is upstream-noise; the .NET wrapper MAY omit them or pass dummies. **Recommendation: omit on the .NET surface** unless integration testing proves signal-cli's JSON-RPC parser rejects calls without them.
- Base64 encoding uses STANDARD alphabet (with `+`/`/`), NOT URL-safe — match this in `Convert.FromBase64String` (which accepts standard).

---

### `getAvatar` — Wave 4

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/GetAvatarCommand.java @ bda4e7fc` (lines 1–78)
- Manager API:
  - `lib/.../Manager.java:412` — `InputStream retrieveContactAvatar(final RecipientIdentifier.Single recipient) throws IOException, UnregisteredRecipientException;`
  - `lib/.../Manager.java:414` — `InputStream retrieveProfileAvatar(final RecipientIdentifier.Single recipient) throws IOException, UnregisteredRecipientException;`
  - `lib/.../Manager.java:416` — `InputStream retrieveGroupAvatar(final GroupId groupId) throws IOException;`
- Result envelope: `src/main/java/org/asamk/signal/json/JsonAttachmentData.java` — `record JsonAttachmentData(String data)`.

**Params (request) wire shape:**

Mutually-exclusive group `.required(true)` (line 32) — exactly ONE of these MUST be provided:

| Field      | JSON name  | Java type | Required?         | Default | Notes |
|------------|------------|-----------|-------------------|---------|-------|
| `contact`  | `contact`  | `String`  | XOR (one of 3)    | —       | argparse `-c`/`--contact` (line 33). E.164 phone or UUID. Retrieves the contact's avatar from the local contact-card (set via contacts-sync). |
| `profile`  | `profile`  | `String`  | XOR (one of 3)    | —       | argparse `-p`/`--profile` (line 34). E.164 or UUID. Retrieves the recipient's profile avatar (server-fetched). |
| `groupId`  | `group-id` (argparse) → likely `groupId` on JSON-RPC wire | `String` | XOR (one of 3) | — | argparse `-g`/`--group-id` (line 35). Base64-encoded group id. |

**Result (response) wire shape:**

`JsonAttachmentData` envelope (same as `getAttachment`):

| Field  | JSON name | Java type | Notes |
|--------|-----------|-----------|-------|
| `data` | `data`    | `String`  | Base64-encoded avatar image bytes (typically `image/jpeg` or `image/webp`). Encoded via `Base64.getEncoder()` (line 69). |

**Validation rules** (UserErrorException throws):
- `"Could not find avatar"` (line 60) — when any of the three `retrieve*Avatar` methods throws `FileNotFoundException` (avatar not in local cache / never set).
- `"The user " + e.getSender().getIdentifier() + " is not registered."` (line 64) — when `UnregisteredRecipientException` is thrown (contact/profile avatar paths only — group path does not throw this).

**Error codes specific to this method:**
- `-1 UserError` — avatar not found / unregistered recipient.
- `-32603 INTERNAL_ERROR` — generic `IOException` while reading cached file (line 62 OR line 75) → `UnexpectedErrorException("An error occurred reading avatar", ex)`. Two separate try/catch blocks both map to the same message.

**Side-effects:**
- Read-only.
- Avatar must be in local cache. Profile avatars are fetched lazily; if not yet downloaded, `FileNotFoundException` → `-1 UserError`. There is NO eager-fetch RPC; consumers should ensure the relevant profile/contact sync has occurred before calling.

**Enum values used:**
- (none)

**Quirks / surprises:**
- Exactly ONE of `contact`/`profile`/`groupId` MUST be set — argparse enforces this at parse time (mutually-exclusive `.required(true)`). The .NET wrapper SHOULD model these as a discriminated union OR validate "exactly one of" at the client side to avoid noisy `INVALID_REQUEST` round-trips.
- `contact` vs `profile` semantic difference: `contact` reads the avatar that the user has set in their LOCAL contact list (synced from another linked device's address book), while `profile` reads the recipient's PUBLIC profile avatar (Signal-server-stored). Document this distinction in the .NET API surface.

---

### `getSticker` — Wave 4

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/GetStickerCommand.java @ bda4e7fc` (lines 1–62)
- Manager API: `lib/src/main/java/org/asamk/signal/manager/Manager.java:418` — `InputStream retrieveSticker(final StickerPackId stickerPackId, final int stickerId) throws IOException;`
- Result envelope: `src/main/java/org/asamk/signal/json/JsonAttachmentData.java` — `record JsonAttachmentData(String data)`.

**Params (request) wire shape:**

| Field       | JSON name      | Java type | Required? | Default | Notes |
|-------------|----------------|-----------|-----------|---------|-------|
| `packId`    | `pack-id` (argparse) → likely `packId` on JSON-RPC wire | `String` | yes | — | argparse `--pack-id` `.required(true)` (line 32). Lowercase-hex string (decoded via `Hex.toByteArray` line 43) → exactly 16 bytes after decoding (`StickerPackId.deserialize`). Match format from `listStickerPacks.packId`. |
| `stickerId` | `sticker-id` (argparse) → likely `stickerId` on JSON-RPC wire | `int` | yes | — | argparse `--sticker-id` `.type(int.class).required(true)` (line 33). 0-based sticker index within pack. |

**Result (response) wire shape:**

`JsonAttachmentData` envelope:

| Field  | JSON name | Java type | Notes |
|--------|-----------|-----------|-------|
| `data` | `data`    | `String`  | Base64-encoded sticker image bytes (typically `image/webp`). Encoded via `Base64.getEncoder()` (line 48). |

**Validation rules** (UserErrorException throws):
- `"Could not find sticker with ID: " + stickerId + " in pack " + packId` (line 54) — when `Manager.retrieveSticker` throws `FileNotFoundException`. The stringified `packId` here is `StickerPackId.toString()`, not the original hex — match this when surfacing the message.

**Error codes specific to this method:**
- `-1 UserError` — sticker-not-found.
- `-32603 INTERNAL_ERROR` — `IOException` from reading cached file → `UnexpectedErrorException("An error occurred reading sticker with ID: " + stickerId + " in pack " + packId, ex)` (lines 56–59).
- **No explicit "invalid hex" guard** — `Hex.toByteArray(ns.getString("pack-id"))` (line 43) will throw `IOException` for malformed hex, which propagates uncaught to `SignalJsonRpcCommandHandler` and becomes `-32603 INTERNAL_ERROR`. The .NET wrapper SHOULD validate hex client-side and throw `ArgumentException` to avoid the noisier `-32603`.

**Side-effects:**
- Read-only.
- Requires the sticker pack to be already known to the local store (installed or seen via incoming sticker message). If pack is unknown, `FileNotFoundException`.

**Enum values used:**
- (none)

**Quirks / surprises:**
- `packId` decoded via `Hex.toByteArray` → must be valid lowercase-hex of 16 bytes (32 hex chars). Validate client-side.
- `stickerId` is `int`, not `long` or `String` — bounds are sticker-pack manifest length (typically 0..199 max).

---

### Receive-side: `JsonSyncMessage` sticker-pack-install event — investigation result

**Source citations:**
- `src/main/java/org/asamk/signal/json/JsonSyncMessage.java @ bda4e7fc` (lines 1–67) — the wire-record exposed to JSON-RPC consumers.
- `lib/src/main/java/org/asamk/signal/manager/api/MessageEnvelope.java:651-685 @ bda4e7fc` — domain `Sync` record.
- `lib/src/main/java/org/asamk/signal/manager/helper/IncomingMessageHandler.java:659-672 @ bda4e7fc` — where signal-cli internally processes sticker-pack-operation sync messages.

**Finding: NO sticker-pack-operation field is exposed on the JSON-RPC `JsonSyncMessage` wire surface.**

`JsonSyncMessage` (full surface, line 21–28):

```java
record JsonSyncMessage(
    @JsonInclude(JsonInclude.Include.NON_NULL) JsonSyncDataMessage sentMessage,
    @JsonInclude(JsonInclude.Include.NON_NULL) JsonSyncStoryMessage sentStoryMessage,
    @JsonInclude(JsonInclude.Include.NON_NULL) List<String> blockedNumbers,
    @JsonInclude(JsonInclude.Include.NON_NULL) List<String> blockedGroupIds,
    @JsonInclude(JsonInclude.Include.NON_NULL) List<JsonSyncReadMessage> readMessages,
    @JsonInclude(JsonInclude.Include.NON_NULL) JsonSyncMessageType type
) { ... }
```

- Six fields total — no `stickerPackOperations`, no `stickerPacksInstalled`, no equivalent.
- `JsonSyncMessageType` enum (lines 14–18) has exactly three values: `CONTACTS_SYNC`, `GROUPS_SYNC`, `REQUEST_SYNC`. No `STICKER_PACK_OPERATION`.

**Where the sticker-pack-operation actually lives:** `MessageEnvelope.Sync` domain record (line 651–660) does NOT include sticker-pack-operations either:

```java
public record Sync(
    Optional<Sent> sent,
    Optional<Blocked> blocked,
    List<Read> read,
    List<Viewed> viewed,
    Optional<ViewOnceOpen> viewOnceOpen,
    Optional<Contacts> contacts,
    Optional<Groups> groups,
    Optional<MessageRequestResponse> messageRequestResponse
) { ... }
```

**Why:** signal-cli processes sticker-pack-operation sync messages **silently inside `IncomingMessageHandler.handleSyncMessage`** (lines 659–672) — it auto-installs the pack into the local sticker store (`context.getStickerHelper().addOrUpdateStickerPack(...)`) and does NOT bubble the event up to `MessageEnvelope.Sync`. The user's other linked device installs a pack → this signal-cli instance auto-installs it locally → no JSON-RPC `receive` notification fires for the sticker-pack-operation itself.

**Search verification:**
- `grep "stickerPackOperation\|StickerPackOperation"` across `signal-cli/src/main/java/org/asamk/signal/json/` → 0 matches (the entire `json/` directory has no sticker-pack-operation wire DTO).
- `grep "stickerPackOperation\|StickerPackOperation"` across `signal-cli/` → 2 files only: `SyncHelper.java` (outbound sticker-sync, send-side) + `IncomingMessageHandler.java:659-672` (inbound, silent auto-install).

**Implication for `signal-cli-api-coverage`:**

The user-facing spec for Wave 4 SHOULD NOT promise a `StickerPackInstalledEvent` on `ISignalEventService` — there is no upstream wire field to decode. Three options:

1. **(Recommended)** Mark sticker-pack-install events as "out of scope for v1; requires upstream signal-cli change". Cite this research file.
2. Track installed packs by **polling `listStickerPacks`** after every `receive` (high overhead, lossy — a pack uninstalled before the next poll is missed).
3. Patch signal-cli upstream to add `stickerPackOperations` to `JsonSyncMessage` and submit PR. Out of scope for this repo.

The DECISION goes in `design.md`; this research file states the upstream fact.

---

## Verification

Java files read for this wave (read-only, no modifications):

- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/UploadStickerPackCommand.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/ListStickerPacksCommand.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/AddStickerPackCommand.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/GetAttachmentCommand.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/GetAvatarCommand.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/GetStickerCommand.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/json/JsonAttachmentData.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/json/JsonSyncMessage.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/jsonrpc/SignalJsonRpcCommandHandler.java` (error-code constants + exception → JSON-RPC code mapping, lines 39–43, 240–283)
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/Manager.java` (interface signatures, lines 331, 333, 335, 410, 412, 414, 416, 418)
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/api/StickerPack.java`
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/api/StickerPackUrl.java`
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/api/MessageEnvelope.java` (lines 651–685 for `Sync` record; lines 388–393 for inline `Sticker` record on data messages)
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/helper/IncomingMessageHandler.java` (lines 659–672 for silent sticker-pack-operation handling)
