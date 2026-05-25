# Design — claude-md-pattern-additions

## Method

Four text-additions to existing CLAUDE.md sections. No structural reorganization, no rule deletions, no cross-reference rewrites. Pure-additive doc PR. Total ~35 lines across 4 insertion points.

Co-existence strategy with `claude-md-rules-restructure` (plan-only, separate change):

- **If this change executes FIRST**: additions land in monolithic CLAUDE.md. When restructure executes later, the additions move to `.claude/rules/patterns.md` (DI + exceptions) and `.claude/rules/conventions.md` (naming) as part of the verbatim section-move per restructure's section-to-file mapping.
- **If restructure executes FIRST**: this change's additions go directly into `.claude/rules/patterns.md` and `.claude/rules/conventions.md` (skipping the monolithic-CLAUDE.md interim).
- **If executed concurrently**: rebase-conflict on CLAUDE.md is mechanical — additions slot into the post-restructure file layout (one-line resolve per addition).

## Exact additions

### Addition 1 — "Established patterns → DI registration" (NEW subsection)

**Location**: insert after existing subsection (currently around line 167 "#### Background loops + time", BEFORE it). Forms a logical pair with the existing "Other established patterns" subsection at end.

**Text** (~10 lines):

```markdown
#### DI registration

- **`TryAddSingleton<T>` over `AddSingleton<T>` for our own services.** `TryAdd*` lets a consumer's prior `services.Replace(...)` survive — useful for testing (`FakeTimeProvider` injection) and for consumers swapping our defaults. Reserve plain `Add*` for `IHostedService` registrations: there we DO want every call to register an additional hosted-service descriptor (host iterates over them); deduplication would silently drop our background work.
- **"One-instance-two-roles" idiom for services that ARE hosted services AND must also resolve via a typed/interface accessor.** Register the concrete once via `services.TryAddSingleton<TConcrete>()`, then forward both the hosted-service slot and the interface adapter to that single instance via factory delegates:
  ```csharp
  services.TryAddSingleton<SignalCliHostedService>();
  services.AddHostedService(sp => sp.GetRequiredService<SignalCliHostedService>());
  services.TryAddSingleton<IStreamPairProvider>(sp => sp.GetRequiredService<SignalCliHostedService>());
  ```
  Canonical sites: `SignalCliHostedService` (concrete + hosted + `IStreamPairProvider`), `JsonRpcClientHostedService` (concrete + hosted + `IJsonRpcClientProvider`), `SignalEventService` (concrete + hosted). One DI instance, three resolution paths, zero risk of "two instances, one host-registered, one not".
- **`AddSignalCli` idempotency via private sentinel-type marker.** Both overloads first check `services.Any(d => d.ServiceType == typeof(SignalCliRegistrationMarker))`; if present, short-circuit. The marker type itself is `private sealed class SignalCliRegistrationMarker {}` inside `ServiceCollectionExtensions` — consumers can't accidentally register or check it. **Do NOT replace with `services.Any(d => d.ServiceType == typeof(IOptions<SignalCliOptions>))`** — that's what was tried originally, but `IOptions<T>` registers as open-generic (`typeof(IOptions<>)`), not concrete; the check always returned false and every repeated `AddSignalCli` call duplicated 3 hosted-service descriptors → double-startup. Full failure-mode rationale in `audit-followup-2026/addsignalcli-idempotency-fix` (4.0.0).
```

### Addition 2 — "Conventions" section additions

**Location**: insert at end of existing "Conventions (match the existing code)" section (currently ending around line 131, before "### Established patterns" subheading).

**Text** (~10 lines):

```markdown
- **Namespace hierarchy.** Three `Services.*` namespaces, partitioned by domain (not by layer):
  - `Services.Rpc` — JSON-RPC transport (`JsonRpcClient`, `JsonRpcClientFactory`, `JsonRpcClientHostedService`). Knows nothing about Signal-specific RPC methods.
  - `Services.SignalCli` — signal-cli process management (`SignalCliHostedService`, `ProcessRunner`, `ProcessStateManager`, `ProcessFactory`, `ProcessWrapper`, `SignalCliHealthMonitor`). Knows about `signal-cli.jar` / Java / native binary; doesn't know JSON-RPC details.
  - `Services.Signal` — Signal-protocol facades on top of RPC (`SignalAccounts`, `SignalDevices`, `SignalGroups`, `SignalMessage`, `SignalService`, `SignalEventService`). Each facade is a thin typed wrapper around `ISignalCliClient.InvokeMethodAsync`.
- **DTO naming.** `*Parameters` for RPC request payloads (`SubscribeReceiveParameters`, `VersionParameters`, ...); `*Response` for RPC reply payloads (`VersionResponse`, `ListAccountsResponse`, ...). Both go under `Models/Signal/<DomainArea>/`. Every `*Parameters` / `*Response` type MUST be registered in `Serialization/SignalJsonContext.cs` via `[JsonSerializable(typeof(T))]` (build-failure-enforced by `JsonContextRegistrationTests` reflection guard — see Critical rule #13).
- **Event-args records.** `*EventArgs` records for `ISignalEventService` Rx streams (`TextMessageEventArgs`, `ReactionEventArgs`, ...) live under `Models/Signal/Events/`. Each new `*EventArgs` MUST get paired `IObservable<T>` AND `IAsyncEnumerable<T>` on `ISignalEventService` (build-failure-enforced by `EventApiSymmetryTests` RG06).
- **Test-class naming and folder mirroring.** `*Tests` suffix; one class per file; folder mirrors namespace (`Tests/SignalCli.Tests/SignalCliHostedService/SignalCliHostedServiceLifecycleTests.cs` tests `src/SignalCli/Services/SignalCli/SignalCliHostedService.cs`). Regression-guard tests go under `Tests/SignalCli.Tests/RegressionGuards/` regardless of which production type they pin (cross-cutting structural invariants).
```

### Addition 3 — "Other established patterns" subsection addition

**Location**: insert as final bullet in existing "#### Other established patterns" subsection (currently around line 187-190).

**Text** (~3 lines):

```markdown
- **Derive a typed exception only for "consumer-actionable, high-frequency" RPC error codes.** Current derived types: `RateLimitException` (signal-cli code `-5`, consumers retry with backoff) and `UntrustedIdentityException` (`-4`, consumers verify safety-number then resend). Other signal-cli codes (`-1` UserError, `-3` IoError, `-6` CaptchaRejected) stay base `JsonRpcException` because consumers typically just log + surface — no actionable typed-catch is expected. Heuristic when adding a new derived type: "Would `catch (XxxException)` lead to materially different consumer code than `catch (JsonRpcException) when (ex.KnownCode == JsonRpcErrorCode.Xxx)`?" If yes, derive. If no, don't — the base + `KnownCode` enum is sufficient and avoids exception-hierarchy bloat.
```

### Addition 4 — "README voice + drift rules" new subsection

**Location**: insert after existing `### CHANGELOG voice template` subsection (currently around line 419, end of "Audit baseline → Version-CHANGELOG lockstep" block). Sibling placement makes both outward-facing doc-rules co-located and discoverable as a pair.

**Text** (~13 lines):

```markdown
### README voice + drift rules

- **README is consumer-facing; CLAUDE.md is contributor-facing.** Different audience, different voice. README answers "what is this, do I want it, how do I use it?" — first 200 chars must hook (NuGet.org renders this as package teaser via `<PackageReadmeFile>`). CLAUDE.md answers "I'm editing the code, what must I not break?" — verbosity acceptable, internal IDs acceptable.

- **No internal IDs in README body** — `NF-003`, `RG05`, capability-slug references, audit-version mentions belong in CHANGELOG / OpenSpec / CLAUDE.md, NOT in README prose. **One exception**: the 4.0 migration `<details>` collapsible can reference capability slugs as historical migration anchors (consumer who's upgrading from 3.x benefits from "this is what we called the cleanup").

- **Quick-start must compile against current API verbatim.** Single copy-paste-working block, ≤30 lines, no deleted-type references (`Config`, `*Async`-suffix-less methods, `(Action<X>)` casts that existed only for disambiguation against removed overloads). Drift discovered during audit v2.1 cleanup: README had 16 broken API sites that survived three releases (3.0.0 → 4.0.0 → 4.0.1) because no regression-guard pins README content. A future RG for code-block compilation against current surface was considered + rejected (AST infrastructure cost > value; see proposal's "Out of scope"); PR-time review is the enforcement.

- **README-update PR-time triggers.** Re-check README and refresh examples when ANY of the following lands:
  - Change to public surface of `ISignalCliClient` / `ISignal*` / `ISignalEventService` interfaces → re-verify API-example signatures compile.
  - Change to `AddSignalCli*` extensions (new overload, removed overload, signature change) → re-verify DI setup snippets.
  - New top-level pattern shipped (event-kind, derived exception type, options field, new optional package like `SignalCli.NET.HealthChecks`) → consider "API capabilities" table addition + Quick-start / Extended-example mention.
  - `<SignalCliPackageVersion>` bump → re-check README's version mentions, migration tables, "TestBaseline ≥ N" claims.

- **Badges + NuGet pack pairing.** Badges MUST use absolute `https://raw.githubusercontent.com/<owner>/<repo>/main/.github/badges/*.svg` URLs — relative paths (`.github/badges/...`) render correctly on github.com but break on nuget.org / IDE previewers / third-party gallery sites (interpreted as hostname → broken `http://.github/...`). README ships in NuGet pack via `<PackageReadmeFile>README.md</PackageReadmeFile>` + `<None Include="..\..\README.md" Pack="true" PackagePath="\" />` on each packable csproj (`SignalCli.csproj` + `SignalCli.HealthChecks.csproj` — see csproj/MSBuild conventions in "Established patterns").
```

## Risk analysis

**Risk 1: Drift from code.** Future refactor renames `SignalCliRegistrationMarker` or breaks "one-instance-two-roles" idiom; CLAUDE.md addition becomes stale.
- Mitigation: each addition cites the enforcement test (`JsonContextRegistrationTests`, `EventApiSymmetryTests`, `NF-003 addsignalcli-idempotency-fix` capability). The cited tests already exist and pin the rule. CLAUDE.md addition is descriptive, not the source-of-truth.

**Risk 2: Anthropic-guidance noise** — adding 22 more lines moves CLAUDE.md from 556 → ~578 lines, further past the 200-line target.
- Acknowledged trade-off. This change explicitly chooses content-completeness over size-discipline. The `claude-md-rules-restructure` change is the long-term fix for size; this change is orthogonal (content quality).

**Risk 3: Reviewer fatigue from "yet another CLAUDE.md edit"** — we've made many CLAUDE.md changes this audit cycle.
- Mitigation: smallest possible scope; one change, three precisely-defined additions; no other doc edits.

## Why one commit (not phased)

3 additions, all to the same file (CLAUDE.md), all additive (no deletions/moves), no test changes. Single coherent commit easier to review than a 3-way split. Mirrors the "csproj-build + [LoggerMessage] template + AOT-drift-fix" commit pattern from `a95985d` (which also bundled 3 CLAUDE.md additions).
