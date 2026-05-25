# Wave 3 — contacts & identity (research notes)

Source-of-truth Java records and command wire-shapes for the 8 RPC methods in
Wave 3. All citations pinned to signal-cli commit **`bda4e7fc`** ("Prepare next
release", 2026-05-24), tag `v0.14.4.1` HEAD — same baseline used by
`.claude/rules/signal-cli-protocol.md`.

## Conventions used in tables below

- **JSON field name** comes from argparse `dest` after `dashSeparatedToCamelCaseString`
  (`src/main/java/org/asamk/signal/util/Util.java:36-39`) applied by `JsonRpcNamespace`
  (`src/main/java/org/asamk/signal/commands/JsonRpcNamespace.java:13-43`).
  Positional `nargs("*")` args are also resolved via plural fallback
  (`get(dest + "s")`), so a positional argparse `recipient` with `nargs("*")` is
  callable as JSON `recipients: string[]` (canonical) OR `recipient: string`
  (single-value fallback).
- **`account` param** is consumed by `SignalJsonRpcCommandHandler.getManagerFromParams`
  (`src/main/java/org/asamk/signal/jsonrpc/SignalJsonRpcCommandHandler.java:127-139`)
  BEFORE the command sees `Namespace` — it is removed from `params` on success.
  Required when daemon serves multiple accounts; optional when exactly one
  account is registered (auto-selected via `c.getManagers().getFirst()`,
  lines 106-112). Treat as **required** at the .NET surface (mandatory in DTO)
  for predictability — single-account auto-select is a daemon convenience, not
  a contract.
- **Identifier strings:** any `recipient` field accepts the union
  `phone-number | UUID | "PNI:<uuid>" | "u:<username>"` per
  `RecipientIdentifier.Single.fromString` (`lib/.../RecipientIdentifier.java:26-49`).
- **Field-name JSON shape:** Jackson default (no `@JsonProperty` overrides on
  any of these records) — Java record component names map verbatim, camelCase.

---

### `listContacts` — Wave 3

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/ListContactsCommand.java @ bda4e7fc`
- Wire records (response elements): same file (anonymous `JsonContact`) →
  `src/main/java/org/asamk/signal/json/JsonContact.java @ bda4e7fc`
- Supporting types:
  - `lib/src/main/java/org/asamk/signal/manager/api/Contact.java @ bda4e7fc`
  - `lib/src/main/java/org/asamk/signal/manager/api/Profile.java @ bda4e7fc`
  - `lib/src/main/java/org/asamk/signal/manager/api/PhoneNumberSharingMode.java @ bda4e7fc`

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| account | `account` | `String` | yes (effectively) | — | Phone-number or UUID; consumed by dispatcher before command sees `Namespace`. |
| recipient | `recipients` (canonical, plural fallback) OR `recipient` | `String[]` | no | `[]` | argparse `nargs("*")`. Each entry is a single-recipient identifier (phone / UUID / `PNI:<uuid>` / `u:<username>`). |
| -a / --all-recipients | `allRecipients` | `boolean` | no | `false` | `Arguments.storeTrue()`. When `true`, returns ALL known recipients, not only known contacts. Internally inverted to `onlyContacts = !allRecipients` (`ListContactsCommand.java:54-59`). |
| --blocked | `blocked` | `Boolean` (nullable) | no | `null` | `type(Boolean.class)`. `null` = "no filter", `true` = only blocked, `false` = only unblocked. |
| --name | `name` | `String` (nullable) | no | `null` | Substring filter on contact OR profile name. |
| --detailed | `detailed` | `boolean` | no | `false` | For JSON output: **ignored — JSON output is always detailed**. Affects only `PlainTextWriter`. Keep on the DTO for completeness but document it as a no-op for JSON callers. |
| --internal | `internal` | `boolean` | no | `false` | When `true`, includes `internal` sub-object (capabilities, unidentifiedAccessMode, etc.). When `false`, the `internal` field is omitted from the per-contact record (`@JsonInclude(NON_NULL)` on `JsonContact.internal`, `JsonContact.java:28`). |

**Result (response) wire shape:**

Top-level result is a **JSON array** of `JsonContact` records (no wrapper
object). Schema per element (`JsonContact.java:9-49`):

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| number | `number` | `String?` | E.164 phone, nullable. |
| uuid | `uuid` | `String?` | ACI UUID as string, nullable. |
| username | `username` | `String?` | Nullable. |
| name | `name` | `String` | Always present (Java `String` default `""` from `Contact.getName()` if both given+family empty). |
| givenName | `givenName` | `String?` | Nullable; from `Contact.givenName()`. |
| familyName | `familyName` | `String?` | Nullable; from `Contact.familyName()`. |
| nickName | `nickName` | `String?` | Nullable. |
| nickGivenName | `nickGivenName` | `String?` | Maps to `Contact.nickNameGivenName()` — **note the JSON-name shortening from Java `nickNameGivenName` → JSON `nickGivenName`** (`JsonContact.java:17`). |
| nickFamilyName | `nickFamilyName` | `String?` | Maps to `Contact.nickNameFamilyName()` — same shortening (`JsonContact.java:18`). |
| note | `note` | `String?` | Nullable. |
| color | `color` | `String?` | Nullable. |
| isArchived | `isArchived` | `boolean` | |
| isBlocked | `isBlocked` | `boolean` | |
| isHidden | `isHidden` | `boolean` | |
| messageExpirationTime | `messageExpirationTime` | `int` | Seconds; `0` = disabled. |
| profileSharing | `profileSharing` | `boolean` | From `Contact.isProfileSharingEnabled()`. |
| unregistered | `unregistered` | `boolean` | `true` ⇔ `Contact.unregisteredTimestamp != null` (`ListContactsCommand.java:152`). |
| profile | `profile` | `JsonProfile?` | Always emitted when `r.getProfile() != null`. Otherwise `null`. |
| internal | `internal` | `JsonInternal?` | Only when request `internal=true`, otherwise omitted via `@JsonInclude(NON_NULL)`. |

`JsonProfile` (`JsonContact.java:31-40`):

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| lastUpdateTimestamp | `lastUpdateTimestamp` | `long` | Unix-ms. |
| givenName | `givenName` | `String?` | |
| familyName | `familyName` | `String?` | |
| about | `about` | `String?` | |
| aboutEmoji | `aboutEmoji` | `String?` | |
| hasAvatar | `hasAvatar` | `boolean` | `r.getProfile().getAvatarUrlPath() != null`. |
| mobileCoinAddress | `mobileCoinAddress` | `String?` | Base64-encoded bytes; `null` when not set. |

`JsonInternal` (`JsonContact.java:42-48`):

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| capabilities | `capabilities` | `String[]` | Enum names (`Profile.Capability`): `storage` \| `storageServiceEncryptionV2Capability` (lower/mixed case — Java enum identifiers, not UPPER_SNAKE; see `Profile.java:163-174`). |
| unidentifiedAccessMode | `unidentifiedAccessMode` | `String?` | One of `DISABLED` \| `ENABLED` \| `UNRESTRICTED`. `UNKNOWN` is mapped to `null` (`ListContactsCommand.java:128-131`). |
| sharesPhoneNumber | `sharesPhoneNumber` | `Boolean?` | `true` ⇔ `PhoneNumberSharingMode == EVERYBODY`; `null` if sharing mode unknown. |
| discoverableByPhonenumber | `discoverableByPhonenumber` | `Boolean?` | Nullable Boolean; **note JSON name `discoverableByPhonenumber` — lowercase "phonenumber", not "PhoneNumber"** (`JsonContact.java:47`). |

**Validation rules** (UserErrorException throws):
- `"Invalid phone number '<x>': <msg>"` when any `recipients[]` entry fails `RecipientIdentifier.Single.fromString` (`CommandUtil.java:107`).

**Error codes specific to this method:**
- `-1 UserError` — invalid recipient identifier.

**Side-effects:**
- Read-only. No notifications, no state change.

**Enum values used:**
- `Profile.Capability`: `storage`, `storageServiceEncryptionV2Capability` (Java enum identifiers are lowercase/mixed-case here — not UPPER_SNAKE — `Profile.java:163-166`).
- `Profile.UnidentifiedAccessMode`: `UNKNOWN`, `DISABLED`, `ENABLED`, `UNRESTRICTED`. `UNKNOWN` is replaced with `null` on the wire.
- `PhoneNumberSharingMode`: `EVERYBODY`, `CONTACTS`, `NOBODY` — not exposed verbatim; surfaced via `sharesPhoneNumber: Boolean?` (only `EVERYBODY` → `true`; everything else → `false`; null → `null`).

**Quirks / surprises:**
- `name` field is the **composed display name** (`givenName + " " + familyName` or empty) — NOT a separately stored field. `Contact` record has no `name`; method is `Contact.getName()` (`Contact.java:68-81`).
- `nickGivenName` / `nickFamilyName` — Java field names are `nickNameGivenName` / `nickNameFamilyName` but the wire JSON drops the second `Name` (`JsonContact.java:17-18`). When generating .NET DTOs, use `[JsonPropertyName("nickGivenName")]` + property name `NickNameGivenName` if you want the .NET property aligned with the Java domain field.
- `discoverableByPhonenumber` (one word, lowercase `n`) — typo-shape preserved verbatim.
- `internal` is omitted (not `null`) when request `internal=false` — Jackson `@JsonInclude(NON_NULL)` behavior. .NET DTO should mark it nullable.
- `--detailed` flag has zero effect on JSON output (always detailed). Keep as no-op parameter for parity with CLI; document this clearly in XMLDoc.

---

### `listIdentities` — Wave 3

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/ListIdentitiesCommand.java @ bda4e7fc`
- Wire record: same file, private `JsonIdentity` (`ListIdentitiesCommand.java:88-96`)
- Supporting types:
  - `lib/src/main/java/org/asamk/signal/manager/api/Identity.java @ bda4e7fc`
  - `lib/src/main/java/org/asamk/signal/manager/api/TrustLevel.java @ bda4e7fc`

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| account | `account` | `String` | yes | — | Dispatcher param. |
| -n / --number | `number` | `String?` | no | `null` | Single recipient identifier. When omitted, lists ALL known identities (`m.getIdentities()`). When set, filters to that one recipient (`m.getIdentities(recipient)`). Accepts phone / UUID / `PNI:<uuid>` / `u:<username>`. |

**Result (response) wire shape:**

Top-level result is a **JSON array** of `JsonIdentity` records.

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| number | `number` | `String?` | E.164 phone, nullable. |
| uuid | `uuid` | `String?` | ACI UUID string, nullable. |
| fingerprint | `fingerprint` | `String` | Hex-encoded bytes (`Hex.toString(id.fingerprint())`). |
| safetyNumber | `safetyNumber` | `String` | Space-formatted 60-digit safety number (`Util.formatSafetyNumber(...)`). |
| scannableSafetyNumber | `scannableSafetyNumber` | `String?` | Base64-encoded bytes; `null` if not available (`ListIdentitiesCommand.java:77-79`). |
| trustLevel | `trustLevel` | `String` | Enum name verbatim — see below. |
| addedTimestamp | `addedTimestamp` | `long` | Unix-ms (`id.dateAddedTimestamp()`). |

**Validation rules** (UserErrorException throws):
- `"Invalid phone number '<x>': <msg>"` when `number` param fails parse (`CommandUtil.java:107` via `getSingleRecipientIdentifier`).

**Error codes specific to this method:**
- `-1 UserError` — invalid recipient identifier.

**Side-effects:**
- Read-only.

**Enum values used:**
- `TrustLevel`: `UNTRUSTED`, `TRUSTED_UNVERIFIED`, `TRUSTED_VERIFIED` — **emitted as UPPER_SNAKE_CASE Java enum names verbatim** via `id.trustLevel().name()` (`ListIdentitiesCommand.java:80`). Expose in .NET as a PascalCase enum (`TrustLevel.Untrusted` / `TrustedUnverified` / `TrustedVerified`) with `JsonStringEnumConverter` that maps to the Java `UPPER_SNAKE` wire shape.

**Quirks / surprises:**
- Filter arg is named `number` (`-n / --number`) even though it accepts non-number identifiers (UUID, PNI, username) thanks to `RecipientIdentifier.Single.fromString`. .NET DTO field name should be `Number` (or `Recipient` alias) — match the wire.
- Returns a flat array, not a wrapped object. Use the same wrapper-record + custom `JsonConverter` pattern as `ListAccountsResponse` / `ListGroupsResponse` per CLAUDE.md "AOT readiness".
- `scannableSafetyNumber` is `null` when signal-cli didn't compute it (e.g. very old session) — model as nullable.

---

### `trust` — Wave 3

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/TrustCommand.java @ bda4e7fc`
- Supporting types:
  - `lib/src/main/java/org/asamk/signal/manager/api/IdentityVerificationCode.java @ bda4e7fc`
  - `lib/src/main/java/org/asamk/signal/manager/api/TrustLevel.java @ bda4e7fc`

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| account | `account` | `String` | yes | — | Dispatcher param. |
| recipient | `recipient` | `String` | yes | — | `subparser.addArgument("recipient").required(true)`. Single-recipient identifier (phone / UUID / `PNI:<uuid>` / `u:<username>`). |
| -a / --trust-all-known-keys | `trustAllKnownKeys` | `boolean` | no (mutex group) | `false` | `Arguments.storeTrue()`. Mutually exclusive with `verifiedSafetyNumber`. Trust ALL known keys for this recipient — testing-only per CLI help. |
| -v / --verified-safety-number / --verified-fingerprint | `verifiedSafetyNumber` | `String?` | no (mutex group) | `null` | String value. Accepts: 60-digit safety number (with optional spaces), 66-hex-char fingerprint, or base64 scannable-safety-number — disambiguated by length (`IdentityVerificationCode.parse`). |

Exactly ONE of `trustAllKnownKeys=true` OR `verifiedSafetyNumber!=null` must
be set. argparse enforces mutual-exclusion at the CLI; in JSON-RPC the dispatcher
does NOT enforce it (argparse mutex groups apply to `--flag` parsing, not to
JSON payload). The command body throws `UserError` if neither is set.

**Result (response) wire shape:**

No `outputWriter.write(...)` call — result is **`null`** (empty/void). signal-cli
JSON-RPC for void commands emits `result: null` on success. .NET surface returns
`Task` (not `Task<T>`).

**Validation rules** (UserErrorException throws):
- `"Failed to set the trust for this number, make sure the number is correct."` —
  when `m.trustIdentityAllKeys(recipient)` returns `false` (no known identity
  for that recipient) (`TrustCommand.java:46-47`).
- `"The user <identifier> is not registered."` — `UnregisteredRecipientException`
  (`TrustCommand.java:50, 74`).
- `"You need to specify the fingerprint/safety number you have verified with -v SAFETY_NUMBER"` —
  when neither `trustAllKnownKeys` nor `verifiedSafetyNumber` is provided
  (`TrustCommand.java:55-56`).
- `"Safety number has invalid format, either specify the old hex fingerprint or the new safety number"` —
  when `IdentityVerificationCode.parse` throws (length not 60 nor 66 and not
  valid base64) (`TrustCommand.java:63-64`).
- `"Failed to set the trust for this number, make sure the number and the fingerprint/safety number are correct."` —
  when `m.trustIdentityVerified` returns `false` (fingerprint/safety-number
  mismatch against stored identity) (`TrustCommand.java:70-71`).

**Error codes specific to this method:**
- `-1 UserError` — all five validation failures above.

**Side-effects:**
- Mutates trust DB entry for the recipient. May trigger `verified-message`
  sync to linked devices.

**Enum values used:**
- `TrustLevel` (indirectly via `IdentityVerificationCode`): set to `TRUSTED_VERIFIED` when verified-safety-number path succeeds; set to `TRUSTED_UNVERIFIED` when `--trust-all-known-keys` path succeeds.
- `IdentityVerificationCode` sealed subtypes: `Fingerprint`, `SafetyNumber`, `ScannableSafetyNumber` (`IdentityVerificationCode.java:10-14`). Disambiguated by length:
  - 66 chars → hex fingerprint
  - 60 chars → space-stripped safety number
  - else → base64-decoded scannable safety number
  This is internal to signal-cli — the .NET surface just passes the original
  string; do NOT replicate the length-dispatch in C#.

**Quirks / surprises:**
- The argparse `dest` for `--trust-all-known-keys` is `trust_all_known_keys` (argparse default), which `JsonRpcNamespace` converts to `trustAllKnownKeys` via `dashSeparatedToCamelCaseString`. Same for `verified-safety-number` → `verifiedSafetyNumber`. CLI-side alias `--verified-fingerprint` is ignored by JSON-RPC (only one canonical dest).
- Both Manager methods (`trustIdentityAllKeys`, `trustIdentityVerified`) return `boolean` and signal-cli surfaces `false` as a `UserError` — there is no native "identity not found" exception subclass. Model both as `JsonRpcException` with code `-1` on the .NET side.

---

### `updateContact` — Wave 3

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/UpdateContactCommand.java @ bda4e7fc`

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| account | `account` | `String` | yes | — | Dispatcher param. |
| recipient | `recipient` | `String` | yes | — | Positional, no `required(true)` on the argparse, but `CommandUtil.getSingleRecipientIdentifier` throws if `null`. Single recipient. |
| -n / --name | `name` | `String?` | no | `null` | Convenience alias: when `givenName` is null and `name` is set, `givenName` is filled from `name`, and `familyName` defaults to empty-string (`UpdateContactCommand.java:53-58`). |
| --given-name | `givenName` | `String?` | no | `null` | New system given name. |
| --family-name | `familyName` | `String?` | no | `null` | New system family name. |
| --nick-given-name | `nickGivenName` | `String?` | no | `null` | New nick given name. JSON name matches argparse-dashed → camelCase mapping (`nick-given-name` → `nickGivenName`). |
| --nick-family-name | `nickFamilyName` | `String?` | no | `null` | New nick family name. |
| --note | `note` | `String?` | no | `null` | New note. |
| -e / --expiration | `expiration` | `Integer?` | no | `null` | `type(int.class)`. Message-expiration timer in seconds. `0` = disabled. |

**Result (response) wire shape:**

No `outputWriter.write(...)` — result is **`null`** (void). .NET returns `Task`.

**Validation rules** (UserErrorException / IOErrorException throws):
- `"Invalid phone number '<x>': <msg>"` — recipient parse failure (`CommandUtil.java:107`).
- `"The user <identifier> is not registered."` — `UnregisteredRecipientException` (`UpdateContactCommand.java:66`).
- `"Update contact error: <message>"` — `IOException` wrapper (`UpdateContactCommand.java:63-64`).

**Error codes specific to this method:**
- `-1 UserError` — invalid recipient OR unregistered recipient.
- `-3 IoError` — generic I/O during DB update or contact-sync to linked devices.

**Side-effects:**
- Mutates contact record (given/family/nick names, note).
- Sets message-expiration timer if `expiration` provided. **Note**: setting the
  timer is a separate Manager call (`m.setExpirationTimer`) — it sends an
  Expiration-Timer-Update protocol message to the contact, so this is NOT a
  pure local mutation.
- Triggers contacts-sync to linked devices.

**Enum values used:**
- None.

**Quirks / surprises:**
- The `--name` convenience param mutates `givenName` AND defaults `familyName=""` — to clear an existing family name, pass `--name "Foo"` (sets givenName=Foo, familyName=""). To set ONLY givenName without touching familyName, use `--given-name "Foo"` (familyName remains whatever it was — actually `null` is passed through to `setContactName`, which means the underlying DB may overwrite to null; verify against `Manager.setContactName` if it matters for callers).
- All fields nullable means there is no way to **clear** a contact-name field through the API (passing `null` is "don't touch"). Empty-string `""` is the canonical "clear" value.
- `expiration=0` disables the timer; positive values set seconds. Negative values are not explicitly guarded — likely accepted by `setExpirationTimer` and surfaced as either UserError or silently clamped (verify if exposing).

---

### `removeContact` — Wave 3

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/RemoveContactCommand.java @ bda4e7fc`

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| account | `account` | `String` | yes | — | Dispatcher param. |
| recipient | `recipient` | `String` | yes | — | Positional. Single recipient identifier. |
| --hide | `hide` | `boolean` | no (mutex group) | `false` | `Arguments.storeTrue()`. Hide contact in list, keep data. |
| --forget | `forget` | `boolean` | no (mutex group) | `false` | `Arguments.storeTrue()`. Delete ALL data (identity keys + sessions). |

Mutex group: at most ONE of `hide` / `forget`. Neither = delete the contact
**record** (`m.deleteContact(recipient)`) but keep identity keys/sessions.
`hide=true` → `m.hideRecipient(recipient)`. `forget=true` → `m.deleteRecipient(recipient)`.

**Result (response) wire shape:**

No `outputWriter.write(...)` — result is **`null`**. .NET returns `Task`.

**Validation rules** (UserErrorException throws):
- `"Invalid phone number '<x>': <msg>"` — recipient parse failure (`CommandUtil.java:107`).

The command body has no explicit `throw` for both flags set simultaneously —
because argparse mutex group catches it at CLI parse-time. In JSON-RPC,
**both flags set together would silently apply only `hide`** (first branch
wins, `RemoveContactCommand.java:43-49`). Recommend client-side guard in .NET
SDK to mirror argparse semantics.

**Error codes specific to this method:**
- `-1 UserError` — invalid recipient.

**Side-effects:**
- `hide=true`: hides contact in UI listings; keeps identity/session/profile data.
- `forget=true`: hard delete — identity keys, sessions, profile, contact record all gone.
- Neither: deletes only the contact record (names/note/blocked state etc.); keeps identity keys/sessions for the underlying recipient.
- Triggers contacts-sync to linked devices.

**Enum values used:**
- None on the wire. Could model the three modes as a .NET enum
  `RemoveContactMode { DeleteContact, Hide, Forget }` and translate to the
  flag pair internally for ergonomics. Document the Java `null`-state mapping
  in XMLDoc.

**Quirks / surprises:**
- Three distinct behaviors selected by two booleans — model as a single
  enum-typed param on the .NET surface to prevent the "both flags set"
  caller mistake.
- Mutex enforcement is argparse-only — JSON-RPC accepts both `hide=true` and
  `forget=true`; signal-cli will silently apply `hide`.

---

### `updateProfile` — Wave 3

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/UpdateProfileCommand.java @ bda4e7fc`
- Supporting type: `lib/src/main/java/org/asamk/signal/manager/api/UpdateProfile.java @ bda4e7fc`

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| account | `account` | `String` | yes | — | Dispatcher param. |
| --given-name / --name | `givenName` | `String?` | no | `null` | Profile given name (also accepts alias `name`; .NET surface should accept ONLY `givenName` for clarity — `name` is a CLI convenience alias). |
| --family-name | `familyName` | `String?` | no | `null` | Profile family name. |
| --about | `about` | `String?` | no | `null` | About text. |
| --about-emoji | `aboutEmoji` | `String?` | no | `null` | About emoji (single emoji string). |
| --mobile-coin-address / --mobilecoin-address | `mobileCoinAddress` | `String?` | no | `null` | Base64-encoded public address bytes (Java decodes via `Base64.getDecoder().decode(...)`). |
| --avatar | `avatar` | `String?` | no (mutex group) | `null` | Filesystem path to new profile-avatar image. **`signal-cli` reads the file from its own filesystem** — when running over JSON-RPC daemon, the path is daemon-side. |
| --remove-avatar | `removeAvatar` | `boolean` | no (mutex group) | `false` | `Arguments.storeTrue()`. Delete current avatar. |

Mutex: `avatar` / `removeAvatar` — only one. Both unset = avatar untouched.
`avatar=null && removeAvatar=true` ⇒ avatar deleted.

**Result (response) wire shape:**

No `outputWriter.write(...)` — result is **`null`**. .NET returns `Task`.

**Validation rules** (UserErrorException / IOErrorException throws):
- `"Update profile error: <message>"` — `IOException` wrapper (`UpdateProfileCommand.java:68`).
- `IllegalArgumentException` from `Base64.getDecoder().decode(mobileCoinAddressString)` when the input is not valid base64 — **NOT caught by command body**; surfaced as raw exception (likely as `-32603 InternalError` via the dispatcher's default handler). Recommend client-side base64 validation before sending.

**Error codes specific to this method:**
- `-3 IoError` — generic I/O (avatar file read, profile-set request to server, contacts-sync).
- `-32603 InternalError` — invalid base64 in `mobileCoinAddress` (Java `IllegalArgumentException` not specially handled).

**Side-effects:**
- Updates server-side profile (sent to Signal service).
- Reads `avatar` file from daemon-side filesystem when set.
- Triggers profile-sync to linked devices.

**Enum values used:**
- None.

**Quirks / surprises:**
- The `avatar` path is **interpreted on the daemon machine**, not the JSON-RPC client. Document this in XMLDoc — clients shipping bytes-over-RPC need a separate path (e.g. write bytes to a local temp file first, then pass that path; or extend to base64-data-URI in a future signal-cli release).
- argparse uses `givenName` argparse-dest (camelCase, not dashed) because `--given-name`'s dest is `given_name` then `JsonRpcNamespace` converts to `givenName`. The CLI alias `--name` collapses to the same `givenName` JSON field.
- argparse alias `--mobilecoin-address` (no hyphen between mobile and coin) — but the canonical dest is `mobile_coin_address` → `mobileCoinAddress` JSON. Only the canonical wire-name is callable via JSON-RPC.
- All fields are independently nullable — to clear `about`, pass `about: ""` (empty string is distinct from `null`-don't-touch).

---

### `block` — Wave 3

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/BlockCommand.java @ bda4e7fc`

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| account | `account` | `String` | yes | — | Dispatcher param. |
| recipient | `recipients` (plural fallback) OR `recipient` | `String[]` | no | `[]` | Positional `nargs("*")`. Each entry is a single-recipient identifier. |
| -g / --group-id / --group | `groupIds` (plural fallback) OR `groupId` | `String[]` | no | `[]` | Base64-encoded GroupIds. argparse dest is `group_id` → camelCase `groupId`; plural-fallback `groupIds`. |

At least ONE of `recipients` or `groupIds` should be non-empty in practice
(otherwise the command is a no-op). signal-cli does **not** validate this —
empty arrays are accepted silently.

**Result (response) wire shape:**

No `outputWriter.write(...)` — result is **`null`**. .NET returns `Task`.

**Validation rules** (UserErrorException / UnexpectedErrorException throws):
- `"Invalid phone number '<x>': <msg>"` — recipient parse failure.
- `"Invalid group id: <message>"` — `GroupIdFormatException` on `GroupId.fromBase64` (`CommandUtil.java:82`).
- `"This command doesn't work on linked devices."` — `NotPrimaryDeviceException` (twice — once for recipients block, once for groups block) (`BlockCommand.java:47, 58`).
- `"The user <identifier> is not registered."` — `UnregisteredRecipientException` (`BlockCommand.java:50-52`).
- `"Failed to sync block to linked devices: <message>"` — `IOException` wrapper (`BlockCommand.java:48-49, 62-63`) → `UnexpectedErrorException` (NOT `IOErrorException`).
- **Unknown group id is NOT an error** — `GroupNotFoundException` is just logged at warn-level and processing continues (`BlockCommand.java:60-61`). The block for **other** known groups still succeeds.

**Error codes specific to this method:**
- `-1 UserError` — invalid recipient / invalid group-id format / not-primary-device / unregistered recipient.
- `-32603 InternalError` (mapped from `UnexpectedErrorException`) — I/O sync failure to linked devices.

**Side-effects:**
- Marks contacts and groups as blocked locally.
- Syncs block-state to linked devices.

**Enum values used:**
- None on the wire.

**Quirks / surprises:**
- The recipient-block and group-block are TWO separate Manager calls, executed
  sequentially. If recipient-block throws `UserError`/`Unexpected`, group-block
  is **not** attempted — partial application. If group-block throws, recipient
  block is already committed.
- `NotPrimaryDeviceException` from either step is `UserError` `-1` (per
  `SignalJsonRpcCommandHandler.USER_ERROR`), NOT a distinct code. Linked-device
  callers should pattern-match on the message text or treat any `-1` from
  these methods as "primary-device required".
- Unknown group-ids are warnings only (not failures) — caller should NOT assume
  every group-id in their request was applied. There's no way to discover which
  failed except by inspecting server-side logs.

---

### `unblock` — Wave 3

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/UnblockCommand.java @ bda4e7fc`

**Params (request) wire shape:**

Identical to `block` — same fields, same mapping. See `block` section.

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| account | `account` | `String` | yes | — | Dispatcher param. |
| recipient | `recipients` (plural fallback) OR `recipient` | `String[]` | no | `[]` | Positional `nargs("*")`. |
| -g / --group-id / --group | `groupIds` (plural fallback) OR `groupId` | `String[]` | no | `[]` | Base64 GroupIds. |

**Result (response) wire shape:**

No `outputWriter.write(...)` — result is **`null`**. .NET returns `Task`.

**Validation rules** (UserErrorException / UnexpectedErrorException throws):
- `"Invalid phone number '<x>': <msg>"` — recipient parse failure.
- `"Invalid group id: <message>"` — `GroupIdFormatException`.
- `"This command doesn't work on linked devices."` — `NotPrimaryDeviceException` (`UnblockCommand.java:47, 58`).
- `"The user <identifier> is not registered."` — `UnregisteredRecipientException` (`UnblockCommand.java:50-52`).
- `"Failed to sync unblock to linked devices: <message>"` — `IOException` wrapper (`UnblockCommand.java:48-49, 62-63`) → `UnexpectedErrorException`.
- Unknown group id is logged at warn-level, processing continues (`UnblockCommand.java:60-61`).

**Error codes specific to this method:**
- `-1 UserError` — invalid recipient / invalid group-id / not-primary-device / unregistered recipient.
- `-32603 InternalError` — I/O sync failure.

**Side-effects:**
- Unblocks contacts and groups locally.
- Syncs unblock-state to linked devices.

**Enum values used:**
- None.

**Quirks / surprises:**
- Same partial-application caveat as `block`: recipients first, then groups; if first half throws, second is skipped.
- Same unknown-group-id-is-warning semantics — silent partial success.
- The error message texts differ from `block` by one word ("sync block" vs
  "sync unblock") — if writing a single shared parser, allow both.

---

## Cross-cutting notes

- **Plural-fallback for `nargs("*")` positional args:** argparse positional
  `recipient` with `nargs("*")` is materialized as a `List<String>` on
  `Namespace`. `JsonRpcNamespace.getList` first tries `super.getList(dest)`
  then falls back to `super.getList(dest + "s")`. So the wire field can be
  either `recipient: ["..."]` or `recipients: ["..."]`. Modern callers should
  use **plural** (`recipients`, `groupIds`) — that's the convention in other
  signal-cli surfaces and what the upstream daemon emits in generated schemas.
  The .NET DTOs should default to plural; document the legacy singular as a
  permissible alias only in XMLDoc.

- **`account` is required when more than one account is registered.** All eight
  Wave-3 commands implement `JsonRpcLocalCommand` (not `MultiLocal`), and
  consequently use `getManagerFromParams` to pick the manager from
  `params.account`. The single-account auto-select (`getManagers().getFirst()`
  if `size==1`, `SignalJsonRpcCommandHandler.java:107-112`) is a daemon
  convenience — at the .NET surface, model `account` as **required** on every
  DTO to prevent silent breakage when the consumer adds a second account.

- **No method in Wave 3 emits notifications.** All eight are request/response;
  none triggers a `subscribeReceive`-channel event. `block`/`unblock` /
  `updateContact` / `updateProfile` send protocol messages to other devices
  (linked-device sync), but these are upstream-of-RPC effects — they do not
  echo back as `receive` notifications to the calling client.

- **No method in Wave 3 returns a wrapper object.** Two (`listContacts`,
  `listIdentities`) return flat JSON arrays; six return `null` (void). Model
  the two list-returning methods with the `IReadOnlyList<T>`-wrapper-record +
  custom `JsonConverter` pattern per CLAUDE.md "AOT readiness" (see
  `Models/Signal/Accounts/ListAccountsResponse.cs` as canonical shape).

- **Enum exposure summary:**
  - `TrustLevel` (Wave 3) — emit on `listIdentities` result and `trust` is
    implicitly using `TRUSTED_VERIFIED` / `TRUSTED_UNVERIFIED`. Expose as
    `enum TrustLevel { Untrusted, TrustedUnverified, TrustedVerified }` with
    `JsonStringEnumConverter` mapping to Java UPPER_SNAKE.
  - `Profile.Capability` (Wave 3, only when `listContacts internal=true`) — Java
    enum identifiers are **lowercase/mixedCase** (`storage`,
    `storageServiceEncryptionV2Capability`), NOT UPPER_SNAKE. .NET should
    model as `string[]` with documentation listing the two known values, OR
    a `[Flags]` enum if `storageServiceEncryptionV2Capability` is the only
    consumer-visible bit (verify in upstream over the next minor releases).
  - `Profile.UnidentifiedAccessMode` (Wave 3, only when `listContacts internal=true`)
    — UPPER_SNAKE: `DISABLED`, `ENABLED`, `UNRESTRICTED`; `UNKNOWN` is mapped
    to `null` on the wire. Expose as nullable enum.
  - `PhoneNumberSharingMode` (Wave 3) — NOT emitted directly; surfaced as
    `sharesPhoneNumber: Boolean?` on `JsonInternal`. Do not expose the enum.

---

## Verification

Java source files read for this research (all at `bda4e7fc`):

- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/ListContactsCommand.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/ListIdentitiesCommand.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/TrustCommand.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/UpdateContactCommand.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/RemoveContactCommand.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/UpdateProfileCommand.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/BlockCommand.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/UnblockCommand.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/commands/JsonRpcNamespace.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/json/JsonContact.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/jsonrpc/SignalJsonRpcCommandHandler.java` (param-binding + error-code constants)
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/util/CommandUtil.java`
- `C:/Users/ivank/Нова папка/signal-cli/src/main/java/org/asamk/signal/util/Util.java` (dashSeparatedToCamelCaseString)
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/api/Contact.java`
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/api/Profile.java`
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/api/Identity.java`
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/api/TrustLevel.java`
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/api/IdentityVerificationCode.java`
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/api/UpdateProfile.java`
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/api/PhoneNumberSharingMode.java`
- `C:/Users/ivank/Нова папка/signal-cli/lib/src/main/java/org/asamk/signal/manager/api/RecipientIdentifier.java`
