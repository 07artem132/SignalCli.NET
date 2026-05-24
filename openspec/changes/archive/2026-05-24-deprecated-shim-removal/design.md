# Design — deprecated-shim-removal

## Method

Five capabilities, sequenced by hard dependency. Each lands as its own commit so a bisect can attribute any post-removal break to the exact removal.

```
config-auto-resolve-migration  ─┬─►  remove-version-dim
                                ├─►  remove-async-suffix-shims
                                ├─►  remove-legacy-addsignalcli-overload  ─►  remove-config-type
```

`config-auto-resolve-migration` is the unblocker — without it, `remove-config-type` breaks the Integration E2E tests; with it, the tests have a new home for the auto-resolve UX. The middle three are independent and could land in any order; the dependency arrows show what MUST precede each.

## 1. `config-auto-resolve-migration`

### Problem

The Integration E2E test [`SignalCliE2EVersionTests.cs:42-67`](../../../Tests/SignalCli.Tests.Integration/SignalCliE2EVersionTests.cs) calls `services.AddSignalCli((Config cfg) => …)` because the legacy overload internally runs `Config.CreateDefault()`, which auto-resolves the bundled JRE path on Win/macOS and sets `LibDirectory = "SignalCli/lib"`. The `Action<SignalCliOptions>?` overload skips both — so the test would fail with `OptionsValidationException` on `[Required(AllowEmptyStrings = false)]` for `LibDirectory` and on the cross-field XOR `JavaExecutable ⊕ SignalCliExecutable`.

CLAUDE.md Critical rule #16 codifies this: *"Do not modernize the Integration test off the legacy overload until either (a) Config-shim is fully removed in 4.0, or (b) auto-resolve logic is migrated into the SignalCliOptions-overload path."* — this capability is option (b).

### Approach

1. **Extract resolver utilities** into `src/SignalCli/Utilities/JavaPathResolver.cs`:
   ```csharp
   internal static class JavaPathResolver
   {
       // Moved verbatim from Config.ResolveBundledJava
       internal static string? TryResolveBundledJre(string baseDirectory) { … }

       // Moved verbatim from Config.ResolveOnPath
       internal static string? TryResolveOnPath(string executable) { … }

       // Moved + simplified from Config.TryResolveJavaPath / .ResolveJavaPath
       internal static string TryResolveJavaPath(string baseDirectory)
       {
           var bundled = TryResolveBundledJre(baseDirectory);
           if (bundled != null) return bundled;

           var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
           if (!string.IsNullOrEmpty(javaHome))
           {
               var fromHome = Path.Combine(javaHome, "bin", JavaExecutableName);
               if (File.Exists(fromHome)) return fromHome;
           }

           if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
           {
               var oracleJavaPath = Path.Combine(
                   Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                   "Common Files", "Oracle", "Java", "javapath", "java.exe");
               if (File.Exists(oracleJavaPath)) return oracleJavaPath;
           }

           return TryResolveOnPath(JavaExecutableName) ?? string.Empty;
       }

       private static string JavaExecutableName =>
           RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java";
   }
   ```

2. **New public extension** in `ServiceCollectionExtensions.cs`:
   ```csharp
   extension(IServiceCollection services)
   {
       /// <summary>
       /// Registers SignalCli with bundled-runtime defaults already applied:
       /// AppHome = AppContext.BaseDirectory; LibDirectory = "SignalCli/lib";
       /// JavaExecutable resolved from bundled JRE / JAVA_HOME / system PATH.
       /// </summary>
       /// <remarks>
       /// Designed for consumers using the SignalCli.Runtime.Jre.{win-x64,osx-arm64}
       /// bundled-runtime packages — they do not need to specify any path. Consumers
       /// without bundled runtime should use AddSignalCli(Action&lt;SignalCliOptions&gt;)
       /// directly.
       /// </remarks>
       public IServiceCollection AddSignalCliWithBundledRuntimeDefaults(
           Action<SignalCliOptions>? configure = null)
       {
           return services.AddSignalCli(opts =>
           {
               opts.AppHome = AppContext.BaseDirectory;
               opts.LibDirectory = "SignalCli/lib";
               opts.JavaExecutable = JavaPathResolver.TryResolveJavaPath(opts.AppHome);
               configure?.Invoke(opts);
           });
       }
   }
   ```

   Order: defaults first, consumer overrides second. Cross-field XOR `JavaExecutable ⊕ SignalCliExecutable` is consumer-driven — Linux native consumer sets `SignalCliExecutable` + nulls `JavaExecutable` in the configure delegate; everyone else gets a populated `JavaExecutable`.

3. **Integration test migration.** Every test in `Tests/SignalCli.Tests.Integration/` becomes:
   ```csharp
   services.AddSignalCliWithBundledRuntimeDefaults(cfg =>
   {
       if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
       {
           cfg.SignalCliExecutable = nativePath;
           cfg.JavaExecutable = null;
       }
       cfg.RequestTimeoutSeconds = 30;
       cfg.MaxRestartAttempts = 0;
       cfg.StoragePathCli = Path.Combine(Path.GetTempPath(), "SignalCliE2E-" + Guid.NewGuid());
   });
   ```
   Drop every `#pragma warning disable CS0618` in the Integration test project.

4. **Unit-test coverage:** new test `OptionsValidationTests.AddSignalCliWithBundledRuntimeDefaults_PopulatesPaths` asserts AppHome/LibDirectory/JavaExecutable populated; consumer override of `MaxRestartAttempts` takes precedence over default.

### Why not `IConfigureOptions<SignalCliOptions>`?

Considered: register an `IConfigureOptions<SignalCliOptions>` that runs before consumer overrides. Rejected because:
- The defaults are opt-in. Consumers who configure SignalCli for a non-bundled-runtime deployment (e.g., Docker with system Java) MUST NOT get auto-resolve magic that overwrites their explicit settings.
- An extension method named `…WithBundledRuntimeDefaults` makes the opt-in explicit at the call site. `IConfigureOptions<>` is global per-DI-container; surprising on shared infra.

## 2. `remove-version-dim`

### Problem & approach

`ISignalCliClient.Version()` is a default interface method that delegates to `VersionAsync()`. It has zero internal callers (every `Version` reference in `src/SignalCli/**` is `VersionAsync`). External consumers were given a one-major-version grace.

Delete:
```csharp
// REMOVE from Interfaces/SignalCli/ISignalCliClient.cs:49-57:
[Obsolete("Use VersionAsync; will be removed in 4.0")]
public Task<VersionResponse> Version(CancellationToken cancellationToken = default)
    => VersionAsync(cancellationToken);
```

The summary doc comment lines that reference `Version` go too. No other source changes needed.

Test impact: no existing test calls `Version()` (the integration test already uses `VersionAsync`). Add a `PublicApiSurfaceTests` baseline diff entry.

## 3. `remove-async-suffix-shims`

Delete the 5 obsolete DIM-shim members and their default bodies:

- [`ISignalAccounts.cs:39-41`](../../../src/SignalCli/Interfaces/Signal/ISignalAccounts.cs) — `Task<ListAccountsResponse> ListAccounts(...)`
- [`ISignalAccounts.cs:58-60`](../../../src/SignalCli/Interfaces/Signal/ISignalAccounts.cs) — `Task<SyncAccountsResponse> SyncAccount(...)`
- [`ISignalDevices.cs:30-…`](../../../src/SignalCli/Interfaces/Signal/ISignalDevices.cs) — `StartLink(…)`
- [`ISignalDevices.cs:71-…`](../../../src/SignalCli/Interfaces/Signal/ISignalDevices.cs) — `FinishLink(…)`
- [`ISignalGroups.cs:42-…`](../../../src/SignalCli/Interfaces/Signal/ISignalGroups.cs) — `ListGroups(…)`

Also remove the `<summary>Застаріле: використовуйте …Async.</summary>` doc-comment blocks immediately preceding each shim.

Implementations (`SignalAccounts.cs` / `SignalDevices.cs` / `SignalGroups.cs`) are unaffected — these are interface DIMs, no impl to delete.

Test impact: any test that calls e.g. `SignalAccounts.ListAccounts` (without `Async`) — search reveals **none** (all unit tests already use `Async` suffix in 3.0). Baseline diff.

## 4. `remove-legacy-addsignalcli-overload`

### Problem

The `AddSignalCli(Action<Config>?)` overload exists only to bridge consumers who haven't migrated to `SignalCliOptions`. After capability `config-auto-resolve-migration` ships, the Integration tests are off this overload. The only remaining users are external consumers.

### Approach

Delete from `src/SignalCli/Extensions/ServiceCollectionExtensions.cs`:

1. The `extension(IServiceCollection services).AddSignalCli(Action<Config>?)` member (lines 122-139).
2. The inline `ConfigureOptions(services, o => { var legacy = Config.CreateDefault(); … })` lambda is uniquely tied to that overload — gone with it.
3. The `CopyFrom(SignalCliOptions src, SignalCliOptions dst)` helper (lines 217-235) becomes unreferenced after the overload removal — delete.
4. Every `using System.Diagnostics.CodeAnalysis;` etc. directives that were imported solely for the `[Obsolete]` / `CS0618` suppressions on the removed code become unused — drop.

Delete from `src/SignalCli/Models/SignalCliOptionsExtensions.cs`:
- `ToOptions(this Config c)` extension method.
- `ToIOptions(this Config c)` extension method.
- The `#pragma warning disable CS0618` block wrapping the whole class.

Sites that referenced the removed extensions: only `ServiceCollectionExtensions.AddSignalCli(Action<Config>?)` (gone in step 1) and `SignalCliOptions.ToConfig()` (gone in capability 5). Verified by `grep -rn '\.ToOptions(\b' src/`.

### Test impact

`OptionsValidationTests` has no test that calls the legacy overload (all `AddSignalCli(o => …)` callsites use the modern signature). Confirmed by `grep -rn 'AddSignalCli((Action<Config>' Tests/`. No test changes needed besides the `PublicApiSurfaceTests` baseline update.

## 5. `remove-config-type`

### Problem

`SignalCli.Models.Config` is the parent of all the field-copier triplet duplication documented as the "Three-site duplication trap" in CLAUDE.md "Backward compatibility convention". Removing it collapses the triplet to a single `ToProcessConfig` extension.

### Approach

Two-step migration to avoid intermediate "broken state":

**Step 5a — relocate logic.** Move from `Config.ToProcessConfig()` (lines 172-236 of `Config.cs`) into `SignalCliOptionsExtensions.ToProcessConfig(this SignalCliOptions)`:

```csharp
internal static class SignalCliOptionsExtensions
{
    /// <summary>
    /// Builds a <see cref="ProcessConfig"/> for launching signal-cli from typed options.
    /// </summary>
    internal static ProcessConfig ToProcessConfig(this SignalCliOptions options)
    {
        // ... entire body of Config.ToProcessConfig, with field references
        // rewritten from this.<field> to options.<field>; classpath cache lives
        // on a thread-static or on the options instance itself.
    }
}
```

**Classpath cache.** `Config._cachedClasspath` is currently an instance field. After the move it can't be on `SignalCliOptions` (that's a public DTO; we shouldn't grow mutable cache state on it). Two options:
- (a) Drop the cache. `Directory.GetFiles(libPath, "*.jar")` runs once per process start (`ToProcessConfig` is called once per `StartProcessInternalAsyncNoLock`); cache savings near-zero in real workflows.
- (b) Keep the cache as a private static `ConcurrentDictionary<(string AppHome, string LibDirectory), string>` keyed on path inputs.

**Decision:** (a). The `Config.BuildClasspath` cache was added in `post-modernize-tuning §8c.10` to avoid repeating `Directory.GetFiles` on every Force-restart. After capability 5, `ToProcessConfig` runs once at startup; restart reuses the cached `ProcessConfig` reference held by `SignalCliHostedService`. Cache is solving a non-problem. Drop it.

**Step 5b — delete `Config`.**
- Delete `src/SignalCli/Models/Config.cs` entirely.
- Delete `SignalCliOptions.ToConfig()` method (lines 110-135 of `SignalCliOptions.cs`).
- Drop the `[Obsolete]` `using` directives + `#pragma warning disable CS0618` blocks throughout `src/SignalCli/**`.
- `SignalCliHostedService.cs:310` currently calls `_options.ToProcessConfig()` which routes through `Config.ToConfig().ToProcessConfig()` — the new extension makes this a direct call (same statement, different binding). No source change at that call site; the binding shifts at compile time.

### Test impact

- `Tests/SignalCli.Tests/ConfigTests.cs` (271 LOC, 10 tests) — half of the tests target `Config.ToProcessConfig` / `Config.CreateDefault` directly. Migration:
  - `ToProcessConfig` tests move to `SignalCliOptionsExtensionsTests.cs` (new file), rewriting the SUT from `new Config { … }.ToProcessConfig()` to `new SignalCliOptions { … }.ToProcessConfig()`. Logic is identical; assertions verbatim.
  - `CreateDefault` tests + the `ResolveBundledJava` / `ResolveOnPath` tests move to `JavaPathResolverTests.cs` (new file), exercising `JavaPathResolver.TryResolveJavaPath` / `.TryResolveBundledJre` / `.TryResolveOnPath`. Test count preserved.
- `Tests/SignalCli.Tests/ConfigResolveOnPathTests.cs` — folds into `JavaPathResolverTests.cs`.
- Existing tests that construct a `Config` instance to feed into `Config.ToOptions()` (e.g., `JsonRpcClientTests.CreateClient`, `BackPressureTests`, `TimeoutVirtualizationTests`) — rewrite to construct `SignalCliOptions` directly. ~15 callsites; mechanical change.
- `PublicApiSurfaceTests` baseline shrinks by ~30 entries (Config + its members).

### Why preserve `ConfigTests.cs` content instead of deleting?

Those tests pin protocol-level behavior that is the responsibility of `ToProcessConfig`, not of `Config` the class: native-vs-JVM mode selection, `--log-file=`/`--config=`/`--receive-mode=` arg shape, classpath separator choice. Deleting the tests would delete the regression-guards for the actual process-launch contract. The migration preserves every assertion; only the SUT type changes.

## Verification

After all five capabilities land:

```bash
dotnet build SignalCli.sln -p:TreatWarningsAsErrors=true
# Expected: 0 warnings. Every previously-suppressed CS0618 site is now gone with the deprecated code.

dotnet test SignalCli.sln
# Expected: 235 - (~5 ConfigTests reshuffled into JavaPathResolverTests / SignalCliOptionsExtensionsTests)
# net = 235 still. + 1 new test for AddSignalCliWithBundledRuntimeDefaults = 236.

grep -rn '\[Obsolete' src/SignalCli/
# Expected: 0 matches.

grep -rn '#pragma warning disable CS0618' src/ Tests/
# Expected: 0 matches.

grep -rn 'class Config\b' src/SignalCli/
# Expected: 0 matches.
```

`ObsoleteMessageConsistencyTests` (landed in `audit-followup-2026`) trivially passes — there's nothing left to validate.

`PublicApiSurfaceTests` baseline is regenerated; the diff is the deliberate breaking change set, reviewable in one PR.

## Migration table (for `CHANGELOG.md [4.0.0]`)

| Removed | Replacement | sed-friendly pattern |
|---|---|---|
| `ISignalCliClient.Version(ct)` | `ISignalCliClient.VersionAsync(ct)` | `s/\.Version(/\.VersionAsync(/g` |
| `ISignalAccounts.ListAccounts(ct)` | `.ListAccountsAsync(ct)` | `s/\.ListAccounts(/\.ListAccountsAsync(/g` |
| `ISignalAccounts.SyncAccount(ct)` | `.SyncAccountAsync(ct)` | `s/\.SyncAccount(/\.SyncAccountAsync(/g` |
| `ISignalDevices.StartLink(...)` | `.StartLinkAsync(...)` | `s/\.StartLink(/\.StartLinkAsync(/g` |
| `ISignalDevices.FinishLink(...)` | `.FinishLinkAsync(...)` | `s/\.FinishLink(/\.FinishLinkAsync(/g` |
| `ISignalGroups.ListGroups(...)` | `.ListGroupsAsync(...)` | `s/\.ListGroups(/\.ListGroupsAsync(/g` |
| `services.AddSignalCli(cfg => { /*Config*/ })` | `services.AddSignalCliWithBundledRuntimeDefaults(opts => { /*SignalCliOptions*/ })` *or* `services.AddSignalCli(opts => { … })` | — (manual; see CHANGELOG migration recipe) |
| `Config` class | `SignalCliOptions` | — (different shape; see CHANGELOG) |
| `Config.CreateDefault()` | `services.AddSignalCliWithBundledRuntimeDefaults(...)` | — |
| `Config.ResolveBundledJava(dir)` | `JavaPathResolver.TryResolveBundledJre(dir)` *(internal — for tests via InternalsVisibleTo)* | — |
| `Config.ResolveOnPath(name)` | `JavaPathResolver.TryResolveOnPath(name)` *(internal)* | — |
| `Config.WithEnvironment(map)` | `opts.EnvironmentVariables = map.ToDictionary(...)` in the configure delegate | — |
