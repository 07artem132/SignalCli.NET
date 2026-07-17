# Tasks — add-per-call-rpc-timeout (→ 4.10.2)

Single set of changes (fix + tests + version-bump + CHANGELOG у тому самому наборі, CLAUDE.md
version-CHANGELOG lockstep rule). DO NOT commit / push — лишити у working tree для рев'ю.

## 1. Transport seam (capability `add-per-call-rpc-timeout`)

- [x] 1.1 `IJsonRpcSender.InvokeMethodAsync` — додати ОСТАННІМ опц. `TimeSpan? timeout = null`
        + XMLDoc (per-call override; null/Zero → default; додатнє → override).
- [x] 1.2 `ISignalCliClient.InvokeMethodAsync` — те саме; XMLDoc зазначає API-additive.
- [x] 1.3 `SignalService.InvokeMethodAsync` (реалізація `ISignalCliClient`) — прокинути `timeout`
        у `_rpcClient.Client.InvokeMethodAsync(...)`.
- [x] 1.4 `JsonRpcClient.InvokeMethodAsync`:
        - валідація на межі: `timeout < TimeSpan.Zero` → `ArgumentOutOfRangeException`.
        - `effectiveTimeout = timeout is { } t && t > TimeSpan.Zero ? t : _requestTimeout`.
        - `new CancellationTokenSource(effectiveTimeout, _timeProvider)` (TimeProvider-aware
          overload — patterns.md; НЕ parameterless CTS).
        - `TimeoutException`-повідомлення показує `effectiveTimeout`.
        - XMLDoc: `<param name="timeout">` + `<exception ArgumentOutOfRangeException>`.

## 2. FinishLink surface (capability `finish-link-timeout`)

- [x] 2.1 `ISignalDevices.FinishLinkAsync` — додати ОСТАННІМ опц. `TimeSpan? timeout = null`;
        XMLDoc пояснює довгу interactive-фазу (ручний QR-скан). `StartLinkAsync` НЕ чіпати.
- [x] 2.2 `SignalDevices.FinishLinkAsync` — прокинути `timeout` у `InvokeMethodAsync`.

## 3. Public API baseline (RG03)

- [x] 3.1 `Tests/SignalCli.Tests/RegressionGuards/SignalCli.public-api.txt` — оновити 3 рядки
        (`IJsonRpcSender`, `ISignalCliClient` `InvokeMethodAsync`; `ISignalDevices.FinishLinkAsync`):
        додати `,System.Nullable<System.TimeSpan>` останнім параметром. Additive-only.

## 4. Tests (FakeTimeProvider — rule #11, НЕ Task.Delay)

- [x] 4.1 `Rpc/PerCallTimeoutTests.cs` — per-call коротший за default: `FakeTimeProvider.Advance`
        до per-call → TimeoutException; до default НЕ доходимо (TCS ще не завершений раніше).
- [x] 4.2 те саме — per-call ДОВШИЙ за default: Advance повз default → запит живий; повз per-call
        → TimeoutException. Доводить, що діє per-call, не default.
- [x] 4.3 `null` → поведінка = client default (Advance повз default → TimeoutException).
- [x] 4.4 від'ємний timeout → `ArgumentOutOfRangeException` (валідація на межі).
- [x] 4.5 `DeviceManagement/SignalDevicesCrudTests.cs` — `FinishLinkAsync` прокидає timeout у
        `ISignalCliClient` (мок: captured timeout != null і == очікуваному).

## 5. Build + test

- [x] 5.1 `dotnet build SignalCli.sln` — TreatWarningsAsErrors, 0 warnings в обох проектах.
- [x] 5.2 `dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj` — усі зелені, база ≥ 509
        не впала (514), усі RG-guard-и (R01–RG10) зелені.

## 6. Version + CHANGELOG + docs lockstep (same change-set)

- [x] 6.1 `Directory.Build.props` `<SignalCliPackageVersion>` 4.10.1 → 4.10.2.
- [x] 6.2 CHANGELOG `## [4.10.2] — 2026-07-17` (consumer-first voice; API-additive per-call timeout).
- [x] 6.3 `docs/api/devices.md` `FinishLinkAsync` — сигнатура + per-call-timeout нота.

## 7. Post-merge (out of scope цього набору змін)

- [ ] 7.1 Після merge: `openspec archive add-per-call-rpc-timeout --yes --skip-specs`
        + оновити CLAUDE.md "Implemented, merged, archived".
