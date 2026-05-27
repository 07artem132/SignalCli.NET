## ADDED Requirements

### Requirement: `.claude/rules/signal-cli-protocol.md` SHALL document the 8th pinned upstream fact

`.claude/rules/signal-cli-protocol.md` SHALL gain an 8th bullet, inserted after the Java-25 fact, citing both upstream source locations at commit `bda4e7fc`:

> - **No JSON-RPC distinction between first-contact-unknown identity and re-installed identity (key change).** `signal-cli/src/main/java/org/asamk/signal/util/SendMessageResultUtils.java:60 @ bda4e7fc` throws `UntrustedKeyErrorException("Failed to send message due to untrusted identities")` — fixed string, plural, no variation. `signal-cli/src/main/java/org/asamk/signal/json/JsonSendMessageResult.java:46 @ bda4e7fc` has `Type.IDENTITY_FAILURE` as a single enum value (not `_NEW`/`_CHANGED`). Distinguishing the two cases requires client-side `listIdentities` cross-reference. Wave-1's `IdentityChangedException` (deprecated 4.10.0, removed 5.0) was a speculative split with no upstream distinguisher.

#### Scenario: Future contributor reading `.claude/rules/signal-cli-protocol.md` finds the IdentityChanged context

- **GIVEN** a contributor exploring why `IdentityChangedException` is `[Obsolete]` in 4.10.0+
- **WHEN** they open `.claude/rules/signal-cli-protocol.md`
- **THEN** the 8th pinned fact explains the upstream-level non-distinction
- **AND** cross-references to upstream source paths + line numbers at `bda4e7fc` are present

### Requirement: Version-bump checklist SHALL include exception-substring stability verification

The footer paragraph of `.claude/rules/signal-cli-protocol.md` SHALL be amended:

> When bumping `<SignalCliVersion>` in `SignalCli.runtime.csproj`: re-verify each of the eight facts above against the new signal-cli source. The PR description SHALL include a one-line confirmation that these facts were re-verified, even if zero edits resulted. **Additionally re-grep upstream for the load-bearing exception-message substrings used in `JsonRpcClient.InvokeMethodAsync`'s typed-exception dispatch switch — currently `"admin"` (case-insensitive) for `GroupAdminRequiredException`. If upstream changes the wording, the substring match silently demotes the typed exception back to base `JsonRpcException`; re-grep `org.asamk.signal.commands/Group*Command.java` confirms the load-bearing token still appears.** Discrepancies SHALL be resolved either by adapting the wrapper or by updating this section + the commit citation.

(7 → 8 facts; new "Additionally re-grep" sentence appended; existing review-time enforcement model preserved.)

#### Scenario: Future contributor bumping `<SignalCliVersion>` reads the amended checklist

- **GIVEN** signal-cli releases `0.14.4` (after `bda4e7fc`)
- **AND** a contributor edits `<SignalCliVersion>` in `SignalCli.runtime.csproj`
- **WHEN** they open `.claude/rules/signal-cli-protocol.md` per the rule-file frontmatter trigger
- **THEN** the checklist lists 8 facts to re-verify
- **AND** the substring-stability instruction is the bottom of the same paragraph

### Requirement: `.claude/rules/audit-debt.md` SHALL document the §0.5 cite-and-read lesson

`.claude/rules/audit-debt.md` § "Working style" SHALL gain a new bullet codifying the lesson from the `IdentityChangedException` finding:

> - **§0.5 cite-and-read, not cite-and-trust.** When citing an upstream line range as protocol evidence in XMLDoc or design docs, read those lines AND grep the broader file for contradictory or extending logic before deriving a wrapper-side type/method/enum from the claim. Wave-1's `IdentityChangedException` (deprecated 4.10.0) cited `SignalJsonRpcCommandHandler.java:248-273` which contained the `-4` mapping but did not contain a first-contact-vs-re-install distinguisher — the citation existed, the verification did not. Detected post-merge by audit pass on 2026-05-25.

#### Scenario: Future contributor working in §0.5-protocol-coverage capability reads the working-style addition

- **GIVEN** a future PR adds a wrapper-side type derived from an upstream Java exception-type
- **AND** the PR XMLDoc cites a specific line range
- **WHEN** the reviewer scans `.claude/rules/audit-debt.md` § Working style
- **THEN** the cite-and-read bullet reminds them to also grep the broader file for distinguishing logic before approving the derivation
