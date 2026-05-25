## ADDED Requirements

### Requirement: `SignalEventService.OnNotificationReceived` SHALL dispatch union members via a generic helper

`src/SignalCli/Services/Signal/SignalEventService.cs` SHALL define a private generic helper:

```csharp
private bool DispatchUnionMember<TPayload, TArgs>(
    TPayload? payload,
    Func<TPayload, TArgs> makeArgs,
    Subject<TArgs> subject,
    Channel<TArgs> channel,
    string kindLabel)
    where TPayload : class
{
    if (payload is null) return false;
    var args = makeArgs(payload);
    subject.OnNext(args);
    TryWriteOrDrop(channel, args, kindLabel);
    return true;
}
```

All 13 union-member dispatch branches in `OnNotificationReceived` (6 pre-existing: text, reaction, sticker, attachments, remoteDelete, quote; 7 from Wave-7b: pollCreate, pollVote, pollTerminate, payment, pinMessage, unpinMessage, adminDelete) SHALL be rewritten as helper calls, using `emitted |= DispatchUnionMember(...)` to preserve the existing presence-based union semantics (critical rule #4).

The Quote-only branch retains its conditional gating on `!emitted` because it is the "tail" of the data-message dispatch (only fires when no other union member fired).

#### Scenario: All 13 union-member dispatch paths preserve previous behavior

- **GIVEN** an inbound `JsonRpcNotification` whose `dataMessage` contains exactly one of: text body, reaction, sticker, attachments, remoteDelete, pollCreate, pollVote, pollTerminate, payment, pinMessage, unpinMessage, adminDelete
- **WHEN** `OnNotificationReceived` dispatches via the new helper
- **THEN** the matching `Subject<T>` fires exactly once
- **AND** the matching `Channel<T>` receives exactly one item
- **AND** the dispatch is single-pass (no double-fire)

#### Scenario: Multiple union members in the same envelope all fire (critical rule #4 — presence-based union, no early return)

- **GIVEN** a `dataMessage` carrying both text body AND attachments
- **WHEN** dispatch runs
- **THEN** both the text-message AND attachment events fire
- **AND** `emitted` is `true` at the end of the data-message block
- **AND** the Quote-tail branch does NOT fire (gated on `!emitted`)

#### Scenario: Empty data-message logs `DataMessageEmpty` (existing behavior preserved)

- **GIVEN** a `dataMessage` with no recognizable payload (no text, no reaction, no sticker, no attachments, no remoteDelete, no quote, no Wave-7b fields)
- **WHEN** dispatch runs through all 13 helper calls
- **THEN** `emitted` stays `false`
- **AND** `SignalEventServiceLog.DataMessageEmpty` is invoked

### Requirement: Existing presence-based dispatch and event-symmetry tests SHALL continue to pass without modification

`EventApiSymmetryWave7bTests` (and any pre-existing presence-based dispatch suites covering the 6 original branches) SHALL pass unchanged. The refactor is behavior-preserving by construction; the helper's generic signature `<TPayload, TArgs>` + the `where TPayload : class` constraint mechanically prevent type-swap mistakes at compile time.

`Tests/SignalCli.Tests/RegressionGuards/SignalCli.public-api.txt` SHALL NOT change — the refactor is purely internal; the helper is `private`.

#### Scenario: Public-API baseline diff is empty after refactor commits land

- **WHEN** `dotnet test --filter PublicApiSurfaceTests` runs after the refactor commit
- **THEN** R03 emits zero diff lines against the baseline
