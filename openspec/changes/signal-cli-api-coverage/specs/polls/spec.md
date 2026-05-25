## ADDED Requirements

### Requirement: `ISignalMessage` SHALL expose 3 poll-messaging methods

`ISignalMessage` SHALL gain three new methods invoking signal-cli's poll RPC methods:

```csharp
Task<SendPollCreateResponse> SendPollCreateAsync(PollCreateOptions options, CancellationToken ct = default);
Task<SendPollVoteResponse> SendPollVoteAsync(PollVoteOptions options, CancellationToken ct = default);
Task<SendPollTerminateResponse> SendPollTerminateAsync(PollTerminateOptions options, CancellationToken ct = default);
```

`PollCreateOptions` SHALL validate that `Options` has between 2 and 10 entries (signal-cli's poll limit per Signal protocol); violations throw `ArgumentException` with paramName "options".

#### Scenario: Create a poll with 3 options
- **GIVEN** `PollCreateOptions(Account: "+1", GroupIds: ["<id>"], Question: "Dinner?", Options: ["Pizza", "Burger", "Salad"], AllowMultipleVotes: false)`
- **WHEN** `SendPollCreateAsync(opts)` is invoked
- **THEN** RPC method `"sendPollCreate"` is invoked
- **AND** group members receive the poll message
- **AND** `SendPollCreateResponse.Timestamp` is populated (used as `PollTimestamp` for subsequent votes/terminate)

#### Scenario: Poll with too few options fails fast
- **GIVEN** `PollCreateOptions` with `Options: ["Only one"]`
- **WHEN** `SendPollCreateAsync(opts)` is invoked
- **THEN** `ArgumentException` is thrown BEFORE any RPC call

#### Scenario: Vote on an existing poll
- **GIVEN** poll was created with timestamp `1700000000000L`, and `PollVoteOptions(Account: "+1", GroupIds: ["<id>"], PollAuthor: "+2", PollTimestamp: 1700000000000L, SelectedOptionIndexes: [0])`
- **WHEN** `SendPollVoteAsync(opts)` is invoked
- **THEN** RPC method `"sendPollVote"` is invoked
- **AND** vote is registered for "Pizza" (index 0)

#### Scenario: Terminate (close) a poll
- **GIVEN** poll author wants to close further votes
- **WHEN** `SendPollTerminateAsync(PollTerminateOptions(Account: "+1", GroupIds: ["<id>"], PollTimestamp: 1700000000000L))` is invoked
- **THEN** RPC method `"sendPollTerminate"` is invoked
- **AND** subsequent vote attempts from other members are rejected server-side
