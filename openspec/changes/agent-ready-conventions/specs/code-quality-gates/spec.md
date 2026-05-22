## ADDED Requirements

### Requirement: Cross-tool style configuration
The repository SHALL provide a root `.editorconfig` defining C# style and naming rules (private `_camelCase`, static `s_`, `I`-prefixed interfaces, PascalCase public members, file-scoped namespaces, `var` usage), usable by any IDE and by the build.

#### Scenario: EditorConfig present and applied
- **WHEN** the solution is opened or built
- **THEN** the `.editorconfig` rules are the authoritative style source (superseding tool-specific settings)

### Requirement: Build-time code analysis
Projects SHALL enable analyzers (`AnalysisLevel = latest-recommended`) and `EnforceCodeStyleInBuild`, configured in one shared location.

#### Scenario: Analyzers run on build
- **WHEN** `dotnet build` runs
- **THEN** code-style and analyzer diagnostics are produced for the projects

#### Scenario: Nullable stays enabled
- **WHEN** the projects build
- **THEN** nullable reference type analysis remains enabled
