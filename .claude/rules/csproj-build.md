---
paths:
  - "**/*.csproj"
  - "Directory.Build.props"
  - ".github/workflows/**"
  - "src/build/**"
  - "src/SignalCli.runtime*/**"
---

# csproj / MSBuild + build/CI conventions

## csproj / MSBuild conventions

- **`Directory.Build.props` is shared canon** — `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`, `<SignalCliPackageVersion>` (main lib + HealthChecks lockstep, per root CLAUDE.md § Version-CHANGELOG lockstep). Per-csproj `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` overrides the prop's `false` default (main lib + test csproj opt in; ad-hoc/runtime projects stay opt-out).
- **`<IsAotCompatible>true</IsAotCompatible>` only on `src/SignalCli/SignalCli.csproj`.** Adapter (`SignalCli.HealthChecks`) and runtime-packages don't ship AOT — they're host-side or build-time. Adding to other csproj would force the IL2026/IL3050 audit on dependencies that don't need it.
- **`<PrivateAssets>all</PrivateAssets>` for build-time-only `PackageReference`s.** Canonical: `JetBrains.Annotations` (PublicAPI / NotNull hints — never leak into consumer dependency graph). If a package only feeds analyzers / source-gens / XMLDoc hints, mark it `PrivateAssets=all`. Failure mode: consumer's `<PackageReference>` resolution pulls our build-time-only dep, bloats their lock file.
- **`<PackageReadmeFile>README.md</PackageReadmeFile>` + `<None Include="..\..\README.md" Pack="true" PackagePath="\" />` is paired** — both required, else NuGet warns "missing readme" and the README doesn't ship. Applied to both packable projects (`SignalCli.csproj` + `SignalCli.HealthChecks.csproj`). Pattern: repo-root README packed as-is; badges use absolute `raw.githubusercontent.com` URLs (per badge-url-fix capability) so they render outside github.com.
- **`<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>` only on main lib.** Per [MS Learn — Configuration source generator](https://learn.microsoft.com/dotnet/core/extensions/configuration-generator), this intercepts `OptionsBuilder.Bind` / `Configure<T>(IConfiguration)` call-sites at compile time and emits reflection-free binder code. Required for AOT-safe `AddSignalCli(IConfiguration)` overload. Don't enable on test/runtime/adapter csprojs — they don't bind configurations and the flag adds compile-time overhead for no benefit.
- **Version goes through `$(SignalCliPackageVersion)`, never hardcoded.** `<Version>$(SignalCliPackageVersion)</Version>` + `<AssemblyVersion>$(SignalCliPackageVersion)</AssemblyVersion>` + `<FileVersion>$(SignalCliPackageVersion)</FileVersion>` in both packable csprojs. Hardcoded `<Version>X.Y.Z</Version>` is caught by `VersionLockstepTests` (RG07) at build time. See `Directory.Build.props:18`.
- **`<GeneratePackageOnBuild>true</GeneratePackageOnBuild>` on packable projects only.** Triggers `dotnet pack` on every `dotnet build` — useful for local-feed testing without explicit pack step. Skip on Tests/Example/runtime-download projects (`IsPackable=false`).

## Mass-edit safety + supply-chain

- **PowerShell file I/O preserves encoding ONLY via `[System.IO.File]`.** `Get-Content -Raw` + `Set-Content -Encoding UTF8` mangles Cyrillic by reading via system codepage (often Windows-1251) and writing UTF-8-BOM. For batch-edits across `.cs` files use:
  ```powershell
  $text  = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
  $bytes = [System.IO.File]::ReadAllBytes($path)
  $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
  # ... mutate $text ...
  $enc = if ($hasBom) { New-Object System.Text.UTF8Encoding($true) } else { New-Object System.Text.UTF8Encoding($false) }
  [System.IO.File]::WriteAllText($path, $text, $enc)
  ```
  Mojibake'd cyrillic mid-batch is fixed by `git checkout` of the affected files + redo with the safe pattern. Symptom: `прибрано — клас` → `РїСЂРёР±СЂР°РЅРѕ вЂ" РєР»Р°СЃ`.
- **GitHub Actions `actions/*` SHAs MUST come from existing workflows in this repo**, not from notes or docs. `grep -rn "actions/" .github/workflows/*.yml | grep -v <new-file>` and copy. Typo-pinning produces fast-fail "Unable to resolve action" — round 16 lost one PR-cycle to a 1-char typo in `actions/checkout` SHA.
- **PowerShell `Get-FileHash` is fragile on `windows-latest` GitHub-runner** (rare `Microsoft.PowerShell.Utility` auto-load race). All download scripts (`src/build/download-jre.ps1`, `src/SignalCli.runtime/download-signal-cli.ps1`) compute SHA-256 directly via `System.Security.Cryptography.SHA256.Create().ComputeHash(stream)` + `BitConverter.ToString().Replace("-","")` — cross-version-safe across WinPS 5.1 and PS 7.x, no module-loading dependency. Don't revert to `Get-FileHash`.
