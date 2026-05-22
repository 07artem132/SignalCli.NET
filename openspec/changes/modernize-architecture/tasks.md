## 1. .NET 10 upgrade (do first, lowest risk)

- [x] 1.1 Bump `<TargetFramework>` `net9.0` → `net10.0` in all projects (src, runtime, Tests, Example)
- [x] 1.2 Update CI `dotnet-desktop.yml`: `setup-dotnet` → `10.0.x`
- [x] 1.3 Update README badges/requirements (`.NET 9` → `.NET 10 (LTS)`); bump Extensions packages to 10.0.0
- [x] 1.4 `dotnet build SignalCli.sln` + full test run green on .NET 10

## 2. Graceful shutdown via process group (net10)

- [x] 2.1 Add `ProcessConfig.CreateNewProcessGroup`; set it (Windows-guarded) in `ProcessRunner`; enable in `Config.ToProcessConfig`
- [x] 2.2 Existing `StopProcessInternalAsyncNoLock` already sends `exit` first, waits 2s, and `Kill(entireProcessTree)` only as fallback — graceful path preserved
- [x] 2.3 Dedicated graceful-exit (no force-kill) vs hang (kill fallback) tests; made the stop grace period configurable (`Config.StopTimeoutSeconds`, default 2s) — also cut test-suite time 31s→10s

## 3. STJ — Phase A: migrate via reflection (highest risk) ✅ DONE & GREEN

- [x] 3.1 Round-trip tests added (`JsonSerializationTests`: request, response, error, envelope composite, sync-enum, case-insensitive)
- [x] 3.2 Shared `JsonSerializerOptions` (`SignalJson.Options`): case-insensitive, WhenWritingNull, AllowTrailingCommas, relaxed encoder
- [x] 3.3 `[JsonProperty]` → `[JsonPropertyName]` across 16 model files (scripted)
- [x] 3.4 `JsonSyncMessageType` → `JsonStringEnumConverter<>`; dropped `NullValueHandling.Ignore`; removed `[JsonArray]`
- [x] 3.5 Rewrote `JsonRpcClient` (`JsonDocument`/`JsonElement`); `JToken` → `JsonElement` in models; `JsonRpcRequest.Params` → `JsonElement`; relaxed constraint `class` → `notnull`
- [x] 3.6 `SignalEventService` subscribe path → `JsonElement.GetInt32`
- [x] 3.7 Removed `Newtonsoft.Json` PackageReference; updated 2 existing tests off `JToken`
- [x] 3.8 README dependency table updated (Newtonsoft → System.Text.Json)
- [x] 3.9 Full test run green (95/95) → **Phase A shipped**

## 4. STJ — Phase B: source generation ✅ DONE & GREEN

- [x] 4.1 `SignalJsonContext : JsonSerializerContext` (metadata mode), `[JsonSerializable]` for all RPC/model/event roots
- [x] 4.2 `TypeInfoResolver = Combine(SignalJsonContext.Default, DefaultJsonTypeInfoResolver())` (reflection fallback for anon/JsonElement)
- [x] 4.3 Round-trip suite passes unchanged (95/95) — context covers all types
- [ ] 4.4 (Optional) `JsonSerializerIsReflectionEnabledByDefault=false` — deferred (would drop reflection fallback; needs anon-type removal)

## 5. Process-state unification ✅ DONE & GREEN

- [x] 5.1 `ProcessStateManager` is now the single source of truth (state + stream pair + error)
- [x] 5.2 `CurrentStreamPair`/`StreamPairChanged` derived from the state subject (`Select`+`DistinctUntilChanged`); removed `_streamPairSubject`; kept a private field only for resource disposal
- [x] 5.3 `WaitForReadyAsync` derived from the state subject (`Where`+`Take(1)`+`ToTask`); removed `_readyTcsList`/`SucceedReady`/`FailReady`/`UpdateStreamPair`
- [x] 5.4 The state `IObservable` is genuinely consumed (integration/restart tests + now the streampair/ready derivations) — NOT dead; kept
- [~] 5.5 `IJsonRpcClientFactory` kept as a deliberate test seam (the escape hatch in the design) — `JsonRpcClientHostedServiceTests` mock + verify it
- [x] 5.6 Behavior parity: 95/95 green; only 2 implementation-detail assertions updated (failed-start null-count collapse; dispose-during-start now surfaces the real `InvalidOperationException` instead of an `ObjectDisposedException` from a disposed `Subject`)

## 6. Verification

- [x] 6.1 `dotnet build SignalCli.sln` — 0 errors
- [x] 6.2 `dotnet test` — 95/95 green (existing + new round-trip)
- [x] 6.3 `openspec validate modernize-architecture` passes
- [x] 6.4 Bumped package version 1.0.3 → 2.0.0 + added `CHANGELOG.md` documenting the breaking changes (TFM, Newtonsoft removal, API changes)
