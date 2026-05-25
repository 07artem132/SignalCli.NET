# Research notes — signal-cli source-of-truth

Per **`tasks.md §0.5`** anti-hallucination protocol, кожен з 44 нових RPC методів
МУСИТЬ мати запис у `wave-<N>-<capability>.md` з прочитаною (НЕ вгаданою)
shape upstream Java records.

## Pinned reference

- **signal-cli local clone:** `C:\Users\ivank\Нова папка\signal-cli` (sibling до repo).
- **Pinned commit SHA:** `bda4e7fc` ("Prepare next release", 2026-05-24).
- **Tag:** `v0.14.4.1` HEAD.
- **Це той самий commit, що `.claude/rules/signal-cli-protocol.md` цитує** для семи "signal-cli protocol behavior we depend on" фактів. Перевіряти drift не потрібно — citation baseline валідний.

## Per-method template (mandatory)

Для кожного RPC method:

```markdown
### `<methodName>` — Wave N

**Source citation:**
- Command: `src/main/java/org/asamk/signal/commands/<X>Command.java @ bda4e7fc`
- Manager API (if separate): `lib/src/main/java/org/asamk/signal/manager/api/<Y>.java @ bda4e7fc`
- Wire records (receive-side only): `src/main/java/org/asamk/signal/json/Json<Z>.java @ bda4e7fc`

**Params (request) wire shape:**

| Field | JSON name | Java type | Required? | Default | Notes |
|---|---|---|---|---|---|
| `account` | `account` | `String` | yes | — | E.164 phone number |
| ... | ... | ... | ... | ... | ... |

**Result (response) wire shape:**

| Field | JSON name | Java type | Notes |
|---|---|---|---|
| ... | ... | ... | ... |

**Validation rules** (UserErrorException throws):
- `<exact message>` коли `<condition>`
- ...

**Error codes specific to this method:**
- `-1 UserError` — bad input X
- `-4 UntrustedIdentity` — only for send-side
- ...

**Side-effects:**
- Mutates state? read-only? triggers notifications?

**Enum values used:**
- `<EnumType>`: VALUE1 | VALUE2 | ... (UPPER_SNAKE_CASE from Java; expose as PascalCase in .NET)

**Quirks / surprises:**
- (anything that contradicts the obvious naming/shape — e.g. snake_case override, optional field with non-null default)
```

## Wave-to-file mapping

- `wave-1-messaging-interactive.md` — sendReaction, sendReceipt, sendTyping, remoteDelete
- `wave-2-groups-crud.md` — joinGroup, updateGroup, quitGroup
- `wave-3-contacts-identity.md` — listContacts, listIdentities, trust, updateContact, removeContact, updateProfile, block, unblock
- `wave-4-sticker-packs-binary-resource-fetch.md` — uploadStickerPack, listStickerPacks, addStickerPack, getAttachment, getAvatar, getSticker + sticker-pack-install sync-event decoder (`JsonSyncMessage` extension)
- `wave-5-device-management.md` — addDevice, listDevices, removeDevice, updateDevice
- `wave-6-account-lifecycle.md` — updateAccount, unregister, deleteLocalAccountData, startChangeNumber, finishChangeNumber, updateConfiguration, setPin, removePin (**destructive — gated by `EnableDestructiveOperations`**)
- `wave-7-polls-power-user.md` — sendPollCreate, sendPollVote, sendPollTerminate, sendAdminDelete, sendPinMessage, sendUnpinMessage, sendMessageRequestResponse, sendPaymentNotification + 7 receive-side decoders (JsonPollCreate/JsonPollVote/JsonPollTerminate/JsonPayment/JsonPinMessage/JsonUnpinMessage/JsonAdminDelete)
- `wave-8-utility-rpc.md` — getUserStatus, submitRateLimitChallenge, sendContacts

## How these notes will be consumed

Wave-N implementation PR pulls from `wave-N-<capability>.md` to:

1. Generate DTO shapes (`*Parameters`, `*Response`, `*Options`).
2. Generate `[JsonPropertyName]` overrides where Jackson names differ from camelCase.
3. Generate enum values (PascalCase mapped from Java UPPER_SNAKE).
4. Wire validation rules into service-layer guards.
5. Cite `src/main/java/org/asamk/signal/commands/<X>Command.java @ bda4e7fc` у XMLDoc
   `<remarks>` per RG10 build-time guard.

Якщо у `wave-N-<capability>.md` для якогось method'у відсутня клітинка таблиці —
implementation МУСИТЬ повернутись і прочитати source, а не вгадати default.
