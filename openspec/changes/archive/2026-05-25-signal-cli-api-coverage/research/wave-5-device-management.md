# Wave 5 — Device management (primary-perspective)

Pinned commit: `bda4e7fc` ("Prepare next release", 2026-05-24), tag `v0.14.4.1`
HEAD. signal-cli local clone at `C:\Users\ivank\Нова папка\signal-cli`.

**Perspective convention.** These four methods are **PRIMARY-perspective**: the
account running signal-cli IS the primary device and is managing its own list
of linked (secondary) devices. They will throw `-1 UserError "This command
doesn't work on linked devices."` if invoked while signal-cli is registered AS
a secondary device. This contrasts with the SECONDARY-perspective pair already
shipped (`startLink` / `finishLink`), where this signal-cli instance becomes
the secondary. Both perspectives use disjoint signal-cli command sets; do NOT
conflate `addDevice` (primary adds secondary) with `startLink` (this instance
becomes secondary).

`account` parameter is shared across all four methods. It is required in
multi-account JSON-RPC mode and falls back to the single registered account
in single-account mode — failure mode is
`-32602 INVALID_PARAMS "Method requires valid account parameter"`
(`SignalJsonRpcCommandHandler.java:116-118`).

---

### `addDevice` — Wave 5

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/AddDeviceCommand.java @ bda4e7fc` (lines 22-67)
- Manager API: `lib/src/main/java/org/asamk/signal/manager/api/DeviceLinkUrl.java @ bda4e7fc` (lines 15-58)
- Exception types:
  - `lib/src/main/java/org/asamk/signal/manager/api/InvalidDeviceLinkException.java @ bda4e7fc`
  - `lib/src/main/java/org/asamk/signal/manager/api/DeviceLimitExceededException.java @ bda4e7fc`
  - `lib/src/main/java/org/asamk/signal/manager/api/NotPrimaryDeviceException.java @ bda4e7fc`
- Wire records: none (request-only RPC; result is empty / `null` on success).

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | conditional | — | E.164 phone number; required in multi-account mode, optional in single-account mode (`SignalJsonRpcCommandHandler.java:128`). |
| `uri` | `uri` | `String` | yes | — | `sgnl://linkdevice?uuid=<deviceIdentifier>&pub_key=<base64ECPublicKey>` — the URI **displayed by signal-cli on the secondary** during its `startLink` flow. Parsed via `new URI(String)` at `AddDeviceCommand.java:47`. `DeviceLinkUrl.parseDeviceLinkUri` (line 17) extracts `uuid` + `pub_key` query params (`DeviceLinkUrl.java:24-25`); both must be non-empty and `pub_key` must decode as base64 → 32-byte Curve25519 public key (`DeviceLinkUrl.java:33-42`). |

**Result (response) wire shape:**

`handleCommand` returns `void`. The JSON-RPC dispatcher returns an empty JSON
object `{}` / `null` `result` on success (no `JsonWriter.write(...)` call in
the command body).

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| _(empty)_ | _(empty)_ | — | Success = empty result; failure paths throw mapped `JsonRpcException`. |

**Validation rules** (`UserErrorException` throws → wire `-1 UserError`):

- `"Device link uri has invalid format: <URISyntaxException message>"` when `new URI(ns.getString("uri"))` throws `URISyntaxException` (`AddDeviceCommand.java:49`).
- `"Invalid device link"` (with chained `InvalidDeviceLinkException` cause) when `DeviceLinkUrl.parseDeviceLinkUri` fails — i.e. missing `uuid` / `pub_key` query params, non-base64 `pub_key`, or wrong key length (`AddDeviceCommand.java:58-60`).
- `"Account has too many linked devices already"` (with chained `DeviceLimitExceededException` cause) when server rejects the link because the account already has the max number of linked devices (`AddDeviceCommand.java:61-62`).
- `"This command doesn't work on linked devices."` when this signal-cli instance is itself a secondary device, not the primary (`AddDeviceCommand.java:63-64`).

**Error codes specific to this method:**

- `-1 UserError` — see four message strings above.
- `-3 IoError` — `"Add device link failed"` wrapping any `IOException` from `m.addDeviceLink(deviceLinkUrl)` (network/server issues during the link handshake; `AddDeviceCommand.java:55-57`).
- `-32602 INVALID_PARAMS` — multi-account mode without `account` param (dispatcher-level).
- No `-4 UntrustedIdentity`, `-5 RateLimit`, `-6 CaptchaRejected` paths in this method's throw set.

**Side-effects:**

- **Mutates state.** Server-side: adds a new entry to the account's linked-device list. Local-side: triggers a device-list refresh on next sync (no immediate local-state mutation observable to the caller; future `listDevices` will return the new device).
- **Triggers notifications.** None directly on the local JSON-RPC subscriber; the primary device may later receive a `sync` event from the now-active secondary, but that is upstream-driven.
- **Cryptographically blocking.** `m.addDeviceLink` performs a key-exchange + provisioning-message round-trip with the secondary across the Signal server before returning — runtime is "noticeable" (seconds), not instant. Pure RPC timeout from `JsonRpcClient.RequestTimeoutSeconds` applies; do NOT shorten it for this method without justification.

**Enum values used:**

- None.

**Quirks / surprises:**

- **The URI is the secondary's "advertisement", not the primary's invitation.** Primary calls `addDevice` with a URI that the SECONDARY device generated and displayed (as QR-code-encoded sgnl:// URL). This is the inverse of the obvious flow ("primary creates invitation, secondary scans"). The mental model: secondary spawns a key-pair, encodes its `uuid` + public key into an `sgnl://` URL, displays the QR; primary scans/types the URL and provisions a session-key encrypted to that public key. See `DeviceLinkUrl.createDeviceLinkUri` (line 47) for the URI shape the secondary emits.
- **`pub_key` is base64-encoded WITHOUT padding when emitted by signal-cli secondary** (`DeviceLinkUrl.java:48` strips `=` chars). Java's `Base64.getDecoder()` still accepts padded input, so external secondaries (Signal Desktop, etc.) that emit padded base64 also work. .NET `Convert.FromBase64String` requires padding restoration; recommend documenting this as a consumer concern OR pre-validating URI client-side.
- **`InvalidDeviceLinkException` vs `RuntimeException`.** `parseDeviceLinkUri` throws `RuntimeException("Invalid device link uri")` (NOT `InvalidDeviceLinkException`) when the URI has empty query string (`DeviceLinkUrl.java:20`). This unchecked exception escapes the declared `throws InvalidDeviceLinkException` and propagates up — likely surfaces as `-32603 INTERNAL_ERROR` rather than `-1 UserError`. Defect upstream; do not rely on `-1` for empty-query URIs.
- **`NotPrimaryDeviceException` carries a different message at the source** (`NotPrimaryDeviceException.java:6`: `"This function is not supported for linked devices."`) than what `AddDeviceCommand` re-throws as `UserErrorException` (`"This command doesn't work on linked devices."`). The wire-level message is the latter; do NOT match against the manager-API string.
- **No `--name` / device name parameter.** The newly linked device's name is set by the secondary itself during the provisioning handshake; primary cannot pre-name the device. To rename, call `updateDevice` after linking.

---

### `listDevices` — Wave 5

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/ListDevicesCommand.java @ bda4e7fc` (lines 20-68)
- Manager API: `lib/src/main/java/org/asamk/signal/manager/api/Device.java @ bda4e7fc` (line 3)
- Wire record: `ListDevicesCommand.java:67` (`private record JsonDevice(long id, String name, long createdTimestamp, long lastSeenTimestamp)`).

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | conditional | — | E.164; multi-account-mode-required (`SignalJsonRpcCommandHandler.java:128`). |

(No other arguments — `attachToSubparser` only adds `--help`.)

**Result (response) wire shape:**

Top-level is a **JSON array of `JsonDevice` objects** (not wrapped). `writer.write(jsonDevices)` at `ListDevicesCommand.java:62` serializes a `List<JsonDevice>`.

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| `id` | `id` | `long` | Signal-protocol device ID. Primary is always `1`; linked devices get sequential IDs assigned by the server. **Wire type is `long` (`JsonDevice` record line 67)**, even though `Device.id()` in the manager API is `int` (`Device.java:3`). The widening happens at the `new JsonDevice(d.id(), ...)` site (`ListDevicesCommand.java:60`) — `int` → `long` implicit widening. Map as `long` on the .NET side to match wire shape. |
| `name` | `name` | `String` | Device's self-reported name. May be `null` (Java `String` is nullable). Set on the secondary side during provisioning and later via `updateDevice`. |
| `createdTimestamp` | `createdTimestamp` | `long` | millis-since-epoch when the device was linked. |
| `lastSeenTimestamp` | `lastSeenTimestamp` | `long` | millis-since-epoch of the device's most recent server-observed activity. May be `0` if the device has never connected since linking. |

**Validation rules:**

- None client-side beyond `account` resolution.

**Error codes specific to this method:**

- `-3 IoError` — `"Failed to get linked devices: <IOException message>"` (`ListDevicesCommand.java:43-45`).
- `-32602 INVALID_PARAMS` — `account` missing in multi-account mode.

**Side-effects:**

- **Read-only.** No local-state mutation. Triggers a server fetch via `m.getLinkedDevices()`; this is HTTP-bound (not local-cache), so transient network failures surface as `-3 IoError`.

**Enum values used:**

- None.

**Quirks / surprises:**

- **`Device.isThisDevice` is DROPPED from the JSON projection.** The manager API's `Device` record has 5 fields `(int id, String name, long created, long lastSeen, boolean isThisDevice)` (`Device.java:3`), but the JSON-RPC wire shape projects only 4 (the `JsonDevice` record at `ListDevicesCommand.java:67`). Consumers who need "which entry am I, the caller?" must self-determine — typically `id == 1` is the primary, but the PlainText writer's "(this device)" annotation (`ListDevicesCommand.java:50`) has no JSON-RPC equivalent. We MAY want to surface this on .NET-side via the `account` context (the caller knows whether they're primary, and `id` matches), but the source-of-truth is gone from the wire — do NOT invent it.
- **Wire-name `createdTimestamp` / `lastSeenTimestamp` differs from manager-API name `created` / `lastSeen`** (`Device.java:3`). The rename happens at the `JsonDevice` projection (record field-name = JSON property-name with Jackson default config). Match the wire names verbatim in `[JsonPropertyName]` on .NET DTO.
- **`id` widens `int → long` at the wire boundary.** `Device.id() : int` (`Device.java:3`); `JsonDevice.id : long` (`ListDevicesCommand.java:67`). The CLAUDE.md task brief stated `Device.id` is `long` — that's correct AT THE WIRE; the manager API itself is `int`. Use `long` on the .NET DTO.
- **Top-level result is a bare array, not `{ devices: [...] }`.** Mirrors `listAccounts` / `listGroups` shapes — use the wrapper-record + custom `JsonConverter` pattern documented in CLAUDE.md "AOT readiness" (`ListAccountsResponse`/`ListGroupsResponse` as canonical reference).

---

### `removeDevice` — Wave 5

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/RemoveDeviceCommand.java @ bda4e7fc` (lines 15-46)
- Manager API: `m.removeLinkedDevices(int deviceId)` (called at `RemoveDeviceCommand.java:39`; signature in `lib/src/main/java/org/asamk/signal/manager/Manager.java` — not read).
- Wire records: none (request-only RPC; result is empty / `null`).

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | conditional | — | E.164; multi-account-mode-required. |
| `deviceId` | `deviceId` | `int` | yes | — | Argparse dest is `device-id` (from `--device-id` flag), with `--deviceId` alias (`RemoveDeviceCommand.java:25-28`). `JsonRpcNamespace.get` (`JsonRpcNamespace.java:20-28`) tries the dashed-form first, then falls back to camelCase — clients SHOULD send `deviceId` (camelCase) per signal-cli JSON-RPC convention. Type-coerced to `int` by argparse (`type(int.class)` at line 27). |

**Result (response) wire shape:**

`handleCommand` returns `void` → empty result.

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| _(empty)_ | _(empty)_ | — | Success = empty result. |

**Validation rules** (`UserErrorException` throws → wire `-1 UserError`):

- `"This command doesn't work on linked devices."` when invoked on a secondary device (`RemoveDeviceCommand.java:40-41`).

**Error codes specific to this method:**

- `-1 UserError` — only the "not primary device" message.
- `-3 IoError` — `"Error while removing device: <IOException message>"` (`RemoveDeviceCommand.java:42-43`). Server rejection (e.g. "device not found") arrives via this path.
- `-32602 INVALID_PARAMS` — `deviceId` missing (argparse rejects with required-arg error; surfaces dispatcher-side) or `account` missing in multi-account mode.

**Side-effects:**

- **Mutates state.** Server-side: removes the entry from the account's linked-device list; the removed secondary device immediately loses all message-sending/receiving capability. This is **destructive** — there is no undo path; the secondary must re-link via `addDevice` to regain access.
- Does NOT trigger a local notification on the JSON-RPC subscriber.

**Enum values used:**

- None.

**Quirks / surprises:**

- **`deviceId` is `int`, not `long`.** Asymmetric with `listDevices` (which widens to `long` at the wire) — the `removeDevice` / `updateDevice` argparse declarations are explicitly `type(int.class)` (`RemoveDeviceCommand.java:27`, `UpdateDeviceCommand.java:27`). signal-cli's wire shape uses the smaller type on input. On .NET, accept `int` on `RemoveDeviceParameters` / `UpdateDeviceParameters` but emit `long` for `listDevices` response shape — match the wire asymmetry.
- **Removing primary device's own ID (`deviceId = 1`) is not blocked client-side.** No throw site catches this; server-side rejection (`-3 IoError`) is the only safety net. Consider .NET-side validation (`if (deviceId == 1) throw new ArgumentException("Cannot remove primary device via removeDevice; use unregister instead.")`) — but ground this in CLAUDE.md rule #14 (idempotent state errors) before adding.
- **No bulk removal.** The manager-API method `removeLinkedDevices(int)` (note plural) is called with a single ID; the JSON-RPC surface mirrors single-device removal only.

---

### `updateDevice` — Wave 5

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/UpdateDeviceCommand.java @ bda4e7fc` (lines 15-50)
- Manager API: `m.updateLinkedDevice(int deviceId, String deviceName)` (called at `UpdateDeviceCommand.java:43`; signature in `Manager.java` — not read).
- Wire records: none (request-only RPC; result is empty / `null`).

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | conditional | — | E.164; multi-account-mode-required. |
| `deviceId` | `deviceId` | `int` | yes | — | Same alias setup as `removeDevice` — argparse dest `device-id` with `--deviceId` alias (`UpdateDeviceCommand.java:25-28`). Clients send `deviceId`. |
| `deviceName` | `deviceName` | `String` | yes | — | Argparse dest `device-name` (from `--device-name` flag at `UpdateDeviceCommand.java:29-31`); no camelCase alias declared but `JsonRpcNamespace.get("device-name")` falls back to `deviceName` (`JsonRpcNamespace.java:26-27`). Clients send `deviceName`. |

**Result (response) wire shape:**

`handleCommand` returns `void` → empty result.

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| _(empty)_ | _(empty)_ | — | Success = empty result. |

**Validation rules** (`UserErrorException` throws → wire `-1 UserError`):

- `"This command doesn't work on linked devices."` when invoked on a secondary device (`UpdateDeviceCommand.java:44-45`).

**Error codes specific to this method:**

- `-1 UserError` — only the "not primary device" message.
- `-3 IoError` — `"Error while updating device: <IOException message>"` (`UpdateDeviceCommand.java:46-47`).
- `-32602 INVALID_PARAMS` — `deviceId` or `deviceName` missing.

**Side-effects:**

- **Mutates state.** Server-side: updates the encrypted device-name blob stored alongside the device entry. The renamed device's stored name in the primary's local cache will refresh on next `listDevices` fetch.
- **The new name is encrypted with the device's identity key before transmission** (Signal-protocol convention — implicit in `m.updateLinkedDevice` semantics; not directly visible in the command file). Privacy implication: `deviceName` MUST NOT be logged above `Trace` (CLAUDE.md critical rule #1).

**Enum values used:**

- None.

**Quirks / surprises:**

- **Argparse flags for the name differ from the JSON field name.** CLI accepts `-n` / `--device-name`; JSON-RPC clients send `deviceName` (per `JsonRpcNamespace.java:26-27` camelCase fallback). The dashed form (`device-name`) is the argparse dest, but signal-cli's JSON-RPC convention uses camelCase keys.
- **No empty-string validation.** Passing `deviceName: ""` is not rejected at the command level (no `isEmpty` guard in `UpdateDeviceCommand.handleCommand`); behavior depends on `m.updateLinkedDevice`'s server-side handling. Consider .NET-side `ArgumentException.ThrowIfNullOrWhiteSpace(deviceName)` (CLAUDE.md rule #14).
- **Cannot change the primary's own name via this method** — `deviceId = 1` is technically accepted by the argparse parser, but server-side semantics are undefined / not tested in upstream. Treat as best-effort.

---

## Verification

Java files read (all under `C:/Users/ivank/Нова папка/signal-cli/` @ `bda4e7fc`):

1. `src/main/java/org/asamk/signal/commands/AddDeviceCommand.java` (full, 67 lines)
2. `src/main/java/org/asamk/signal/commands/ListDevicesCommand.java` (full, 68 lines)
3. `src/main/java/org/asamk/signal/commands/RemoveDeviceCommand.java` (full, 46 lines)
4. `src/main/java/org/asamk/signal/commands/UpdateDeviceCommand.java` (full, 50 lines)
5. `src/main/java/org/asamk/signal/commands/JsonRpcLocalCommand.java` (full, 30 lines)
6. `src/main/java/org/asamk/signal/commands/JsonRpcNamespace.java` (full, 43 lines)
7. `src/main/java/org/asamk/signal/jsonrpc/SignalJsonRpcCommandHandler.java` (lines 60-168, dispatcher-level `account` extraction + error paths)
8. `lib/src/main/java/org/asamk/signal/manager/api/Device.java` (full, 3 lines — single-line record)
9. `lib/src/main/java/org/asamk/signal/manager/api/DeviceLinkUrl.java` (full, 58 lines)
10. `lib/src/main/java/org/asamk/signal/manager/api/DeviceLimitExceededException.java` (full, 12 lines)
11. `lib/src/main/java/org/asamk/signal/manager/api/InvalidDeviceLinkException.java` (full, 12 lines)
12. `lib/src/main/java/org/asamk/signal/manager/api/NotPrimaryDeviceException.java` (full, 8 lines)

No `lib/src/main/java/org/asamk/signal/manager/Manager.java` direct read — manager-API method signatures inferred from call-sites (`m.addDeviceLink(DeviceLinkUrl)`, `m.getLinkedDevices() : List<Device>`, `m.removeLinkedDevices(int)`, `m.updateLinkedDevice(int, String)`). Signatures verified consistent across all four command files. If wave-5 implementation needs throw-set details from inside the Manager (e.g. whether `getLinkedDevices` ever throws `NotPrimaryDeviceException` — it doesn't appear in the catch list at `ListDevicesCommand.java:43`), re-read `Manager.java` then.
