## ADDED Requirements

### Requirement: CLAUDE.md SHALL document the signal-cli protocol behaviors the wrapper relies on

CLAUDE.md SHALL contain a new H2 section titled **"signal-cli protocol behavior we depend on"** placed after "Conventions (match the existing code)" and before "Established patterns". The section SHALL document seven facts about upstream signal-cli that our wrapper relies on, each with a citation to a specific signal-cli source file at commit `bda4e7f` (after 0.14.3):

1. **Graceful shutdown trigger** = stdin EOF or SIGTERM/SIGINT (cite `JsonRpcReader.java:59-75`, `Shutdown.java:24-25`).
2. **Stdout** = pure JSON-RPC, line-flushed (cite `JsonWriterImpl.java:30`).
3. **Parallel request processing** via virtual threads (cite `JsonRpcReader.java:58`) → match by `id`, not order.
4. **`subscribeReceive`** non-idempotent at the protocol level (cite `SignalJsonRpcDispatcherHandler.java:143`) → our reservation TCS pattern is mandatory.
5. **Jackson `maxStringLength`** = 20_000_000 per string token (cite `gradle/libs.versions.toml:10`).
6. **Custom error codes** `-1..-6` (cite `SignalJsonRpcCommandHandler.java:35-280`).
7. **Java 25 requirement** for signal-cli 0.14.0+ (cite `build.gradle.kts:7-8`).

#### Scenario: An agent verifying a fact can re-locate the signal-cli source
- **GIVEN** an agent reading the "signal-cli protocol behavior we depend on" section
- **WHEN** the agent wants to verify fact #6 (custom error codes) against a newer signal-cli release
- **THEN** the citation `SignalJsonRpcCommandHandler.java:35-280 @ bda4e7f` is sufficient to clone the upstream repo, check out the cited commit, and read the cited lines
- **AND** the agent does not need to re-run a code investigation to know where to look

### Requirement: A maintainer bumping signal-cli SHALL re-verify each cited fact

When `SignalCli.runtime.csproj`'s `<SignalCliVersion>` is bumped, the maintainer SHALL re-check every cited fact in the "signal-cli protocol behavior we depend on" section against the new signal-cli source. Discrepancies SHALL be resolved either by adapting the wrapper or by updating the section + commit citation. The bump PR description SHALL include a one-line confirmation that the seven facts were re-verified, even if the verification took zero edits.

#### Scenario: Bump-signal-cli PR documents the re-verification
- **GIVEN** a PR bumps `<SignalCliVersion>` from `0.14.3` to `0.15.0`
- **WHEN** the PR description is reviewed
- **THEN** it includes the phrase "signal-cli protocol behaviors re-verified" (or equivalent acknowledgment)
- **AND** any divergent behavior is called out as a separate commit in the same PR
