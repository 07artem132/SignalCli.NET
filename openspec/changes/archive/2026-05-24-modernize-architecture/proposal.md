## Why

SignalCli.NET targets `net9.0` (STS, support ended ~May 2026), serializes with `Newtonsoft.Json` (the README itself lists migration to System.Text.Json as a goal), and represents process readiness/state through three hand-synced mechanisms. Moving to .NET 10 (LTS, 3-year support), to System.Text.Json with source generation, and unifying the process-state representation modernizes the library, improves performance, and removes a class of consistency bugs — grounded in current Microsoft Learn guidance and the signal-cli JSON-RPC protocol spec.

## What Changes

- **.NET 10 upgrade**: bump `TargetFramework` `net9.0` → `net10.0` across all projects and CI; adopt `ProcessStartInfo.CreateNewProcessGroup` for graceful signal-cli shutdown instead of hard `Kill(entireProcessTree)`. **BREAKING** for consumers pinned to net9.0.
- **System.Text.Json migration**: replace `Newtonsoft.Json` with `System.Text.Json` end-to-end (RPC client, models, events). Use a source-generated `JsonSerializerContext` (metadata mode) for performance/trimming. Map `[JsonProperty]` → `[JsonPropertyName]`, the sync-message enum via `JsonStringEnumConverter<T>`, enable `PropertyNameCaseInsensitive`, and `DefaultIgnoreCondition = WhenWritingNull`. **BREAKING**: removes the public `Newtonsoft.Json` dependency and changes model attributes.
- **Process-state unification**: make `ProcessStateManager` the single source of truth; derive readiness (`WaitForReadyAsync`), `CurrentStreamPair`, and stream-pair change notifications from its state instead of maintaining three parallel mechanisms. Remove or genuinely use the currently-unsubscribed `IObservable<ProcessStateInfo>`. Collapse the `Factory → Provider → Singleton` chain for the single JSON-RPC client.

## Capabilities

### New Capabilities
- `net10-upgrade`: targeting .NET 10 and using its process-group APIs for graceful child-process shutdown.
- `json-serialization`: serializing/deserializing the signal-cli JSON-RPC protocol with System.Text.Json + source generation.
- `process-state-unification`: a single authoritative process-state model from which readiness and stream availability are derived.

### Modified Capabilities
<!-- None: no baseline specs exist in openspec/specs/. -->

## Impact

- Code: all 3 `.csproj` (TFM + package refs), `Models/Rpc/*`, `Models/Signal/*` (attributes), `Services/Rpc/JsonRpcClient.cs` (serializer + `JsonRpcNotificationRaw`), `Services/Signal/SignalEventService.cs`, `Services/SignalCli/{ProcessStateManager,SignalCliHostedService,ProcessWrapper}.cs`, `Services/Rpc/JsonRpcClientFactory.cs`, `Extensions/ServiceCollectionExtensions.cs`.
- CI: `.github/workflows/dotnet-desktop.yml` (`setup-dotnet` → `10.0.x`), README (`.NET 9` → `.NET 10`, dependency table).
- Sequencing/risk: three independent workstreams; recommended order **net10 → STJ → state-unification** so each lands and is tested separately. STJ is the highest-risk (round-trip behavior changes); state-unification is internal-only.
