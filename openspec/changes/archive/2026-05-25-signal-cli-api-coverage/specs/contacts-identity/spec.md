## ADDED Requirements

### Requirement: New `ISignalContacts` interface SHALL be added with 8 methods

A new `public interface SignalCli.Interfaces.Signal.ISignalContacts` SHALL be added, registered as singleton via `AddSignalCli`. It exposes:

```csharp
Task<ListContactsResponse> ListContactsAsync(string account, ListContactsFilter? filter = null, CancellationToken ct = default);
Task<ListIdentitiesResponse> ListIdentitiesAsync(string account, string? recipientFilter = null, CancellationToken ct = default);
Task TrustAsync(TrustOptions options, CancellationToken ct = default);
Task UpdateContactAsync(UpdateContactOptions options, CancellationToken ct = default);
Task RemoveContactAsync(string account, string recipient, RemoveContactBehavior behavior = RemoveContactBehavior.Hide, CancellationToken ct = default);
Task UpdateProfileAsync(UpdateProfileOptions options, CancellationToken ct = default);
Task BlockAsync(string account, IReadOnlyList<string> recipients, IReadOnlyList<string>? groupIds = null, CancellationToken ct = default);
Task UnblockAsync(string account, IReadOnlyList<string> recipients, IReadOnlyList<string>? groupIds = null, CancellationToken ct = default);
```

Each method invokes the matching signal-cli JSON-RPC method (`listContacts`, `listIdentities`, `trust`, `updateContact`, `removeContact`, `updateProfile`, `block`, `unblock`).

#### Scenario: List contacts returns address book
- **GIVEN** an account with synchronized contact list
- **WHEN** `await contacts.ListContactsAsync("+1")` is invoked
- **THEN** RPC method `"listContacts"` is called
- **AND** response is an `IReadOnlyList<Contact>` projecting each contact's number, name, profile-key, expiration, blocked flag

#### Scenario: List identities filtered by recipient
- **GIVEN** `recipientFilter = "+2"`
- **WHEN** `await contacts.ListIdentitiesAsync("+1", "+2")` is invoked
- **THEN** RPC payload contains `recipient: "+2"`
- **AND** response contains only identity entries for "+2"

#### Scenario: Trust an identity by safety number
- **GIVEN** `TrustOptions(Account: "+1", Recipient: "+2", Mode: TrustMode.VerifiedSafetyNumber, SafetyNumber: "01234 ...")`
- **WHEN** `TrustAsync` is invoked
- **THEN** RPC `trust` is called with the safety number
- **AND** subsequent `ListIdentitiesAsync` shows the identity with `TrustLevel = "TRUSTED_VERIFIED"`

#### Scenario: Trust all known (one-shot trust without safety-number verification)
- **GIVEN** `TrustOptions(Account: "+1", Recipient: "+2", Mode: TrustMode.TrustAllKnown)` (no safety number)
- **WHEN** `TrustAsync` is invoked
- **THEN** RPC payload contains `trustAllKnownKeys: true` and no safety-number field

#### Scenario: Update contact local name
- **GIVEN** `UpdateContactOptions(Account: "+1", Recipient: "+2", Name: "Alice")`
- **WHEN** `UpdateContactAsync` is invoked
- **THEN** RPC `updateContact` is called with `name: "Alice"`
- **AND** the change is local-only (not synced to recipient or other devices unless `sendContacts` is invoked next)

#### Scenario: Remove a contact with Hide behavior
- **GIVEN** `RemoveContactBehavior.Hide`
- **WHEN** `RemoveContactAsync` is invoked
- **THEN** RPC payload contains `hide: true`
- **AND** contact disappears from `ListContactsAsync` but local message history is preserved

#### Scenario: Update own profile with given/family name + about
- **GIVEN** `UpdateProfileOptions(Account: "+1", GivenName: "Alice", FamilyName: "Smith", About: "Hello world", AboutEmoji: "👋")`
- **WHEN** `UpdateProfileAsync` is invoked
- **THEN** RPC `updateProfile` is called with all four fields
- **AND** the change propagates via Signal's profile-key encryption (server-side)

#### Scenario: Block a contact
- **GIVEN** `account = "+1"`, `recipients = ["+2"]`
- **WHEN** `await contacts.BlockAsync("+1", ["+2"])` is invoked
- **THEN** RPC method `"block"` is called
- **AND** subsequent `SendTextMessageAsync` to "+2" fails server-side (signal-cli error)

#### Scenario: Unblock a previously blocked contact
- **GIVEN** previously blocked "+2"
- **WHEN** `UnblockAsync("+1", ["+2"])` is invoked
- **THEN** RPC method `"unblock"` is called
- **AND** messages to "+2" succeed again
