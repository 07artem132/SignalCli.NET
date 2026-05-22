## Context

Three independent modernization workstreams, grounded in Microsoft Learn and the signal-cli JSON-RPC spec:

- **Protocol (signal-cli)**: JSON-RPC 2.0 over stdin/stdout. Request `{jsonrpc:"2.0", method:<camelCase>, id, params}`; response `{jsonrpc, result|error{code,message,data}, id}`; incoming messages arrive as notifications with `method:"receive"` and an `envelope`; manual mode uses `subscribeReceive`/`unsubscribeReceive`. (man `signal-cli-jsonrpc.5`.)
- **STJ guidance**: source generation (`JsonSerializerContext`, metadata mode) cuts startup ~40%, reduces memory and is trim/AOT-safe; STJ is strict by default (case-sensitive, no string-enum, escapes more); `[JsonProperty]`→`[JsonPropertyName]`, string enums need `JsonStringEnumConverter<T>`.
- **Current state**: `Newtonsoft.Json` 13.0.3, `net9.0`, and three parallel readiness mechanisms in `SignalCliHostedService` (`ProcessStateManager` enum, `_readyTcsList`, `BehaviorSubject<StreamPair?>`).

## Goals / Non-Goals

**Goals:** LTS runtime; remove the public Newtonsoft dependency; one source of truth for process state; keep the public API stable apart from the documented breaking items.

**Non-Goals:** Native AOT/trimming end-to-end (only keep the door open via source-gen); rewriting the Rx event surface for consumers (`TextMessages` etc. stay `IObservable`); implementing new signal-cli verbs.

## Decisions

### A. .NET 10
- Bump TFM to `net10.0` in all projects; CI `setup-dotnet` → `10.0.x`.
- **Graceful shutdown**: in `StopProcessInternalAsyncNoLock`, launch with `ProcessStartInfo.CreateNewProcessGroup = true` (Windows) and send a group signal / `exit` before falling back to `Kill(entireProcessTree)`. Keep the 2s grace window; `Kill` becomes the last resort, not the default.
- *Alternative considered*: stay on net9.0 — rejected (out of support).

### B. System.Text.Json
- **Phased rollout (decided)**: Phase A migrates to STJ using the **reflection** resolver to isolate record-ctor/round-trip risk; Phase B adds the source-generated context on the already-green baseline. This separates "does binding work" from "does the generator cover all types".
- Add a `partial JsonSerializerContext` annotated with `[JsonSerializable]` for every RPC/model/event type (Phase B); use **metadata mode** (`JsonSourceGenerationMode.Metadata`) — fast-path doesn't support deserialization, which we need.
- Single shared `JsonSerializerOptions`: `PropertyNameCaseInsensitive = true` (Newtonsoft was case-insensitive by default — preserve behavior), `DefaultIgnoreCondition = WhenWritingNull` (replaces per-property `NullValueHandling.Ignore`), `TypeInfoResolver = SignalJsonContext.Default`.
- Attribute migration: `[JsonProperty("x")]` → `[JsonPropertyName("x")]`; keep records + `[JsonConstructor]`. `JsonSyncMessageType` enum → `[JsonConverter(typeof(JsonStringEnumConverter<JsonSyncMessageType>))]`.
- **The envelope is a presence-based union, not a discriminated hierarchy** → no `[JsonDerivedType]`/polymorphism needed. `JsonMessageEnvelope` stays one record with optional members; the existing null-check dispatch in `SignalEventService` is unchanged.
- `JsonRpcClient`: replace `JObject.Parse` + `JsonConvert` with `JsonDocument`/`JsonNode` to peek `id` vs `method`, then `JsonSerializer.Deserialize` against the context. `result`/`params` become `JsonElement` instead of `JToken`.
- *Alternative considered*: keep Newtonsoft — rejected (public dependency, project's own roadmap). Reflection-based STJ — rejected in favor of source-gen for perf/trim.

### C. Process-state unification
- `ProcessStateManager` becomes the single source of truth: it already holds `ProcessStateInfo` (state + `StreamPair` + error) in a `BehaviorSubject`. Expose `CurrentStreamPair`, `StreamPairChanged`, and `WaitForReadyAsync` as **projections** of that subject (e.g. `ProcessState.Select(s => s.StreamPair)` / `FirstAsync(ready)`), removing `_readyTcsList`, the separate `_streamPairSubject`, and manual triple-update.
- Either delete `IObservable<ProcessStateInfo>` (no current subscribers) or make it the backbone per above — **prefer the latter** so the Observer pattern earns its place.
- Collapse `IJsonRpcClientFactory` into `JsonRpcClientHostedService` (it produces exactly one client) unless a test seam justifies keeping it.

## Risks / Trade-offs

- **STJ round-trip differences** (case sensitivity, string enums, missing-member handling, escaping) → could silently change protocol behavior. Mitigation: add round-trip unit tests for every event/response type *before* swapping; diff against captured Newtonsoft output.
- **net10 process-group shutdown** behaves differently per OS → guard Windows-only API with `OperatingSystem.IsWindows()`; keep `Kill` fallback; cover with hosted-service tests.
- **State refactor regressions** in restart/health flows → the existing ~90% hosted-service/health tests are the safety net; run them after each step.
- **Breaking changes** (TFM, Newtonsoft removal) → bump the package major version; document in README.

## Migration Plan

1. **net10** (lowest risk): TFM + CI + README, then process-group shutdown. Run full suite.
2. **STJ** (highest risk): add context + options + attribute swap + `JsonRpcClient` rewrite; add round-trip tests; drop Newtonsoft. Run full suite.
3. **state-unification** (internal-only): derive readiness/stream-pair from `ProcessStateManager`; simplify factory. Run full suite.

Each step is independently shippable and reversible.
