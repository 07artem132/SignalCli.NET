---
paths:
  - "src/SignalCli/**"
  - "src/SignalCli.HealthChecks/**"
  - "Tests/**"
  - "Example/**"
---

# Conventions (match the existing code)

- Modern C#: file-scoped namespaces, primary constructors, records for DTOs, `required`/collection expressions where natural, `Func<>`/`Action<>` over custom delegates.
- `var` only when the type is obvious; explicit type in `foreach`.
- `string`/`int` keywords, not `String`/`Int32`.
- `_camelCase` private fields, PascalCase public, `I`-prefixed interfaces.
- Always `.ConfigureAwait(false)` in library code.
- **Exceptions:** throw and catch *specific* types. A broad `catch (Exception)` is allowed **only** at long-running boundaries (the stdout reader loop, the health-monitor loop, the notification dispatcher) where one bad item must not kill the loop — and such catches must log and continue. Do not swallow exceptions silently elsewhere.
- **Comments and log messages are written in Ukrainian** in this codebase — match that when editing existing files.
- Keep XML doc comments on public members.
- **Namespace hierarchy.** Three `Services.*` namespaces, partitioned by domain (not by layer):
  - `Services.Rpc` — JSON-RPC transport (`JsonRpcClient`, `JsonRpcClientFactory`, `JsonRpcClientHostedService`). Knows nothing about Signal-specific RPC methods.
  - `Services.SignalCli` — signal-cli process management (`SignalCliHostedService`, `ProcessRunner`, `ProcessStateManager`, `ProcessFactory`, `ProcessWrapper`, `SignalCliHealthMonitor`). Knows about `signal-cli.jar` / Java / native binary; doesn't know JSON-RPC details.
  - `Services.Signal` — Signal-protocol facades on top of RPC (`SignalAccounts`, `SignalDevices`, `SignalGroups`, `SignalMessage`, `SignalService`, `SignalEventService`). Each facade is a thin typed wrapper around `ISignalCliClient.InvokeMethodAsync`.
- **DTO naming.** `*Parameters` for RPC request payloads (`SubscribeReceiveParameters`, `VersionParameters`, ...); `*Response` for RPC reply payloads (`VersionResponse`, `ListAccountsResponse`, ...). Both go under `Models/Signal/<DomainArea>/`. Every `*Parameters` / `*Response` type MUST be registered in `Serialization/SignalJsonContext.cs` via `[JsonSerializable(typeof(T))]` (build-failure-enforced by `JsonContextRegistrationTests` reflection guard — and the root-CLAUDE.md "source-gen JSON has no reflection fallback" rule).
- **Event-args records.** `*EventArgs` records for `ISignalEventService` Rx streams (`TextMessageEventArgs`, `ReactionEventArgs`, ...) live under `Models/Signal/Events/`. Each new `*EventArgs` MUST get paired `IObservable<T>` AND `IAsyncEnumerable<T>` on `ISignalEventService` (build-failure-enforced by `EventApiSymmetryTests` RG06).
- **Test-class naming and folder mirroring.** `*Tests` suffix; one class per file; folder mirrors namespace (`Tests/SignalCli.Tests/SignalCliHostedService/SignalCliHostedServiceLifecycleTests.cs` tests `src/SignalCli/Services/SignalCli/SignalCliHostedService.cs`). Regression-guard tests go under `Tests/SignalCli.Tests/RegressionGuards/` regardless of which production type they pin (cross-cutting structural invariants).
