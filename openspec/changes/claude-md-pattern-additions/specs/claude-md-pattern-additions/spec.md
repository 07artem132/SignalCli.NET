## ADDED Requirements

### Requirement: CLAUDE.md SHALL document the three under-documented patterns the codebase enforces

CLAUDE.md (or, if `claude-md-rules-restructure` has executed first, the appropriate `.claude/rules/<topic>.md` files) SHALL contain explicit documentation for three patterns that the codebase consistently enforces but that previously lived only in implicit-code-knowledge:

1. **DI registration patterns** — `TryAddSingleton<T>` vs `AddSingleton<T>` choice (use `TryAdd*` for our concrete services, `Add` only for `IHostedService` slots); "one-instance-two-roles" idiom (`AddHostedService(sp => sp.GetRequiredService<TConcrete>())` paired with `TryAddSingleton<IInterface>(sp => sp.GetRequiredService<TConcrete>())`); idempotency-guard-via-sentinel-type (`SignalCliRegistrationMarker`, NOT the broken `IOptions<T>`-presence check).
2. **Naming and namespace hierarchy** — three `Services.*` namespaces partitioned by domain (Rpc / SignalCli / Signal); `*Parameters` / `*Response` DTO convention with `Models/Signal/<DomainArea>/` placement; `*EventArgs` records with `Models/Signal/Events/` placement; `*Tests` test classes with folder mirroring; regression-guard tests under `RegressionGuards/` subfolder regardless of pinned-production type.
3. **Exception derivation heuristic** — derive `XxxException : JsonRpcException` only for consumer-actionable, high-frequency RPC error codes (current: `RateLimitException` for `-5`, `UntrustedIdentityException` for `-4`); rare codes stay base + inspect `KnownCode` enum.

Each documented pattern SHALL cite the enforcement mechanism that prevents drift (e.g. `JsonContextRegistrationTests` for DTO registration, `EventApiSymmetryTests` RG06 for event-API symmetry, `addsignalcli-idempotency-fix` capability for sentinel-marker rationale). Documentation that doesn't cite the enforcement test/capability is descriptive-only and risks staleness on future refactor.

#### Scenario: New contributor adding an RPC method finds DTO-placement rule in CLAUDE.md
- **GIVEN** a new contributor (human or AI agent) reads CLAUDE.md to learn where to put a new `*Parameters` / `*Response` DTO
- **WHEN** they search for "DTO" or "Parameters" in CLAUDE.md (or in the appropriate topic file after restructure)
- **THEN** they find an explicit rule: "Both go under `Models/Signal/<DomainArea>/`. Every `*Parameters` / `*Response` type MUST be registered in `Serialization/SignalJsonContext.cs` via `[JsonSerializable(typeof(T))]`"
- **AND** the rule cites the enforcement test (`JsonContextRegistrationTests`) so the contributor knows breakage will be caught at build-time, not at runtime

#### Scenario: New contributor adding a Signal facade finds DI-registration idiom in CLAUDE.md
- **GIVEN** a new contributor adding a new Signal-protocol facade (e.g. `SignalProfile`) that should be both an `IHostedService` and resolvable via a typed interface
- **WHEN** they search for "AddHostedService" or "one-instance" in CLAUDE.md
- **THEN** they find the canonical 3-line registration template:
  ```csharp
  services.TryAddSingleton<SignalCliHostedService>();
  services.AddHostedService(sp => sp.GetRequiredService<SignalCliHostedService>());
  services.TryAddSingleton<IStreamPairProvider>(sp => sp.GetRequiredService<SignalCliHostedService>());
  ```
- **AND** the rule lists existing canonical sites (`SignalCliHostedService`, `JsonRpcClientHostedService`, `SignalEventService`) for cross-reference

#### Scenario: New contributor sees a high-frequency signal-cli error and considers a derived exception
- **GIVEN** a new contributor working with a signal-cli RPC error code that appears in consumer code regularly (e.g. hypothetical new `-7 SubscriptionExpired`)
- **WHEN** they consult CLAUDE.md for guidance on deriving a new `XxxException : JsonRpcException`
- **THEN** they find the heuristic: "Would `catch (XxxException)` lead to materially different consumer code than `catch (JsonRpcException) when (ex.KnownCode == JsonRpcErrorCode.Xxx)`?"
- **AND** the rule lists the existing two derived types (`RateLimitException`, `UntrustedIdentityException`) as examples of "yes, derive" cases
- **AND** the rule lists current `-1` / `-3` / `-6` codes as examples of "no, stay base" cases

#### Scenario: Restructure executes first; additions land in topic files instead of root
- **GIVEN** `claude-md-rules-restructure` has executed before this change
- **WHEN** this change executes
- **THEN** the DI-registration addition lands in `.claude/rules/patterns.md` (which inherited the "Established patterns" content per restructure's section-to-file mapping)
- **AND** the naming addition lands in `.claude/rules/conventions.md`
- **AND** the exception-derivation addition lands in `.claude/rules/patterns.md` (under "Other established patterns")
- **AND** the post-execution state is identical to "this change first, restructure later" — same content, same path-scoping
