## Why

After the modernization work (net10 + System.Text.Json, native/bundled-JRE runtime variants, C# 13/14 features) the library is functionally complete, but it has not had a *systematic, documentation-grounded* review since the original `address-audit-findings` pass. The owner requested a 100%-coverage audit that:

- checks every source file against **official Microsoft documentation** best practices (the audit MUST cite Microsoft Learn for each best-practice claim),
- surfaces latent **bugs / race conditions / resource leaks** before they reach production,
- assesses **test quality** and the **integration-test gap** (today there is no end-to-end test that launches a real `signal-cli`),
- evaluates **documentation quality** (XML docs, README, CLAUDE.md, language consistency).

The deliverable of this change is an **audit report**: a prioritized, evidence-backed list of findings (severity + `file:line` + Microsoft-docs citation + recommendation). Fixing the findings is intentionally out of scope here — each accepted finding becomes a task in a follow-up change so fixes stay reviewable and isolated.

## What Changes

- Produce a complete, severity-ranked findings report covering all `src/SignalCli/**` source (≈6 000 LOC) and the test project (≈3 700 LOC).
- Cross-check each subsystem against the relevant Microsoft Learn guidance (async/cancellation, `BackgroundService`/`IHostedService`, `System.Diagnostics.Process`, `System.Text.Json` source-gen, `IDisposable`/`IAsyncDisposable`, `System.Threading.Lock`, structured logging / `LoggerMessage`, nullable reference types).
- Assess unit-test value/coverage and define the missing integration-test strategy (real `signal-cli` over JSON-RPC, including the bundled-JRE consumer path that is currently only verified manually).
- Evaluate documentation quality and list concrete gaps.
- Record findings as follow-up tasks; **no production-code behavior changes** are made under this change.

## Capabilities

### New Capabilities
- `code-audit`: the methodology and required contents/quality bar of the comprehensive audit report (what "done" means for the audit itself).

### Modified Capabilities
<!-- None: this change adds the audit capability; remediation is deferred to follow-up changes. -->

## Impact

- Adds: `openspec/changes/comprehensive-code-audit/**` (this plan) and an `AUDIT-FINDINGS.md` report at the repo root.
- No changes to `src/**` runtime behavior under this change.
- Output feeds one or more follow-up remediation changes (e.g. `address-audit-findings-2`).
