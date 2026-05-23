## ADDED Requirements

### Requirement: Complete source coverage
The audit SHALL review 100% of the production source under `src/SignalCli/**` and the test project under `Tests/**`. Every `.cs` file MUST be accounted for as either "reviewed, no finding" or associated with one or more findings.

#### Scenario: Every source file accounted for
- **WHEN** the audit report is produced
- **THEN** it lists, or otherwise confirms coverage of, each non-generated `.cs` file in `src/SignalCli`
- **AND** no production file is silently skipped

### Requirement: Evidence-backed, documentation-grounded findings
Each finding SHALL include a severity (Critical/High/Medium/Low), a precise `file:line` location, a short statement of the problem, the reason it is a problem with a citation to official Microsoft documentation (Microsoft Learn URL), and a concrete recommendation. High and Critical findings MUST be verified against the cited source lines before inclusion.

#### Scenario: A best-practice finding is recorded
- **WHEN** a deviation from a Microsoft-documented best practice is identified
- **THEN** the finding cites the specific Microsoft Learn page that defines the practice
- **AND** it gives the exact location and a recommended remediation

#### Scenario: A high-severity finding is verified
- **WHEN** a finding is rated High or Critical
- **THEN** the cited source lines have been read and the issue confirmed (not inferred from a name or excerpt)

### Requirement: Test quality and integration-test gap assessment
The audit SHALL assess whether existing tests provide real value (not tautological), report coverage, and define the missing integration-test strategy for an end-to-end `signal-cli` JSON-RPC round-trip.

#### Scenario: Integration-test gap is documented
- **WHEN** the audit evaluates the test suite
- **THEN** it states whether any test exercises a real `signal-cli` process end-to-end
- **AND** it proposes a concrete integration-test approach (including how CI runs it without a system Java install)

### Requirement: Documentation quality assessment
The audit SHALL evaluate documentation quality: XML-doc completeness on the public API surface, and the accuracy of `README.md`, `CLAUDE.md`, and `.github/copilot-instructions.md` against the current code.

#### Scenario: Documentation drift is reported
- **WHEN** a documented statement contradicts the current code
- **THEN** the audit records the drift with the document location and the correct value

### Requirement: No production behavior change
This audit change SHALL NOT modify production code behavior; remediation is deferred to follow-up changes so fixes remain isolated and reviewable.

#### Scenario: Audit leaves runtime code unchanged
- **WHEN** the audit change is completed
- **THEN** the only added artifacts are the audit plan and the findings report
- **AND** `src/SignalCli/**` runtime behavior is unchanged by this change
