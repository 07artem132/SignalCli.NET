## ADDED Requirements

### Requirement: `ISignalMessage` SHALL expose 5 power-user messaging methods

`ISignalMessage` SHALL gain five new methods:

```csharp
Task<SendAdminDeleteResponse> SendAdminDeleteAsync(AdminDeleteOptions options, CancellationToken ct = default);
Task<SendPinMessageResponse> SendPinMessageAsync(PinMessageOptions options, CancellationToken ct = default);
Task<SendUnpinMessageResponse> SendUnpinMessageAsync(UnpinMessageOptions options, CancellationToken ct = default);
Task SendMessageRequestResponseAsync(MessageRequestResponseOptions options, CancellationToken ct = default);
Task<SendPaymentNotificationResponse> SendPaymentNotificationAsync(PaymentNotificationOptions options, CancellationToken ct = default);
```

`MessageRequestResponseOptions.Type` is a `MessageRequestResponseType` enum: `Accept` | `Delete` | `Block` | `BlockAndDelete` | `Unblock`.

`SendAdminDelete` requires the caller to be a group admin; on non-admin, signal-cli returns `-1 UserError` with "admin" in message → caller receives `GroupAdminRequiredException`.

#### Scenario: Admin deletes a member's message
- **GIVEN** caller is admin of group `<id>`, and a member's offensive message has `targetAuthor = "+3", targetTimestamp = 1700000000000L`
- **WHEN** `SendAdminDeleteAsync(AdminDeleteOptions(Account: "+1", GroupIds: ["<id>"], TargetAuthor: "+3", TargetTimestamp: 1700000000000L))` is invoked
- **THEN** RPC method `"sendAdminDelete"` is invoked
- **AND** the message is removed from all group members' clients

#### Scenario: Non-admin admin-delete attempt surfaces GroupAdminRequiredException
- **GIVEN** caller is NOT admin
- **WHEN** `SendAdminDeleteAsync` is invoked
- **THEN** signal-cli returns `-1` with "admin"-containing message
- **AND** the task faults with `GroupAdminRequiredException`

#### Scenario: Pin a message in a group
- **GIVEN** `PinMessageOptions(Account: "+1", GroupIds: ["<id>"], TargetAuthor: "+1", TargetTimestamp: 1700000000000L)`
- **WHEN** `SendPinMessageAsync(opts)` is invoked
- **THEN** RPC method `"sendPinMessage"` is invoked
- **AND** group members see the message in their pinned-list

#### Scenario: Unpin a previously pinned message
- **GIVEN** `UnpinMessageOptions` matching the pinned message id
- **WHEN** `SendUnpinMessageAsync(opts)` is invoked
- **THEN** RPC method `"sendUnpinMessage"` is invoked
- **AND** the message disappears from the pinned-list

#### Scenario: Accept a pending message request
- **GIVEN** `MessageRequestResponseOptions(Account: "+1", Recipient: "+2", Type: MessageRequestResponseType.Accept)`
- **WHEN** `SendMessageRequestResponseAsync(opts)` is invoked
- **THEN** RPC method `"sendMessageRequestResponse"` is invoked with `type: "ACCEPT"`
- **AND** linked devices sync the accept-state

#### Scenario: Block + delete on a message request
- **GIVEN** `Type: MessageRequestResponseType.BlockAndDelete`
- **WHEN** invoked
- **THEN** RPC payload contains `type: "BLOCK_AND_DELETE"`
- **AND** linked devices both block the sender AND delete the conversation thread

#### Scenario: Send a payment notification with receipt
- **GIVEN** `PaymentNotificationOptions(Account: "+1", Recipient: "+2", Note: "Thanks!", Receipt: <base64 receipt blob>)`
- **WHEN** `SendPaymentNotificationAsync(opts)` is invoked
- **THEN** RPC method `"sendPaymentNotification"` is invoked
- **AND** recipient receives the payment-notification message with receipt attached
