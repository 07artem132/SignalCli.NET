## ADDED Requirements

### Requirement: MSBuild paths use forward slashes
Every MSBuild item glob, `DestinationFiles` template, and `Exists()` condition in `src/SignalCli.runtime*/**/*.targets` and `src/SignalCli.runtime*/**/*.csproj` SHALL use forward slashes (`/`) regardless of the OS. MSBuild normalizes `/` on every platform; mixing `\` with `/` breaks Linux- and macOS-only build targets.

#### Scenario: Linux native-runtime build delivers the binary to consumer's TargetDir
- **GIVEN** a Linux consumer of `SignalCli.Runtime.Native` runs `dotnet build`
- **WHEN** `SignalCli.Native.targets` runs
- **THEN** every file under `signal-cli-native/**/*` is enumerated by the `Include` glob (forward slashes)
- **AND** every file is copied to `$(TargetDir)signal-cli-native/...`
- **AND** the `chmod +x` exec succeeds because the binary actually exists at the path it expects

#### Scenario: Idempotency gate works on Linux
- **GIVEN** the runtime project has already downloaded signal-cli
- **WHEN** `dotnet build` runs again
- **THEN** the `Exists()` gate in `SignalCli.runtime.csproj` correctly observes the marker (forward slash path) as present
- **AND** the download is skipped

### Requirement: Exists() gate uses a stable file marker, not a directory
The incremental-build gate in `src/SignalCli.runtime*/**/*.csproj` SHALL key on a file that the download script produces last (e.g. `bin/signal-cli` or `lib/signal-cli-0.14.3.jar`), not on a top-level directory. A directory can survive a partial download.

#### Scenario: Partial download is retried
- **GIVEN** a download was interrupted leaving `bin/` empty
- **WHEN** the next build runs
- **THEN** `Exists()` returns false (the marker file is absent) and the download repeats
- **AND** subsequent builds with a complete payload skip the download

### Requirement: Pinned versions and SHA-256 hashes live in the csproj
The pinned version and SHA-256 for `signal-cli` and the Temurin JRE SHALL be MSBuild properties in the csproj (`<SignalCliVersion>`, `<SignalCliSha256>`, `<JreVersion>`, `<JreSha256>`). The download scripts SHALL accept these as parameters; the script SHALL verify the downloaded archive's SHA against the passed value before extraction. Hard-coding hashes inside `*.sh`/`*.ps1` SHALL be removed.

#### Scenario: Bumping signal-cli is a one-place edit
- **WHEN** a contributor updates `<SignalCliVersion>` and `<SignalCliSha256>` in `src/SignalCli.runtime/SignalCli.runtime.csproj`
- **THEN** every consumer of those values (`SignalCli.runtime`, `SignalCli.runtime.jre.win-x64`, `SignalCli.runtime.jre.osx-arm64`) picks them up automatically
- **AND** no other file needs editing

#### Scenario: Script rejects a tampered archive
- **GIVEN** a downloaded archive whose SHA-256 does not match the `<SignalCliSha256>` argument
- **WHEN** the download script runs
- **THEN** it exits non-zero before extraction
- **AND** the build fails with a clear error message identifying the expected vs actual hash

### Requirement: GitHub Actions are pinned to commit SHAs
Every `uses:` reference to a third-party (non-`actions/*`) GitHub Action in `.github/workflows/**` SHALL be pinned to a 40-character commit SHA rather than a moving tag (`@v1`, `@v2`, `@main`). First-party `actions/*` SHOULD also be pinned for full reproducibility (Microsoft *Security hardening for GitHub Actions — Using third-party actions*).

#### Scenario: Tag movement does not change CI behavior
- **GIVEN** a community action whose `v2` tag is later moved to a different commit (compromise or innocent re-tag)
- **WHEN** the workflow runs
- **THEN** the workflow uses the historically-pinned SHA, not the new commit
- **AND** the CI build outcome is reproducible

### Requirement: Post-extraction integrity verification for JRE bundles
After `<Unzip>` extracts a bundled JRE in `SignalCli.Jre.targets`, the extracted `bin/java[.exe]` SHALL be checked for existence. If it is missing the build SHALL fail with a clear message that names the expected path and suggests deleting the cached `obj/jre` to re-extract. This is defense in depth on top of the download-time SHA pin.

#### Scenario: A corrupted cache is detected
- **GIVEN** `obj/jre/bin/java` is missing (interrupted previous extract)
- **WHEN** the build runs
- **THEN** the targets fail with an actionable message
- **AND** the message names `obj/jre/bin/java` and explains how to recover

### Requirement: PowerShell hash comparison is case-invariant
SHA-256 comparisons in `*.ps1` download scripts SHALL be case-insensitive. Both sides of the comparison SHALL be lowered through `.ToLowerInvariant()` (or compared via `[StringComparison]::OrdinalIgnoreCase`) so that an uppercase hash in the csproj does not cause a false negative against a lowercase `Get-FileHash` result.

#### Scenario: Uppercase hash in csproj
- **GIVEN** `<SignalCliSha256>` is uppercase
- **AND** `Get-FileHash` returns the same hash in lowercase
- **WHEN** the script compares them
- **THEN** the comparison succeeds (case-insensitive)

### Requirement: Runtime packages ship a LICENSE file
Each runtime package (`SignalCli.runtime`, `SignalCli.runtime.native`, `SignalCli.runtime.jre.*`) SHALL include a `LICENSE.txt` file packed at the package root via `<None Include="LICENSE.txt" Pack="true" PackagePath="" />`. `<PackageLicenseExpression>` (already present) remains the canonical SPDX identifier; the bundled file gives consumers a direct on-disk reference (NuGet 5.10+ recommendation).

#### Scenario: Consumer inspects the package
- **WHEN** a consumer unpacks the .nupkg
- **THEN** they find `LICENSE.txt` at the root
- **AND** the SPDX expression in nuspec matches the file contents

### Requirement: Adoptium URL has a fallback and a clear failure message
`src/build/download-jre.{sh,ps1}` SHALL emit a clear, actionable error message (naming the assumed URL pattern and how to override it) when the Adoptium release URL returns 404. A fallback URL (an organizational mirror or an `apt`/`brew`-style hint) SHOULD be documented in a comment in the script.

#### Scenario: Adoptium changes the URL pattern
- **GIVEN** Adoptium changes its release URL schema
- **WHEN** the script runs
- **THEN** the failure message identifies the URL it tried, the expected pattern, and the env var (or csproj property) to override it
- **AND** the message guides the contributor to update both `download-jre.sh` and `download-jre.ps1`
