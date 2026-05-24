## ADDED Requirements

### Requirement: Long-running loops use BackgroundService
Long-running loop-driven hosted services SHALL inherit from `Microsoft.Extensions.Hosting.BackgroundService` and put their loop in `ExecuteAsync(stoppingToken)`. Hand-rolled `Task.Run(MonitorLoop)` + bespoke `CancellationTokenSource` plumbing SHALL NOT remain for cases that fit the `BackgroundService` shape (Microsoft *Worker Services in .NET*).

#### Scenario: Health monitor lifecycle
- **GIVEN** `SignalCliHealthMonitor` is registered as a hosted service
- **WHEN** the host calls `StopAsync`
- **THEN** the host blocks in `StopAsync` waiting for `ExecuteAsync` to return (BackgroundService semantics)
- **AND** the loop observes `stoppingToken` and exits promptly

#### Scenario: Startup is not blocked on the loop
- **WHEN** the host starts in .NET 10
- **THEN** `ExecuteAsync` runs entirely on a background thread (.NET 10 behavior)
- **AND** other hosted services start without waiting for the monitor's first iteration

### Requirement: Startup order is expressed via IHostedLifecycleService
Hosted services whose `StartAsync` depends on another service being "ready" SHALL participate in the `IHostedLifecycleService` lifecycle (Microsoft *Generic Host — IHostApplicationLifetime*). Implicit "registration order" SHALL NOT be the only mechanism that guarantees correct startup ordering between `SignalCliHostedService` and `JsonRpcClientHostedService`.

#### Scenario: SignalCli process is ready before JSON-RPC client starts
- **GIVEN** `SignalCliHostedService.StartedAsync` runs to completion
- **WHEN** the host advances to the next lifecycle phase
- **THEN** `JsonRpcClientHostedService.StartedAsync` is invoked
- **AND** it observes a non-null `StreamPair` from the very first call to `_streamProvider.CurrentStreamPair`

### Requirement: SignalCliHostedService is async-disposable
`SignalCliHostedService` SHALL implement `IAsyncDisposable` (in addition to or instead of `IDisposable`) so its disposal — which currently kills the child process, completes Rx subjects, and clears restart-timer CTS — can be awaited rather than executed synchronously inside `Dispose`.

#### Scenario: Host disposes the hosted service
- **WHEN** the DI container disposes `SignalCliHostedService`
- **THEN** `DisposeAsync` is preferred over `Dispose`
- **AND** the process is killed and Rx subjects completed without any sync-over-async patterns

### Requirement: Sync methods are not wrapped in Task
Internal methods that perform no `await` and have no I/O SHALL NOT return `Task<T>` via `Task.FromResult(...)`. Prefer synchronous methods or `ValueTask<T>.FromResult` (Microsoft *ValueTask* — for synchronous results on async-shaped APIs).

#### Scenario: ProcessRunner.StartProcessWithHandle is synchronous-friendly
- **WHEN** the method is called
- **THEN** it returns `(IProcess, StreamPair)` synchronously OR returns `ValueTask<(IProcess, StreamPair)>` with `ValueTask.FromResult`
- **AND** the hot-path caller does not allocate a `Task` for purely synchronous work
