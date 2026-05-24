## ADDED Requirements

### Requirement: `AddSignalCli` SHALL actually be idempotent across all three overloads

`ServiceCollectionExtensions.AddSignalCli(Action<SignalCliOptions>?)`, `AddSignalCli(IConfiguration)`, and the legacy `AddSignalCli(Action<Config>?)` SHALL each detect prior registration and short-circuit on every subsequent invocation. The current guard — `services.Any(d => d.ServiceType == typeof(IOptions<SignalCliOptions>) || d.ServiceType == typeof(SignalCliOptions))` — never matches because `IOptions<T>` is registered as an open-generic descriptor (`typeof(IOptions<>)`), not as a concrete `typeof(IOptions<SignalCliOptions>)`, and `SignalCliOptions` itself is never directly registered. The guard SHALL be replaced with a check for a private sentinel type (e.g., `SignalCliRegistrationMarker`) that AddSignalCli registers exactly once on its first successful call.

#### Scenario: Second AddSignalCli leaves descriptor count unchanged
- **GIVEN** an `IServiceCollection` after one `services.AddSignalCli(o => …)` call
- **AND** `var descriptorCountAfterFirst = services.Count`
- **WHEN** `services.AddSignalCli(o2 => …)` is called a second time on the same collection
- **THEN** `services.Count == descriptorCountAfterFirst` (no new descriptors added)

#### Scenario: First call's options win
- **GIVEN** `services.AddSignalCli(o => o.AppHome = "/first")` was called first
- **WHEN** `services.AddSignalCli(o => o.AppHome = "/SECOND")` is called second
- **AND** the consumer resolves `IOptions<SignalCliOptions>.Value`
- **THEN** `AppHome == "/first"` (second-call delegate did NOT execute)

#### Scenario: Mixed overloads also idempotent
- **GIVEN** `services.AddSignalCli(o => …)` was called first
- **WHEN** `services.AddSignalCli(configuration.GetSection("SignalCli"))` is called second
- **THEN** `services.Count` is unchanged
- **AND** the resolved options reflect the first call's values

### Requirement: Hosted-service duplication SHALL NOT occur on repeated registration

The 3 `services.AddHostedService(…)` calls inside `RegisterCoreServices` (for `SignalCliHostedService`, `JsonRpcClientHostedService`, `SignalCliHealthMonitor`) SHALL execute exactly once across any number of `AddSignalCli` invocations on the same collection. Today they execute on every call — the idempotency guard short-circuit happens BEFORE `RegisterCoreServices`, but the broken guard never fires, so the `AddHostedService` calls accumulate (3 duplicate descriptors per extra `AddSignalCli`). After this fix, repeated registration is a true no-op and the host starts exactly 3 hosted services.

#### Scenario: Repeated registration registers each hosted service exactly once
- **GIVEN** `services.AddSignalCli(o => …)` called twice on the same collection
- **WHEN** the consumer enumerates `services.Where(d => d.ServiceType == typeof(IHostedService))`
- **THEN** the count of hosted-service descriptors attributable to `AddSignalCli` is exactly 3
- **AND** during `host.StartAsync` each of `SignalCliHostedService`, `JsonRpcClientHostedService`, and `SignalCliHealthMonitor` has `StartAsync` invoked exactly once

### Requirement: CHANGELOG MUST reflect the corrected idempotency contract

The `CHANGELOG.md [3.0.0]` entry currently declares `AddSignalCli` overloads idempotent. That claim was over-broad: the guard's NEVER matched, so behavior was "first call works, every subsequent call duplicates hosted services and re-runs configure delegates". The patch-release entry (e.g., `[3.0.3]` or whichever version this fix lands as) SHALL document the fix and the previous broken behavior so users who were affected understand what changed.

#### Scenario: CHANGELOG documents the previously broken behavior
- **WHEN** a reader of `CHANGELOG.md` looks at the patch-release entry for this fix
- **THEN** they find a `### 🐛 Виправлено` (or equivalent) section
- **AND** the entry names `AddSignalCli` idempotency as the affected behavior
- **AND** the entry states that pre-fix versions ran the configure delegate twice and registered 3 duplicate hosted services on every extra call
