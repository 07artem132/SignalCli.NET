## ADDED Requirements

### Requirement: `[Obsolete]` removal version SHALL be greater than current major

Every `[ObsoleteAttribute]` whose `Message` includes the phrase "will be removed in N.0" SHALL have `N` strictly greater than the major component of the current `Assembly.GetName().Version`. The same rule SHALL apply to inline comments and XML documentation that announce a removal version on a deprecated member.

#### Scenario: `Config` deprecation announces removal in 4.0
- **GIVEN** the package version is `3.0.0`
- **WHEN** a consumer reads the `[Obsolete]` attribute on `SignalCli.Models.Config`
- **THEN** the attribute message reads "will be removed in 4.0" (or higher)
- **AND** the same wording is used in the type's XML documentation `<remarks>`

#### Scenario: `ISignalCliClient.Version()` deprecation announces removal in 4.0
- **GIVEN** the package version is `3.0.0`
- **WHEN** a consumer reads the `[Obsolete]` attribute on `ISignalCliClient.Version`
- **THEN** the attribute message reads "Use VersionAsync; will be removed in 4.0"

#### Scenario: `AddSignalCli(Action<Config>?)` overload announces removal in 4.0
- **GIVEN** the package version is `3.0.0`
- **WHEN** a consumer reads the `[Obsolete]` attribute on `ServiceCollectionExtensions.AddSignalCli(Action<Config>?)`
- **THEN** the attribute message reads "Use AddSignalCli(Action<SignalCliOptions>?) ...; will be removed in 4.0"

### Requirement: CLAUDE.md SHALL list each deprecated member exactly once and in the correct lifecycle bucket

The `CLAUDE.md` "Backward compatibility convention" section SHALL list every `[Obsolete]` member exactly once across the two buckets ("Already removed in 3.0" and "Currently in flight (will be removed in 4.0)"). A member SHALL NOT appear in both buckets. A member listed under "Already removed" SHALL NOT exist in the live source.

#### Scenario: `Version()` is listed in "in flight" not "already removed"
- **WHEN** the maintainer reads `CLAUDE.md` "Backward compatibility convention" section
- **THEN** `ISignalCliClient.Version()` is listed under "Currently in flight (will be removed in 4.0)"
- **AND** is NOT listed under "Already removed in 3.0"

#### Scenario: `AddSignalCli(Action<Config>?)` is listed in "in flight" not "already removed"
- **WHEN** the maintainer reads `CLAUDE.md` "Backward compatibility convention" section
- **THEN** `AddSignalCli(Action<Config>?)` is listed under "Currently in flight (will be removed in 4.0)"
- **AND** is NOT listed under "Already removed in 3.0"
