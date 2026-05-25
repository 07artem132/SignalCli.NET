## ADDED Requirements

### Requirement: `ISignalMessage` SHALL expose `SendReactionAsync`

A `Task<SendReactionResponse> SendReactionAsync(ReactionOptions options, CancellationToken ct = default)` method SHALL be added to `ISignalMessage`. It invokes signal-cli's JSON-RPC `sendReaction` method, mapping options through `ReactionOptions.ToParameters()` into the wire payload (`account`, `recipient`/`groupId`, `emoji`, `targetAuthor`, `targetTimestamp`, `remove`).

`ReactionOptions` SHALL be a `sealed record` with a nested `Builder` mirroring `TextMessageOptions.Builder` ergonomics. `Account`, `TargetAuthor`, `TargetTimestamp`, `Emoji` are required; exactly one of `Recipients` or `GroupIds` SHALL be non-empty (validation throws `ArgumentException` with paramName "options").

#### Scenario: Reaction with emoji is sent to single recipient
- **GIVEN** `ReactionOptions` with `Account = "+1"`, `Recipients = ["+2"]`, `Emoji = "👍"`, `TargetAuthor = "+2"`, `TargetTimestamp = 1700000000000L`, `Remove = false`
- **WHEN** consumer calls `await signalMessage.SendReactionAsync(opts)`
- **THEN** `ISignalCliClient.InvokeMethodAsync` is invoked with method-name `"sendReaction"` and payload matching the documented signal-cli shape
- **AND** the returned `SendReactionResponse` exposes the wire `timestamp` + per-recipient `results`

#### Scenario: Reaction with both Recipients and GroupIds fails fast
- **GIVEN** `ReactionOptions` with both non-empty
- **WHEN** `SendReactionAsync` is invoked
- **THEN** `ArgumentException` is thrown with `paramName = "options"` BEFORE any RPC call

#### Scenario: Reaction to recipient who re-installed surfaces IdentityChangedException
- **GIVEN** signal-cli returns `{"error":{"code":-4,"message":"Untrusted identity"}}`
- **WHEN** `SendReactionAsync` awaits the RPC
- **THEN** the task faults with `IdentityChangedException` (which is also `UntrustedIdentityException` and `JsonRpcException`)

### Requirement: `ISignalMessage` SHALL expose `SendReceiptAsync`

A `Task<SendReceiptResponse> SendReceiptAsync(ReceiptOptions options, CancellationToken ct = default)` method SHALL invoke `sendReceipt`. `ReceiptOptions.Type` is a `ReceiptType` enum: `Read` | `Viewed` (serialized as `"read"`/`"viewed"`). Target timestamps SHALL be a non-empty `IReadOnlyList<long>`.

#### Scenario: Read-receipt for one message is sent
- **GIVEN** `ReceiptOptions(Account: "+1", Recipient: "+2", Type: ReceiptType.Read, TargetTimestamps: [1700000000000L])`
- **WHEN** `SendReceiptAsync` is invoked
- **THEN** RPC method `"sendReceipt"` is called with `type: "read"`
- **AND** `SendReceiptResponse.Timestamp` is populated from server response

### Requirement: `ISignalMessage` SHALL expose `SendTypingAsync`

A `Task<SendTypingResponse> SendTypingAsync(TypingOptions options, CancellationToken ct = default)` method SHALL invoke `sendTyping`. `TypingOptions.Stop` (default `false`) controls start-vs-stop semantics.

#### Scenario: Typing-start indicator
- **GIVEN** `TypingOptions(Account: "+1", Recipients: ["+2"], Stop: false)`
- **WHEN** `SendTypingAsync` is invoked
- **THEN** RPC payload sets `stop: false`

#### Scenario: Typing-stop indicator
- **GIVEN** same with `Stop: true`
- **THEN** RPC payload sets `stop: true`

### Requirement: `ISignalMessage` SHALL expose `RemoteDeleteAsync`

A `Task<RemoteDeleteResponse> RemoteDeleteAsync(RemoteDeleteOptions options, CancellationToken ct = default)` method SHALL invoke `remoteDelete` with `targetTimestamp` of the originally-sent message.

#### Scenario: Remote-delete of own previously-sent message
- **GIVEN** message previously sent with timestamp `1700000000000L`, and `RemoteDeleteOptions(Account: "+1", Recipients: ["+2"], TargetTimestamp: 1700000000000L)`
- **WHEN** `RemoteDeleteAsync` is invoked
- **THEN** RPC method `"remoteDelete"` is called with the matching `targetTimestamp`
- **AND** receiving clients SHALL replace the original message with a "this message was deleted" placeholder (signal-cli behavior contract)

### Requirement: `JsonRpcClient` SHALL dispatch new typed exceptions for error codes -6 and admin-related -1

`JsonRpcClient.InvokeMethodAsync`'s exception-mapping switch SHALL be extended to throw:
- `CaptchaRequiredException` when `error.code == -6 (CaptchaRejected)`
- `GroupAdminRequiredException` when `error.code == -1 (UserError)` AND `error.message` contains the substring `"admin"` (case-insensitive)

`IdentityChangedException` SHALL be defined as a `sealed` subclass of `UntrustedIdentityException` (code -4) — it inherits the existing -4 dispatch and is `catch`-able as either `IdentityChangedException` (semantic re-install case) or `UntrustedIdentityException` (broader -4) by consumer choice.

#### Scenario: Captcha-rejected response surfaces CaptchaRequiredException
- **GIVEN** an in-flight RPC call
- **WHEN** signal-cli emits `{"id":"1","error":{"code":-6,"message":"Captcha required"}}`
- **THEN** the task faults with `CaptchaRequiredException`
- **AND** `ex.KnownCode == JsonRpcErrorCode.CaptchaRejected`
- **AND** `catch (JsonRpcException)` still catches it (backward compat)

#### Scenario: Group-admin-required error surfaces GroupAdminRequiredException
- **GIVEN** consumer calls a group-modification RPC without admin rights
- **WHEN** signal-cli emits `{"id":"1","error":{"code":-1,"message":"Only group admins can perform this action"}}`
- **THEN** the task faults with `GroupAdminRequiredException`
- **AND** `catch (JsonRpcException)` still catches it
