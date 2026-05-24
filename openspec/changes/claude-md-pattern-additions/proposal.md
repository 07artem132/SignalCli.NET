# Document three under-documented patterns in CLAUDE.md

## Why

Audit v2.1 (CLAUDE.md coverage agent + manual cross-check) found three patterns that the codebase enforces consistently but CLAUDE.md does NOT explicitly document. Each is "visible from the code" but invisible to a new contributor (or AI agent) reading CLAUDE.md as the entry point. Re-checking the agent's initial flag (which I dismissed too quickly as "false positives"), 2 of 3 are real partial gaps:

1. **DI registration patterns** — `TryAddSingleton<T>` vs `AddSingleton<T>` choice, "one-instance-two-roles" idiom (`AddHostedService(sp => sp.GetRequiredService<X>())`), idempotency-guard-via-sentinel-type. Used everywhere in `ServiceCollectionExtensions.RegisterCoreServices` but CLAUDE.md only mentions "DI registration is idempotent" — doesn't explain HOW or WHEN to use which API.

2. **Namespace hierarchy / DTO naming / test-class naming** — three `Services.*` namespaces by domain (Rpc / SignalCli / Signal), `*Parameters` / `*Response` for RPC DTOs, `*EventArgs` records, `*Tests` test classes. All consistent in code, none documented as convention in CLAUDE.md.

3. **Exception derivation guidance** — "when to derive `XxxException : JsonRpcException` vs throw base". `RateLimitException` (-5) and `UntrustedIdentityException` (-4) exist; other codes (-1, -3, -6) stay base. The heuristic (high-leverage / consumer-actionable codes → derive; rare codes → base + inspect `Code`) is implicit in CHANGELOG history but absent from CLAUDE.md.

These are NOT critical — code-reviewers catch divergence — but the cost-of-add is tiny (~15-25 lines total across 3 sections) and the benefit is real: future contributors (human or AI) doing DI extensions / new RPC methods / new exception types currently have to infer the pattern from existing code; explicit doc makes it one Cmd-F search.

Filed as its OWN OpenSpec change rather than scope-creep into `claude-md-rules-restructure` per Anthropic's "don't bundle structural with content edits" guidance — restructure stays content-preserving, this change stays purely additive.

## What Changes

Single capability, three small text-additions to CLAUDE.md:

- **`claude-md-pattern-additions`**:
  - **New subsection in "Established patterns" → "DI registration"** (3 bullets covering `TryAdd*` vs `Add*` choice, one-instance-two-roles idiom with `SignalCliHostedService` / `JsonRpcClientHostedService` / `SignalEventService` as canonical examples, sentinel-type idempotency guard with `SignalCliRegistrationMarker` and NF-003 reference for full rationale).
  - **Addition to "Conventions" section — "Naming and namespace hierarchy"** (4 bullets covering the three `Services.*` namespaces, `*Parameters`/`*Response` DTO convention with `JsonContextRegistrationTests` enforcement reference, `*EventArgs` records with `EventApiSymmetryTests` RG06 enforcement reference, test-class naming + folder structure).
  - **Addition to "Established patterns" → "Other established patterns"** (1 bullet — exception-derivation heuristic with two current derived-types as examples + "would catch-by-type lead to different code than catch-base + inspect Code?" decision rule).

Total addition: ~22 lines. CLAUDE.md grows 556 → ~578 (still 2.9× over Anthropic 200-line target but unchanged on order-of-magnitude; restructure (separate change) is the long-term fix for size).

No code change. No test change. No regression-guard needed for documentation additions (R03 PublicApiSurfaceTests pins public-API surface; nothing pins doc-content completeness, which is correct — that's an editorial concern, not a structural invariant).

## Capabilities

### New Capabilities

- **`claude-md-pattern-additions`**: CLAUDE.md SHALL explicitly document three under-documented patterns that the codebase consistently enforces:
  1. DI registration idioms (`TryAddSingleton` vs `AddSingleton`, one-instance-two-roles via `AddHostedService(sp => sp.GetRequiredService<T>())`, sentinel-type idempotency guard).
  2. Naming and namespace hierarchy (three `Services.*` namespaces, `*Parameters`/`*Response` DTOs, `*EventArgs` records, `*Tests` test classes with folder mirroring).
  3. Exception derivation heuristic (derive only for high-leverage / consumer-actionable RPC error codes; rare codes stay base).

### Modified Capabilities

- **None.** No existing rule changes; only additions. R01–RG07 + the proposed RG08 (from `claude-md-rules-restructure`) are unaffected.

## Out of scope

- **The structural restructure of CLAUDE.md into root + `.claude/rules/<topic>.md`.** That's separate change `claude-md-rules-restructure` (already filed, plan-only). If both changes execute, this change's additions go into the appropriate topic file (DI + exceptions → `.claude/rules/patterns.md`; naming → `.claude/rules/conventions.md`) instead of monolithic CLAUDE.md. Execution-order doesn't matter; both end-states are valid.
- **Adding regression-guard tests for documentation content.** RG-tests pin structural invariants (EventId blocks, public API surface, version lockstep, etc.) — doc-content completeness is editorial and best caught by audit-cycle review, not build-time tests. The audit cycle that generated this change IS the mechanism.
- **Documenting every other minor pattern audit could surface.** This change is scoped to the 3 specific gaps from audit v2.1; other "X could be documented better" findings go in future audit cycles.
- **Rewriting the existing CLAUDE.md "Established patterns" subsection structure.** Additions slot into existing subsections; no reorganization.
