## ADDED Requirements

### Requirement: New `ISignalResources` interface SHALL expose 3 methods returning `byte[]`

A new `public interface SignalCli.Interfaces.Signal.ISignalResources` SHALL be registered as singleton via `AddSignalCli`:

```csharp
Task<byte[]> GetAttachmentAsync(string account, string id, string? recipient = null, string? groupId = null, CancellationToken ct = default);
Task<byte[]> GetAvatarAsync(string account, string? contact = null, string? groupId = null, string? profile = null, CancellationToken ct = default);
Task<byte[]> GetStickerAsync(string account, string packId, int stickerId, CancellationToken ct = default);
```

signal-cli returns the binary payload as a base64-encoded string in `result.data`. The service SHALL decode via `Convert.FromBase64String` and return the resulting `byte[]`. On invalid base64, the method SHALL throw `InvalidOperationException("invalid base64 payload from signal-cli")` (NOT silent empty array — fail loud, no defense-in-depth bypass).

For `GetAvatarAsync`, exactly one of `contact`, `groupId`, or `profile` SHALL be non-null; validation throws `ArgumentException` with paramName "options" before any RPC call.

#### Scenario: Fetch a previously-received attachment
- **GIVEN** an attachment was received with `id = "abc123"` for sender "+2"
- **WHEN** `await resources.GetAttachmentAsync("+1", "abc123", recipient: "+2")` is invoked
- **THEN** RPC method `"getAttachment"` is called with the id + recipient
- **AND** the returned `byte[]` is the decoded binary payload
- **AND** the byte array length matches the original attachment size

#### Scenario: Fetch a contact's avatar
- **GIVEN** "+2" has a public avatar
- **WHEN** `GetAvatarAsync("+1", contact: "+2")` is invoked
- **THEN** RPC method `"getAvatar"` is called
- **AND** returned `byte[]` is a decoded JPEG/PNG image

#### Scenario: Avatar with multiple targets fails fast
- **GIVEN** both `contact: "+2"` and `groupId: "<id>"` non-null
- **WHEN** `GetAvatarAsync` is invoked
- **THEN** `ArgumentException` is thrown BEFORE any RPC call

#### Scenario: Invalid base64 from signal-cli throws fail-loud
- **GIVEN** signal-cli responds with `{"result": {"data": "this-is-not-valid-base64-!!!"}}`
- **WHEN** `GetAttachmentAsync` awaits the response
- **THEN** `InvalidOperationException` is thrown with message `"invalid base64 payload from signal-cli"`
- **AND** the byte array is NOT empty-defaulted (fail-loud per critical rule #18 hardening philosophy)

#### Scenario: Fetch a sticker by pack id + sticker id
- **GIVEN** sticker pack `packId = "abc"` is installed
- **WHEN** `GetStickerAsync("+1", "abc", 3)` is invoked
- **THEN** RPC method `"getSticker"` is called with `packId` + `stickerId: 3`
- **AND** returned `byte[]` is the decoded sticker image
