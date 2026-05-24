# Tasks — deprecated-shim-removal

## 0. Setup

- [ ] 0.1 Confirm `audit-followup-2026` is **archived** in `openspec/changes/archive/` (this change's `PublicApiSurfaceTests` baseline updates assume that test exists). If not yet — block.
- [ ] 0.2 Create branch `claude/deprecated-shim-removal` from current `main`.
- [ ] 0.3 `npx -y @fission-ai/openspec@latest validate deprecated-shim-removal --strict` — confirm green.

## 1. `config-auto-resolve-migration`

- [ ] 1.1 Create `src/SignalCli/Utilities/JavaPathResolver.cs` with `internal static class JavaPathResolver`. Move (verbatim — same string-matching logic) from `Config.cs`:
  - `Config.ResolveBundledJava(string baseDirectory)` → `JavaPathResolver.TryResolveBundledJre(string baseDirectory)` (internal, return `string?`).
  - `Config.ResolveOnPath(string executable)` → `JavaPathResolver.TryResolveOnPath(string executable)` (internal, return `string?`).
  - `Config.TryResolveJavaPath()` + `Config.ResolveJavaPath()` → `JavaPathResolver.TryResolveJavaPath(string baseDirectory)` (returns `string.Empty` when no Java found — preserves `Config.TryResolveJavaPath` swallow-and-empty contract).
  - `DefaultJavaPath` constant, `DefaultBundledJreDirectory` constant → moved as `private const` fields.
- [ ] 1.2 At the original sites in `Config.cs` — leave them in place for now (capability 5 deletes them). All `Config.cs` calls re-route to `JavaPathResolver.*` to keep behavior identical.
- [ ] 1.3 Add a new extension member to `ServiceCollectionExtensions.cs` inside the existing `extension(IServiceCollection services) { … }` block:
  - `public IServiceCollection AddSignalCliWithBundledRuntimeDefaults(Action<SignalCliOptions>? configure = null)` per `design.md` §1.
  - XMLDoc: target audience = consumers of `SignalCli.Runtime.Jre.{win-x64,osx-arm64}` packages.
- [ ] 1.4 Add `[InternalsVisibleTo("SignalCli.Tests")]` allowance for `JavaPathResolver` (the existing `InternalsVisibleTo` for `SignalCli.Tests` in `csproj` already covers it — verify no extra change needed).
- [ ] 1.5 Migrate `Tests/SignalCli.Tests.Integration/SignalCliE2EVersionTests.cs`:
  - Replace `services.AddSignalCli((Config cfg) => …)` with `services.AddSignalCliWithBundledRuntimeDefaults(cfg => …)`.
  - Drop `#pragma warning disable CS0618` / `#pragma warning restore CS0618` blocks.
  - Drop `using SignalCli.Models.Config = …` aliases if any.
- [ ] 1.6 Add new unit test: `Tests/SignalCli.Tests/OptionsValidationTests.cs` →
  ```
  [Fact] AddSignalCliWithBundledRuntimeDefaults_PopulatesAppHomeLibAndJava()
  [Fact] AddSignalCliWithBundledRuntimeDefaults_ConsumerOverridePrecedesDefault()
  ```
- [ ] 1.7 `dotnet build -p:TreatWarningsAsErrors=true && dotnet test SignalCli.sln` — clean.
- [ ] 1.8 **Commit** `feat: add JavaPathResolver + AddSignalCliWithBundledRuntimeDefaults helper`.

## 2. `remove-version-dim`

- [ ] 2.1 Delete from `src/SignalCli/Interfaces/SignalCli/ISignalCliClient.cs:49-57` — the `[Obsolete]` `Version` DIM and its preceding `<summary>` doc block.
- [ ] 2.2 Search `grep -rn '\.Version(' src/ Tests/` — confirm no remaining callsite (Integration test already migrated in §1.5; if any unit test still uses `Version`, rewrite to `VersionAsync`).
- [ ] 2.3 Update `PublicApiSurfaceTests` baseline by removing the `M:SignalCli.Interfaces.SignalCli.ISignalCliClient.Version(...)` line.
- [ ] 2.4 `dotnet build && dotnet test` — clean.
- [ ] 2.5 **Commit** `feat!: remove ISignalCliClient.Version() DIM shim (deprecated since 3.0)`.

## 3. `remove-async-suffix-shims`

- [ ] 3.1 Delete from `src/SignalCli/Interfaces/Signal/ISignalAccounts.cs:36-41` (the `<summary>Застаріле…</summary>` doc + `[Obsolete]` `ListAccounts` DIM).
- [ ] 3.2 Delete from `src/SignalCli/Interfaces/Signal/ISignalAccounts.cs:55-60` — the `SyncAccount` shim block.
- [ ] 3.3 Delete from `src/SignalCli/Interfaces/Signal/ISignalDevices.cs:27-…` — the `StartLink` shim block.
- [ ] 3.4 Delete from `src/SignalCli/Interfaces/Signal/ISignalDevices.cs:67-…` — the `FinishLink` shim block.
- [ ] 3.5 Delete from `src/SignalCli/Interfaces/Signal/ISignalGroups.cs:39-…` — the `ListGroups` shim block.
- [ ] 3.6 `grep -rn '\.ListAccounts(\|\.SyncAccount(\|\.StartLink(\|\.FinishLink(\|\.ListGroups(' src/ Tests/` — confirm no remaining non-`Async` callsites (regex deliberately bounds with `(` to exclude `…AsyncListAccounts` false positives).
- [ ] 3.7 Update `PublicApiSurfaceTests` baseline by removing the 5 corresponding `M:` lines.
- [ ] 3.8 `dotnet build && dotnet test` — clean.
- [ ] 3.9 **Commit** `feat!: remove 5 Async-suffix-less DIM shims (deprecated since 3.0)`.

## 4. `remove-legacy-addsignalcli-overload`

- [ ] 4.1 Delete from `src/SignalCli/Extensions/ServiceCollectionExtensions.cs`:
  - Lines 103-139: the `<summary>…legacy-overload</summary>` doc + `[Obsolete]` attribute + `AddSignalCli(Action<Config>?)` extension member body.
- [ ] 4.2 Delete `CopyFrom(SignalCliOptions src, SignalCliOptions dst)` helper (lines 217-235) — no more callers after §4.1.
- [ ] 4.3 Delete from `src/SignalCli/Models/SignalCliOptionsExtensions.cs`:
  - `ToOptions(this Config c)` extension method.
  - `ToIOptions(this Config c)` extension method.
  - The `#pragma warning disable CS0618` / `#pragma warning restore CS0618` block wrapping these.
- [ ] 4.4 Keep `SignalCliOptionsExtensions.ToProcessConfig(this SignalCliOptions options)` — it still routes through `Config` in this commit; capability 5 inlines.
- [ ] 4.5 `grep -rn 'AddSignalCli((Action<Config>\|AddSignalCli(Action<Config>' src/ Tests/` — confirm no remaining callers (Integration test migrated in §1.5).
- [ ] 4.6 Update `PublicApiSurfaceTests` baseline by removing:
  - `M:SignalCli.Extensions.ServiceCollectionExtensions.AddSignalCli(System.Action{SignalCli.Models.Config})`
  - Any other public-surface member that disappears as a side effect (none expected since `CopyFrom` is private).
- [ ] 4.7 `dotnet build && dotnet test` — clean.
- [ ] 4.8 **Commit** `feat!: remove AddSignalCli(Action<Config>?) legacy overload`.

## 5. `remove-config-type`

- [ ] 5.1 **Step 5a — move `ToProcessConfig` body.** Open `src/SignalCli/Models/SignalCliOptionsExtensions.cs`. Replace the one-line shim:
  ```csharp
  public static ProcessConfig ToProcessConfig(this SignalCliOptions options)
      => options.ToConfig().ToProcessConfig();
  ```
  with the full body translated from `Config.ToProcessConfig()` (current `Config.cs:172-236`) — field references rewritten from `this.X` to `options.X`. Drop the classpath cache (`_cachedClasspath`) per `design.md` §5 decision (a); inline `BuildClasspath()` into the method (Directory.GetFiles + separator + join).
- [ ] 5.2 Verify `dotnet test SignalCli.sln` — green at this midpoint. (ToProcessConfig now has its own impl; `Config.ToProcessConfig` still exists but is now dead code.)
- [ ] 5.3 **Step 5b — delete `Config`.**
  - Delete `src/SignalCli/Models/Config.cs` entirely.
  - Delete `SignalCliOptions.ToConfig()` method from `src/SignalCli/Models/SignalCliOptions.cs` (lines 110-135) + the `#pragma warning disable CS0618` block around it.
- [ ] 5.4 Update XML doc on `SignalCliOptions` (line 24 of `SignalCliOptions.cs`) — replace the `<para>Для backward compat існує застарілий <see cref="Config"/>…</para>` paragraph with a single sentence: *"This is the sole configuration surface — previously a legacy `Config` shim existed; removed in 4.0."*
- [ ] 5.5 Migrate tests:
  - `Tests/SignalCli.Tests/ConfigTests.cs` — rename to `SignalCliOptionsExtensionsTests.cs`; rewrite each test to construct `SignalCliOptions` instead of `Config`; assertions verbatim.
  - `Tests/SignalCli.Tests/ConfigResolveOnPathTests.cs` — rename to `JavaPathResolverTests.cs`; rewrite SUT from `Config.ResolveOnPath` to `JavaPathResolver.TryResolveOnPath`.
  - Rewrite ~15 other test callsites that construct a `Config` for fixture purposes (`JsonRpcClientTests.CreateClient`, `BackPressureTests`, `TimeoutVirtualizationTests`, `SignalEventServiceTests`, …) to construct `SignalCliOptions` directly. Use `grep -rn 'new Config' Tests/` to enumerate.
- [ ] 5.6 `grep -rn 'SignalCli\.Models\.Config\b' src/ Tests/` — confirm zero matches (the type doesn't exist anymore).
- [ ] 5.7 `grep -rn '#pragma warning disable CS0618' src/ Tests/` — confirm zero matches.
- [ ] 5.8 `grep -rn '\[Obsolete' src/SignalCli/` — confirm zero matches.
- [ ] 5.9 Update `PublicApiSurfaceTests` baseline — remove all `T:SignalCli.Models.Config`, `M:SignalCli.Models.Config.*`, `P:SignalCli.Models.Config.*`, `M:SignalCli.Models.SignalCliOptionsExtensions.ToOptions*`, `M:SignalCli.Models.SignalCliOptionsExtensions.ToIOptions*` lines.
- [ ] 5.10 `dotnet build -p:TreatWarningsAsErrors=true && dotnet test SignalCli.sln` — clean.
- [ ] 5.11 **Commit** `feat!: remove Config class — SignalCliOptions is the sole configuration surface`.

## 6. Final pass (single trailing commit)

- [ ] 6.1 Bump `<Version>4.0.0</Version>`, `<AssemblyVersion>4.0.0</AssemblyVersion>`, `<FileVersion>4.0.0</FileVersion>` in `src/SignalCli/SignalCli.csproj`.
- [ ] 6.2 Add `CHANGELOG.md [4.0.0]` entry:
  - Date.
  - `### ⚠️ Breaking` section listing every removal from capabilities 2-5.
  - Migration table from `design.md` "Migration table".
  - `### 🛠 Внутрішнє` section noting the cache-drop in `ToProcessConfig` and the relocation of resolver utilities into `JavaPathResolver`.
- [ ] 6.3 Update `CLAUDE.md`:
  - "Backward compatibility convention" — move every member from "Currently in flight (will be removed in 4.0)" to a new "Already removed in 4.0" bullet list. Make the "in flight" bullet empty or remove it.
  - "Three-site duplication trap" paragraph — delete entirely (no more triplet).
  - Critical rule #16 (Integration E2E tests use legacy `Action<Config>` overload) — delete or rewrite to reflect the new `AddSignalCliWithBundledRuntimeDefaults` reality.
- [ ] 6.4 Update README.md "Quick start" / "Configuration" sections — replace any `Config`-based snippet with `SignalCliOptions`. Add an example for `AddSignalCliWithBundledRuntimeDefaults`.
- [ ] 6.5 `Example/SignalCli.Example/Program.cs` — already on `SignalCliOptions` after `audit-followup-2026` §6b.1; no change needed.
- [ ] 6.6 Re-run `dotnet build -p:TreatWarningsAsErrors=true && dotnet test SignalCli.sln` — clean.
- [ ] 6.7 `npx -y @fission-ai/openspec@latest validate deprecated-shim-removal --strict` — final green.
- [ ] 6.8 **Commit** `chore: 4.0 release — version bump + CHANGELOG + CLAUDE.md reconcile`.

## 7. Post-merge

- [ ] 7.1 Wait for CI green on `main`.
- [ ] 7.2 `git pull --rebase origin main && git push origin main` (CLAUDE.md "Git" rule — coverage-bot may have committed `[skip ci]`).
- [ ] 7.3 `npx -y @fission-ai/openspec@latest archive deprecated-shim-removal --yes --skip-specs`.
- [ ] 7.4 Update CLAUDE.md "Implemented, merged, archived" with a new bullet: `deprecated-shim-removal (4.0.0)` + archive path pointer.
- [ ] 7.5 Tag `v4.0.0` on GitHub.
