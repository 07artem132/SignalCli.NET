## ADDED Requirements

### Requirement: `SignalCliOptions.EnableDestructiveOperations` SHALL gate 8 destructive lifecycle methods

A new `public bool EnableDestructiveOperations { get; set; } = false` property SHALL be added to `SignalCliOptions`. When `false` (the default), invoking any of the eight destructive methods listed below SHALL throw `InvalidOperationException` BEFORE any RPC call is made, with the message: `"Destructive operation '<methodName>' is disabled. Set SignalCliOptions.EnableDestructiveOperations = true to enable."`

`SignalAccounts` SHALL read the flag once in its constructor (per critical rule #10 — fail-fast at construction) and cache it in a `private readonly bool _destructiveOpsEnabled` field. A `private void EnsureDestructiveAllowed([CallerMemberName] string? method = null)` helper SHALL throw when disabled. Each destructive method SHALL call the helper as its first statement.

The eight destructive methods are:
- `UpdateAccountAsync` — change device-name, discoverable-by-number, etc.
- `UpdateConfigurationAsync` — sync configuration prefs across linked devices.
- `SetPinAsync` / `RemovePinAsync` — registration-lock PIN.
- `UnregisterAsync` — disable push, optionally delete account.
- `DeleteLocalAccountDataAsync` — remove local data.
- `StartChangeNumberAsync` / `FinishChangeNumberAsync` — change registered phone number.

#### Scenario: Destructive call without opt-in throws fail-fast
- **GIVEN** `SignalCliOptions.EnableDestructiveOperations` is `false` (default)
- **WHEN** consumer calls `await accounts.UnregisterAsync("+1")`
- **THEN** `InvalidOperationException` is thrown with message containing `"UnregisterAsync"` and `"EnableDestructiveOperations = true"`
- **AND** no `InvokeMethodAsync` call is made to signal-cli

#### Scenario: Destructive call with opt-in reaches RPC layer
- **GIVEN** `SignalCliOptions.EnableDestructiveOperations = true`
- **WHEN** `await accounts.UnregisterAsync("+1")` is invoked
- **THEN** RPC method `"unregister"` is invoked via `InvokeMethodAsync`

#### Scenario: Non-destructive methods are NOT gated
- **GIVEN** `EnableDestructiveOperations = false`
- **WHEN** `await accounts.ListAccountsAsync()` is invoked (existing, non-destructive)
- **THEN** the call proceeds normally without throwing

### Requirement: `ISignalAccounts` SHALL expose 8 destructive lifecycle methods

`ISignalAccounts` SHALL gain:

```csharp
Task UpdateAccountAsync(UpdateAccountOptions options, CancellationToken ct = default);
Task UpdateConfigurationAsync(UpdateConfigurationOptions options, CancellationToken ct = default);
Task SetPinAsync(string account, string pin, CancellationToken ct = default);
Task RemovePinAsync(string account, CancellationToken ct = default);
Task UnregisterAsync(string account, bool deleteAccount = false, CancellationToken ct = default);
Task DeleteLocalAccountDataAsync(string account, bool ignoreRegistered = false, CancellationToken ct = default);
Task<StartChangeNumberResponse> StartChangeNumberAsync(StartChangeNumberOptions options, CancellationToken ct = default);
Task FinishChangeNumberAsync(FinishChangeNumberOptions options, CancellationToken ct = default);
```

Each invokes the matching signal-cli JSON-RPC method. `UpdateConfigurationOptions` properties are `bool?`-nullable (tristate: set true / set false / leave unchanged via null), matching signal-cli's per-field semantics.

#### Scenario: Update device name
- **GIVEN** opt-in enabled and `UpdateAccountOptions(Account: "+1", DeviceName: "My Phone")`
- **WHEN** `UpdateAccountAsync(opts)` is invoked
- **THEN** RPC method `"updateAccount"` is invoked with `deviceName: "My Phone"`

#### Scenario: Update configuration with tristate
- **GIVEN** opt-in enabled, `UpdateConfigurationOptions(Account: "+1", ReadReceipts: true, TypingIndicators: null, LinkPreviews: false, UnidentifiedDeliveryIndicators: null)`
- **WHEN** `UpdateConfigurationAsync(opts)` is invoked
- **THEN** RPC payload contains `readReceipts: true` and `linkPreviews: false` but OMITS `typingIndicators` and `unidentifiedDeliveryIndicators` (the null fields)

#### Scenario: Set PIN
- **GIVEN** opt-in enabled
- **WHEN** `SetPinAsync("+1", "1234")` is invoked
- **THEN** RPC method `"setPin"` is invoked with `pin: "1234"`

#### Scenario: Unregister with delete-flag
- **GIVEN** opt-in enabled
- **WHEN** `UnregisterAsync("+1", deleteAccount: true)` is invoked
- **THEN** RPC method `"unregister"` is invoked with `deleteAccount: true`
- **AND** the account is destroyed server-side (irreversible)

#### Scenario: Start change-number flow
- **GIVEN** opt-in enabled and `StartChangeNumberOptions(Account: "+1", NewNumber: "+11", VoiceVerification: false)`
- **WHEN** `StartChangeNumberAsync(opts)` is invoked
- **THEN** RPC `"startChangeNumber"` is invoked
- **AND** SMS verification code is sent to "+11"

#### Scenario: Finish change-number flow with code
- **GIVEN** opt-in enabled, verification code received, and `FinishChangeNumberOptions(Account: "+1", NewNumber: "+11", VerificationCode: "123456")`
- **WHEN** `FinishChangeNumberAsync(opts)` is invoked
- **THEN** RPC `"finishChangeNumber"` is invoked
- **AND** account number becomes "+11" — subsequent `ListAccountsAsync` reflects new number
