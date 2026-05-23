## ADDED Requirements

### Requirement: State transitions are reentrant-safe
`ProcessStateManager.UpdateState` SHALL NOT hold its internal lock while invoking `IObserver<T>.OnNext` on subscribers. The state snapshot SHALL be produced under the lock and the publication SHALL happen after the lock is released, so a synchronous subscriber that re-enters the manager cannot deadlock.

#### Scenario: Synchronous subscriber re-enters during emission
- **GIVEN** an `IObservable<ProcessStateInfo>` subscriber that, on receiving `Starting`, synchronously reads `CurrentState`
- **WHEN** `UpdateState(Starting)` is called
- **THEN** the subscriber's read returns `Starting`
- **AND** the call completes without deadlock

#### Scenario: Multiple observers serialize in publication order
- **GIVEN** two observers `A` and `B`
- **WHEN** `UpdateState` is called concurrently from threads T1 and T2
- **THEN** every observer sees the same total order of state-info snapshots
- **AND** no observer observes an `OnNext` after the subject's `OnCompleted`

### Requirement: Disposal race window is collapsed to a documented try/catch
A `Dispose` racing with an `UpdateState` SHALL NOT corrupt observer state. If `OnNext` happens to execute against a just-disposed `BehaviorSubject`, the resulting `ObjectDisposedException` SHALL be caught and ignored at the publication site, with the rationale documented inline.

#### Scenario: Dispose between snapshot and OnNext
- **GIVEN** `UpdateState` has captured a snapshot and released the lock
- **WHEN** `Dispose` runs and completes before `OnNext` fires
- **THEN** the `OnNext` call observes the disposal and the manager swallows the resulting `ObjectDisposedException`
- **AND** no exception propagates to the caller of `UpdateState`

### Requirement: Disposed flag is lock-free readable
The disposed flag on long-running services (`SignalCliHostedService`, `JsonRpcClient`, `JsonRpcClientHostedService`, `SignalEventService`) SHALL be readable without taking a lock — via `Volatile.Read` or `Interlocked` — so background callbacks (`OnProcessExited`, `OnStreamPairChanged`, notification dispatch) can short-circuit cheaply and race-free.

#### Scenario: Background callback after dispose
- **GIVEN** the service has been disposed
- **WHEN** a background callback runs after dispose
- **THEN** the callback observes the disposed flag and returns without mutating state
- **AND** no `NullReferenceException` is thrown from accessing torn-down dependencies
