---
paths:
  - "src/SignalCli/**"
---

# Backward compatibility convention

When we deprecate API, the rule is **one major version of `[Obsolete]` shim** before removal.

**Already removed in 3.0** (see `CHANGELOG.md [3.0.0]`):
- `*Options.CancellationToken` field + `WithCancellationToken` builder method on `TextMessageOptions`/`AttachmentMessageOptions`/`StickerMessageOptions` (round 9 §4.7).
- `ISignalMessage.{SendText,SendAttachment,SendSticker}MessageAsync` returning `Task<List<SendMessageResponse>>` (now `Task<SendMessageResponse>`, round 9 §4.23-§4.24).
- `InvokeMethodAsync<TResponse, TRequest>` old generic-param order (round 9 §4.27) — no shim possible (C# overload resolution can't disambiguate generic-arity reorders).
- `FinishLinkResponse.number`/`SubscribeReceiveResponse.id` lowercase wire shape — replaced with PascalCase properties + `[JsonPropertyName]`.

**Currently in flight (will be removed in 4.0):**
- `ISignalCliClient.Version()` — DIM shim delegating to `VersionAsync()`. Still present in [`ISignalCliClient.cs:54-57`](../../src/SignalCli/Interfaces/SignalCli/ISignalCliClient.cs).
- `ServiceCollectionExtensions.AddSignalCli(Action<Config>?)` — legacy overload. Still present in [`ServiceCollectionExtensions.cs:123-139`](../../src/SignalCli/Extensions/ServiceCollectionExtensions.cs). Integration E2E tests still depend on `Config.CreateDefault()`-auto-resolve of bundled-JRE; tests use `#pragma CS0618 disable` around the call site. Real production consumers should migrate to `AddSignalCli(Action<SignalCliOptions>?)` or `AddSignalCli(IConfiguration)`.
- `SignalCli.Models.Config` itself — `[Obsolete]` class. Stays as long as the `Action<Config>?` overload + the `Config.ToOptions` / `SignalCliOptionsExtensions.ToOptions(Config)` / `ServiceCollectionExtensions.CopyFrom` triplet stay (see "Three-site duplication trap" below).
- `ISignalAccounts.ListAccounts` / `SyncAccount` / `ISignalDevices.StartLink` / `FinishLink` / `ISignalGroups.ListGroups` — Async-suffix-less shim methods, kept as `[Obsolete]` DIMs per round 9 §4.x.

**Doc-sync invariant.** Every `[Obsolete("...; will be removed in N.0")]` attribute message — N MUST be strictly greater than the current package major version. The same applies to Ukrainian XML doc / inline comments announcing "буде видалений у N.0" / "зникне у N.0" / "removed in N.0". Drift here lies to consumers and trains AI agents to disbelieve `[Obsolete]` lifetime claims. The `2026-05-24` audit found 6 sites still saying "3.0" in 3.0.0 source — `audit-followup-2026` capability `obsolete-doc-sync` corrects them and lands `ObsoleteMessageConsistencyTests` so drift becomes a build failure.

**Three-site duplication trap (in flight to 4.0).** Adding a new property to `SignalCliOptions` today requires updating three near-mirror field-copiers — `Config.ToOptions()`, `SignalCliOptionsExtensions.ToOptions(Config)`, and `ServiceCollectionExtensions.CopyFrom`. Collapsing them into one mapper now is throwaway work because all three disappear with `Config` in 4.0. Until then, when you add a property, update all three. There is intentionally no reflective drift-guard for this — the risk is bounded by the 4.0 cleanup horizon.

When adding a new deprecation, mirror this shape: real new API + `[Obsolete("Use Y; will be removed in N.0")]` shim that delegates, plus a `CHANGELOG.md` entry under "Інше". Internal call sites are migrated immediately; external call sites get one major release of grace. **Exception:** when the shim is technically impossible (generic-order, ctor-overload-ambiguity per `JsonRpcException` §4.22, etc.), do the pure removal and document the impossibility in the CHANGELOG migration note.
