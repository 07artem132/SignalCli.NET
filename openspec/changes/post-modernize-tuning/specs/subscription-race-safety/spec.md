## ADDED Requirements

### Requirement: Subscribe is atomic with respect to signal-cli
`SignalEventService.SubscribeAsync(account)` SHALL ensure that, for any given `account`, at most one `subscribeReceive` RPC is sent to `signal-cli`, regardless of how many callers invoke `SubscribeAsync` concurrently with the same account. A reservation placeholder MUST be inserted into the subscription map under the lock before the RPC is sent.

#### Scenario: Many callers race on the same account
- **GIVEN** no existing subscription for `account = "+380501234567"`
- **WHEN** `Task.WhenAll(Enumerable.Range(0, 10).Select(_ => svc.SubscribeAsync(account)))` runs
- **THEN** exactly one `subscribeReceive` RPC is sent to `signal-cli`
- **AND** exactly one caller's task completes successfully
- **AND** the other 9 tasks fault with `InvalidOperationException` containing "вже підписаний"

#### Scenario: RPC failure rolls the reservation back
- **GIVEN** a `SubscribeAsync(account)` call whose RPC to `signal-cli` faults with `TimeoutException`
- **WHEN** the exception propagates
- **THEN** the reservation placeholder is removed from the subscription map
- **AND** a subsequent `SubscribeAsync(account)` is allowed to proceed

### Requirement: Unsubscribe ignores placeholders
`UnsubscribeAsync(subscriptionId)` SHALL treat the reservation sentinel value as "no live subscription" and SHALL NOT invoke `unsubscribeReceive` on `signal-cli` for it.

#### Scenario: Unsubscribe while a reservation is in flight
- **GIVEN** `SubscribeAsync(account)` is in flight (reservation present, RPC pending)
- **WHEN** `UnsubscribeAsync(reservationId)` is called by another thread
- **THEN** no `unsubscribeReceive` RPC is sent
- **AND** the in-flight `SubscribeAsync` proceeds as normal

### Requirement: Subscribe/Unsubscribe reject calls on a disposed service
`SubscribeAsync` and `UnsubscribeAsync` SHALL throw `ObjectDisposedException` when invoked after `Dispose`, so callers see a stable, documented error instead of a silent failure deep in the dispatch chain.

#### Scenario: Subscribe after dispose
- **GIVEN** the `ISignalEventService` has been disposed
- **WHEN** `SubscribeAsync(account)` is called
- **THEN** the call throws `ObjectDisposedException`
- **AND** no RPC is sent
