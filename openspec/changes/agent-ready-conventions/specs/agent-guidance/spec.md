## ADDED Requirements

### Requirement: Repo-level agent instructions
The repository SHALL contain a `CLAUDE.md` instruction file at the root that documents the project, build/test commands, architecture, conventions, and the critical non-regression rules.

#### Scenario: Instructions cover critical rules
- **WHEN** an AI agent reads `CLAUDE.md`
- **THEN** it finds the audit-derived rules (no PII above Trace, `ArgumentList` for process args, attachment filename sanitization, composite event dispatch, the download-script SHA/BOM requirements)

#### Scenario: Build/test commands documented
- **WHEN** an agent needs to build or test
- **THEN** `CLAUDE.md` provides the exact `dotnet build`/`dotnet test` commands and notes the runtime-download behavior
