## ADDED Requirements

### Requirement: `ISignalAccounts` SHALL expose 3 non-destructive utility methods

In addition to existing `ListAccountsAsync` / `SendSyncRequestAsync` and Wave-6's destructive methods (gated by `EnableDestructiveOperations`), `ISignalAccounts` SHALL gain three non-destructive utility methods that work without the opt-in flag:

```csharp
Task<GetUserStatusResponse> GetUserStatusAsync(GetUserStatusOptions options, CancellationToken ct = default);
Task SendContactsAsync(string account, CancellationToken ct = default);
Task SubmitRateLimitChallengeAsync(string account, string challenge, string captcha, CancellationToken ct = default);
```

`GetUserStatusOptions` SHALL validate that exactly one of `Recipients` (list of phone numbers) or `Usernames` (list of usernames) is non-empty; mutually exclusive. Validation throws `ArgumentException("recipients/usernames mutually exclusive", "options")`.

#### Scenario: Check registration status of multiple numbers
- **GIVEN** `GetUserStatusOptions(Account: "+1", Recipients: ["+2", "+3", "+99999999999"])`
- **WHEN** `await accounts.GetUserStatusAsync(opts)` is invoked
- **THEN** RPC method `"getUserStatus"` is invoked with `recipient: ["+2", "+3", "+99999999999"]`
- **AND** response is `IReadOnlyList<UserStatus>` mapping each number to `Registered: bool` and optionally `Uuid: string`
- **AND** "+99999999999" returns `Registered: false`, "+2" returns `Registered: true`

#### Scenario: Check by usernames
- **GIVEN** `GetUserStatusOptions(Account: "+1", Usernames: ["alice.42"])`
- **WHEN** invoked
- **THEN** RPC payload contains `username: ["alice.42"]` and no `recipient` field

#### Scenario: Both recipients and usernames fails fast
- **GIVEN** both non-empty
- **WHEN** invoked
- **THEN** `ArgumentException` thrown BEFORE any RPC call

#### Scenario: Sync local contacts to linked devices
- **GIVEN** primary device with locally-updated contact names
- **WHEN** `await accounts.SendContactsAsync("+1")` is invoked
- **THEN** RPC method `"sendContacts"` is invoked
- **AND** linked devices receive the contact sync message

#### Scenario: Submit captcha to resolve rate-limit challenge
- **GIVEN** a previous RPC call faulted with `CaptchaRequiredException` carrying `challenge = "abc123"`
- **GIVEN** user solved captcha on https://signalcaptchas.org/registration/generate.html and obtained `captcha = "signalcaptcha://..."`
- **WHEN** `await accounts.SubmitRateLimitChallengeAsync("+1", "abc123", "signalcaptcha://...")` is invoked
- **THEN** RPC method `"submitRateLimitChallenge"` is invoked with both `challenge` and `captcha`
- **AND** subsequent rate-limited RPC calls succeed
