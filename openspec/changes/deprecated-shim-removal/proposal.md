# Deprecated shim removal (4.0 cleanup)

## Why

After `audit-followup-2026` lands and synchronizes `[Obsolete]` removal-version strings (M-1 fix), all surviving `[Obsolete]` members in `src/SignalCli/**` will read **"will be removed in 4.0"**. This change executes those removals.

The promise was made:
- `agent-friendly-modernization` (2.1.0) introduced the Async-suffix shims (`ListAccounts`, `SyncAccount`, `StartLink`, `FinishLink`, `ListGroups`) as `[Obsolete]` DIMs delegating to their `*Async` siblings — explicit one-major-version grace per `CLAUDE.md` "Backward compatibility convention".
- `post-modernize-tuning` (3.0.0) kept `ISignalCliClient.Version()`, `AddSignalCli(Action<Config>?)`, and `SignalCli.Models.Config` as `[Obsolete]` shims for the same one-major reason — they could not be removed in 3.0 because the Integration E2E tests depended on `Config.CreateDefault()`-auto-resolve of the bundled JRE.

This change cashes that promise. The blocker — Integration E2E test dependency on `Config.CreateDefault()` — is resolved by capability `config-auto-resolve-migration` first; the removals follow in dependency order.

**SemVer note.** The user used the working name *«умовний 3.1»* (notional 3.1) for this work. **Per SemVer this is a major release — 4.0** — because every removal below is a breaking change on the public API surface. Calling the release "3.1" would silently violate the SemVer contract the project commits to in `CHANGELOG.md` ("проєкт дотримується семантичного версіонування"). This change is structured, validated, and ready to ship; the maintainer makes the final call on the version label at archive time, but the implementation tasks and the migration table are written for a 4.0 release.

## What Changes

Five capabilities, sequenced by dependency:

1. **`config-auto-resolve-migration`** *(prerequisite — must land first)* — migrate the bundled-JRE / `JAVA_HOME` / `PATH` resolution logic out of `Config.CreateDefault()` / `Config.ResolveBundledJava` / `Config.ResolveOnPath` / `Config.TryResolveJavaPath` into a new internal `SignalCli/Utilities/JavaPathResolver.cs`. Add a public extension method `ServiceCollectionExtensions.AddSignalCliWithBundledRuntimeDefaults(Action<SignalCliOptions>? configure = null)` that wires the modern overload with auto-resolved defaults (`AppHome = AppContext.BaseDirectory`, `LibDirectory = "SignalCli/lib"`, `JavaExecutable = JavaPathResolver.TryResolve(...)`). The Integration E2E test (`SignalCliE2EVersionTests` + the 5 new tests from `audit-followup-2026` §5) migrates onto this helper, removing every `#pragma warning disable CS0618` site in `Tests/SignalCli.Tests.Integration/`.

2. **`remove-version-dim`** — delete `ISignalCliClient.Version()` default-interface-method. The single live consumer (`SignalCliE2EVersionTests`) already uses `VersionAsync` directly; no other usage exists.

3. **`remove-async-suffix-shims`** — delete the 5 obsolete shim methods from interfaces + their impls:
   - `ISignalAccounts.ListAccounts` / `.SyncAccount` + `SignalAccounts.cs` impls
   - `ISignalDevices.StartLink` / `.FinishLink` + `SignalDevices.cs` impls
   - `ISignalGroups.ListGroups` + `SignalGroups.cs` impl

4. **`remove-legacy-addsignalcli-overload`** — delete `ServiceCollectionExtensions.AddSignalCli(Action<Config>?)` overload, the `ConfigureOptions(services, o => { var legacy = Config.CreateDefault(); … })` helper, the `CopyFrom` field-copier, and the `SignalCliOptionsExtensions.ToOptions(this Config)` / `ToIOptions(this Config)` extension methods. Drop the `#pragma warning disable CS0618` blocks at every call site in `src/SignalCli/**`.

5. **`remove-config-type`** — delete `src/SignalCli/Models/Config.cs` and its `ToProcessConfig` / `BuildClasspath` methods. The `ToProcessConfig` logic moves to an internal extension method `SignalCliOptionsExtensions.ToProcessConfig(this SignalCliOptions)` (today this method internally delegates to `Config.ToProcessConfig` via the `ToConfig()` shim; the delegation is collapsed into a direct implementation). The `BuildClasspath` cache (`_cachedClasspath`) and the Windows vs POSIX path separator logic are preserved verbatim. Then drop the `ToConfig()` method on `SignalCliOptions`, the `[InternalsVisibleTo]` allowance for `Config` reflection, and the `#pragma CS0618` suppressions around the type.

After this change:
- `src/SignalCli/**` contains zero `[Obsolete]` attributes and zero `#pragma warning disable CS0618` lines.
- `ObsoleteMessageConsistencyTests` (from `audit-followup-2026`) trivially passes — no `[Obsolete]` to validate.
- `PublicApiSurfaceTests` baseline (from `audit-followup-2026`) shrinks by ~9 public members. The PR updates the baseline in the same commit.
- `JsonContextRegistrationTests` is unaffected (the removals touch no JSON-serializable DTO).

## Capabilities

### New Capabilities

- `config-auto-resolve-migration`: `JavaPathResolver` internal static class + `AddSignalCliWithBundledRuntimeDefaults` extension method preserve the bundled-JRE auto-resolve UX that Integration E2E tests depend on, without `Config`.
- `remove-version-dim`: `ISignalCliClient.Version()` SHALL NOT exist; callers use `VersionAsync()`.
- `remove-async-suffix-shims`: the five Async-suffix-less shim methods on `ISignalAccounts` / `ISignalDevices` / `ISignalGroups` SHALL NOT exist.
- `remove-legacy-addsignalcli-overload`: `ServiceCollectionExtensions.AddSignalCli(Action<Config>?)` SHALL NOT exist.
- `remove-config-type`: `SignalCli.Models.Config` SHALL NOT exist; `SignalCli.Models.SignalCliOptionsExtensions.ToOptions(Config)` and `.ToIOptions(Config)` SHALL NOT exist; `SignalCliOptions.ToConfig()` SHALL NOT exist.

### Modified Capabilities

- `agent-friendly-api` (archived in 2026-05-24-post-modernize-tuning): the Async-suffix-shim requirement was satisfied with `[Obsolete]` DIMs; with this change the shims are removed entirely — `Async`-suffix becomes the sole surface, not the preferred surface.
- `options-validation` (same archive): the `AddSignalCli(Action<SignalCliOptions>?)` and `AddSignalCli(IConfiguration)` overloads remain the only public registration paths. The legacy `Action<Config>?` overload is removed; the migration table in `CHANGELOG.md [4.0.0]` documents the rewrite recipe.

## Out of scope

- **Removing `JsonRpcException` legacy ctor.** Already removed in 3.0 (audit-followup `obsolete-doc-sync` reconciles the doc).
- **Removing the old generic-param order of `InvokeMethodAsync<TResponse, TRequest>`.** Already removed in 3.0; no shim possible (C# overload resolution can't disambiguate generic-arity reorders).
- **Migrating consumers automatically.** This is a major-version-break. The CHANGELOG entry SHALL include a migration table with sed-friendly patterns (e.g. `s/\.ListAccounts\b/.ListAccountsAsync/`), but no Roslyn analyzer / code-fix is produced as part of this change.
- **Removing `SignalCli.NET.HealthChecks` as a separate package.** The optional-package pattern stays — that's an architectural choice unrelated to deprecation removal.

## Dependencies

- **`audit-followup-2026` MUST land first.** Two reasons:
  1. `PublicApiSurfaceTests` baseline exists only after that change lands; this change updates that baseline as part of its deliverable.
  2. `ObsoleteMessageConsistencyTests` (also from `audit-followup-2026`) trivially passes after this change — but if both PRs land out of order, the baseline conflict is unnecessary churn.
- This change does NOT depend on any other in-flight work.

## Release strategy

- Branch: `claude/deprecated-shim-removal`.
- One commit per capability (5 commits), following CLAUDE.md "One commit per capability/cluster" rule. Order: `config-auto-resolve-migration` → `remove-version-dim` → `remove-async-suffix-shims` → `remove-legacy-addsignalcli-overload` → `remove-config-type`. Each commit MUST keep `dotnet test` green.
- Final commit: `<Version>4.0.0</Version>` bump, `CHANGELOG.md [4.0.0]` entry, `PublicApiSurfaceTests` baseline regeneration, `CLAUDE.md` "Backward compatibility convention" reconcile (move 4.0-targeted items to "Already removed in 4.0").
