## ADDED Requirements

### Requirement: Coverage badges in README SHALL use absolute raw.githubusercontent.com URLs

The coverage badges in `README.md` (`Lines`, `Methods`, `Branches`) SHALL be declared with absolute `https://raw.githubusercontent.com/07artem132/SignalCli.NET/main/.github/badges/<name>.svg` URLs. Relative paths (`.github/badges/<name>.svg`) SHALL NOT be used.

Rationale: GitHub.com's own markdown renderer transparently resolves relative paths to raw URLs, but every other markdown renderer (NuGet.org's package README, IDE markdown previewers, third-party gallery sites) interprets a path that starts with `.github/...` as a URL whose host is `.github` — producing the broken `http://.github/badges/<name>.svg` form. Absolute raw URLs work identically on github.com and everywhere else.

#### Scenario: README badges render on github.com
- **GIVEN** README.md uses absolute raw.githubusercontent.com URLs
- **WHEN** the consumer views the repo on github.com
- **THEN** all three coverage badges render correctly as images

#### Scenario: README badges render outside github.com
- **GIVEN** README.md uses absolute raw.githubusercontent.com URLs
- **WHEN** the README is rendered by NuGet.org, an IDE previewer, or a third-party markdown service
- **THEN** the badge URLs resolve and render correctly (no `http://.github/...` malformed URLs appear in the rendered output)

### Requirement: CI workflow SHALL emit absolute badge URLs

`.github/workflows/dotnet-desktop.yml` SHALL produce absolute raw.githubusercontent.com URLs at every site where it writes coverage-badge markdown — currently 4 sites:

- Insert into existing README (line ~166 — `sed -i ... ![Lines](...)`)
- Write to fresh README.md.new (line ~172 — `echo "![Lines](...)" >> README.md.new`)
- Create new README.md from scratch (line ~181 — `echo "![Lines](...)" >> README.md`)
- Coverage summary for PR comments (line ~203 — `echo "![Lines](...)" >> coverage-summary.md`)

The auto-commit hook (`stefanzweifel/git-auto-commit-action` step) re-runs the README-mutation logic after every successful Debug+ubuntu-latest run and commits with `[skip ci]`. If only README is fixed without updating the workflow, the next CI run reverts the fix. Both SHALL change in the same commit.

#### Scenario: Workflow-generated README badges are absolute URLs
- **GIVEN** the workflow runs on a `main`-push event
- **WHEN** the workflow's "Update README with coverage badges" step inserts/replaces the badge markdown
- **THEN** the resulting README.md badge lines contain `https://raw.githubusercontent.com/07artem132/SignalCli.NET/main/.github/badges/`
- **AND** do NOT contain bare relative paths `.github/badges/`

#### Scenario: PR-comment coverage summary uses absolute URLs
- **GIVEN** the workflow runs on a `pull_request` event
- **WHEN** "Create Coverage Summary" step writes `coverage-summary.md`
- **THEN** the badge lines use absolute raw.githubusercontent.com URLs

### Requirement: NuGet package SHALL include the README

`src/SignalCli/SignalCli.csproj` SHALL declare `<PackageReadmeFile>README.md</PackageReadmeFile>` and pack the repo-root README into the NuGet package via `<None Include="..\..\README.md" Pack="true" PackagePath="\" />` (or equivalent). The current build emits a warning *"The package SignalCli.NET.3.0.0 is missing a readme"* — this warning SHALL disappear after the fix.

The badge-URL absoluteness from the previous requirements is a prerequisite: a packaged README that uses relative paths would render badges as broken images on nuget.org. Both fixes ship together.

#### Scenario: NuGet package contains README with working badges
- **GIVEN** the SignalCli.NET NuGet package built after this fix
- **WHEN** a consumer inspects the package on nuget.org
- **THEN** the package's README tab renders all three coverage badges as images
- **AND** no broken-image placeholders appear

#### Scenario: Build no longer warns about missing readme
- **GIVEN** `dotnet build src/SignalCli/SignalCli.csproj -p:TreatWarningsAsErrors=true`
- **WHEN** the build runs
- **THEN** no warning matching `"The package .* is missing a readme"` appears
- **AND** the build succeeds with 0 warnings
