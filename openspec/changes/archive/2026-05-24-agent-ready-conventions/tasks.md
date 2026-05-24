## 1. Agent guidance

- [x] 1.1 Add `CLAUDE.md` at repo root (project, build/test, architecture, conventions, critical audit rules, OpenSpec note)
- [x] 1.2 Added `.github/copilot-instructions.md` pointing to `CLAUDE.md`

## 2. Code-quality gates

- [x] 2.1 Add root `.editorconfig` with C# style + naming rules (private `_camelCase`, static `s_`, `I` interfaces, PascalCase, file-scoped namespaces, var usage, using placement)
- [x] 2.2 Add `Directory.Build.props` with `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`, keep `Nullable`; `TreatWarningsAsErrors=false`
- [x] 2.3 Triaged all analyzer warnings 288 → 0: tuned `.editorconfig` for justified noise (CA1848/CA1873 logging-perf, CA1711 EventArgs suffix, test-only CA1051/CA2201/CS8620, STJ-DTO CS8618→suggestion, protocol-enum CA1707) and fixed the real ones in code (CA1816, CA2016, CA1822, CA1305, CA1513/CA1510 throw-helpers, CA1727, CA1860, CS8600/CS8602, CS1574, removed obsolete `ISerializable`). Enabled `TreatWarningsAsErrors` on the shipped library to prevent regressions.
- [x] 2.4 Reviewed `SignalCli.sln.DotSettings` — only a ReSharper coverage filter (no style/naming rules); zero conflict with `.editorconfig`, left as-is

## 3. Exception handling

- [x] 3.1 Inventory all `catch (Exception)` / `throw new Exception` sites (24 catches; 0 `throw new Exception` remain — already fixed in audit)
- [x] 3.2 `throw new Exception` already replaced with specific types (Config → `InvalidOperationException`)
- [x] 3.3 Documented the 3 intentional loop-boundary broad catches (`JsonRpcClient` reader + `ProcessMessage`, `SignalCliHealthMonitor.MonitorLoop`, `SignalEventService.OnNotificationReceived`) with intent comments
- [x] 3.4 No silent swallowing: remaining broad catches log-and-rethrow (acceptable)

## 4. Verification

- [x] 4.1 `dotnet build SignalCli.sln` — 0 errors; analyzer diagnostics kept as warnings
- [x] 4.2 `dotnet test` — 95/95 green
- [x] 4.3 `openspec validate agent-ready-conventions` passes
