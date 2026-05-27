## ADDED Requirements

### Requirement: `IdentityChangedException` SHALL be marked `[Obsolete]` and scheduled for removal in 5.0

`src/SignalCli/Exceptions/IdentityChangedException.cs` SHALL be annotated with `[Obsolete("...; will be removed in 5.0.", DiagnosticId = "SIGNALCLI001")]`. The XMLDoc `<remarks>` SHALL be rewritten to honestly state that the type is never dispatched, citing the new pinned fact #8 in `.claude/rules/signal-cli-protocol.md` (see capability `protocol-checklist-amend`).

The `[Obsolete]` message SHALL explain:
1. The type is never dispatched (no `throw new IdentityChangedException(...)` in production code).
2. Upstream signal-cli has no protocol-level distinction between first-contact-unknown identity and re-installed identity — both surface as `UntrustedKeyErrorException("Failed to send message due to untrusted identities")` mapping to code -4.
3. Consumers wanting the distinction MUST query `ISignalContacts.ListIdentitiesAsync` and cross-reference against their own cached trust-store state.
4. The type stays binary-compatible during 4.x and is removed in 5.0 per `.claude/rules/obsolete-shims.md` one-major-grace convention.

The R04 regression-guard test `ObsoleteMessageConsistencyTests` SHALL automatically verify the `"5.0"` reference is strictly greater than current package major (`4`).

#### Scenario: Consumer compiles code catching `IdentityChangedException` after 4.10.0 lands

- **GIVEN** consumer source code with `catch (IdentityChangedException ex) { /* handle */ }`
- **WHEN** consumer rebuilds against `SignalCli.NET 4.10.0`
- **THEN** the compiler emits warning `CS0618` referencing `SIGNALCLI001` with the deprecation message
- **AND** the catch block continues to compile and (vacuously) execute — the exception type still exists and still derives from `UntrustedIdentityException`

#### Scenario: `ObsoleteMessageConsistencyTests` validates removal-target version

- **GIVEN** `IdentityChangedException` carries `[Obsolete("...; will be removed in 5.0.", ...)]`
- **AND** current `<SignalCliPackageVersion>` is `4.10.0`
- **WHEN** the R04 regression-guard test runs
- **THEN** the test passes because parsed-target-major `5` is strictly greater than current-major `4`

### Requirement: `<exception cref="IdentityChangedException">` SHALL be removed from public XMLDoc

`src/SignalCli/Interfaces/Signal/ISignalMessage.cs` SHALL be edited to remove `<exception cref="IdentityChangedException">` tags from `SendReactionAsync` (and any other ISignalMessage methods that reference the deprecated type). `<exception cref="UntrustedIdentityException">` SHALL remain in place — that one is honest, the dispatch arm exists.

`src/SignalCli/Exceptions/UntrustedIdentityException.cs` SHALL be edited to remove the misleading `<remarks>` text claiming "derivation IdentityChangedException для опт-ін різнення re-install уже знаних"; replaced with a simpler note about un-sealing for consumer subtyping.

#### Scenario: Generated `SignalCli.xml` documentation no longer advertises `IdentityChangedException` as throwable

- **WHEN** the build emits the XMLDoc artifact `src/SignalCli/SignalCli.xml`
- **THEN** no `<exception cref="T:SignalCli.Exceptions.IdentityChangedException"/>` element appears
- **AND** the existing `<exception cref="T:SignalCli.Exceptions.UntrustedIdentityException"/>` element on `SendReactionAsync` remains

### Requirement: Existing type-hierarchy tests SHALL continue to pass under `#pragma warning disable CS0618`

`Tests/SignalCli.Tests/Exceptions/NewTypedRpcErrorsTests.cs` SHALL retain its `IdentityChangedException_IsSubtypeOfUntrustedIdentity` and sealed-check assertions during the 4.x deprecation period (until 5.0 removes the type). The two test sites that construct or reference `IdentityChangedException` SHALL be wrapped in `#pragma warning disable CS0618` (with `restore`) and justified by a comment: `// CS0618: deprecated type — hierarchy guard retained during 4.x grace period; removed alongside type in 5.0`.

#### Scenario: Test build succeeds under `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`

- **GIVEN** `Tests/SignalCli.Tests.csproj` has `TreatWarningsAsErrors=true`
- **AND** test source contains `new IdentityChangedException(...)` wrapped in `#pragma warning disable CS0618` / `restore`
- **WHEN** `dotnet build Tests/SignalCli.Tests.csproj` runs
- **THEN** no `CS0618` error is raised and the build succeeds

#### Scenario: `ObsoleteMessageConsistencyTests` continues to enforce removal target after future bumps

- **GIVEN** `<SignalCliPackageVersion>` is bumped from `4.10.0` to `4.11.0` in a future release
- **WHEN** R04 runs
- **THEN** test still passes (target `5` > current `4`)
- **AND** the day `<SignalCliPackageVersion>` becomes `5.0.0`, R04 fails — explicit signal to delete the type per scheduled removal in capability `deprecated-shim-removal-5.0` (separate future change)
