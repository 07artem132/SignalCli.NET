# Design — api-coverage-audit-followup

## Context

This change addresses 5 findings from the post-merge code review of `signal-cli-api-coverage` (PR #18, merged 2026-05-25 → 4.1.0-4.9.0). Findings come from a 3-dimensional pass: code-reuse, code-quality, efficiency. See PR #18 review thread for original wording. Each finding maps 1:1 to a capability below.

Source-of-truth verification was done against upstream signal-cli @ `bda4e7fc` (the pinned commit in [SignalCli.runtime.csproj](src/SignalCli.runtime/SignalCli.runtime.csproj)). Where this design depends on a specific upstream line, it cites `signal-cli/<path>:<line> @ bda4e7fc`.

## Decision 1 — `IdentityChangedException`: deprecate, do not delete

### Considered

- **Option A (wire dispatch via substring match):** add `JsonRpcClient.cs:511`-style `Contains("changed", OrdinalIgnoreCase)` branch for code `-4`.
- **Option B (delete the type immediately):** breaking change, 5.0 timing.
- **Option C (deprecate with `[Obsolete]`, scheduled removal in 5.0):** chosen.

### Why Option C

Option A is **impossible** — upstream verification on 2026-05-25 grep'd `signal-cli/src/main/java` for `IdentityChange|isNewIdentity|firstContact|IdentityKeyChange|keyChange|hasOnlyUntrustedIdentity` and found **zero distinguishing logic**:

- [`SendMessageResultUtils.java:60`](file:///C:/Users/ivank/Нова%20папка/signal-cli/src/main/java/org/asamk/signal/util/SendMessageResultUtils.java#L60) throws `UntrustedKeyErrorException("Failed to send message due to untrusted identities")` — single fixed string, always plural, no variation.
- [`JsonSendMessageResult.Type`](file:///C:/Users/ivank/Нова%20папка/signal-cli/src/main/java/org/asamk/signal/json/JsonSendMessageResult.java#L42-L50) has one identity-related enum value: `IDENTITY_FAILURE`, not `IDENTITY_NEW`/`IDENTITY_CHANGED`.
- [`SignalJsonRpcCommandHandler.java:256-259`](file:///C:/Users/ivank/Нова%20папка/signal-cli/src/main/java/org/asamk/signal/jsonrpc/SignalJsonRpcCommandHandler.java#L256-L259) passes `e.getMessage()` through verbatim.

Distinguishing first-contact-unknown from re-installed identity is **only possible client-side** by cross-referencing against `listIdentities`. That's a consumer concern (caching + diff), not a wrapper concern.

Option B is breaking: the type is in `SignalCli.public-api.txt` baseline from 4.1.0 onward. Per [.claude/rules/obsolete-shims.md](.claude/rules/obsolete-shims.md) "one-major-grace" convention, public-API removals get one major-version's worth of `[Obsolete]` shim before deletion.

Option C: mark `[Obsolete("...; will be removed in 5.0.", DiagnosticId = "SIGNALCLI001")]`. The R04 regression-guard (`ObsoleteMessageConsistencyTests`) auto-verifies the "5.0" version reference is strictly greater than current major (4). Consumers see deprecation warning at compile time; existing catch-blocks continue to function (the type still inherits from `UntrustedIdentityException`).

### Lesson for §0.5 anti-hallucination protocol

This finding **slipped through** the §0.5 protocol that landed in the same PR. The type's XMLDoc cites `SignalJsonRpcCommandHandler.java:248-273` — those lines exist and contain the `-4` mapping — but the cited region shows only generic `UntrustedKeyErrorException → -4`, no split. The author cited the file but didn't verify the cited lines actually support the claimed behavior.

Working-style addition for [.claude/rules/audit-debt.md](.claude/rules/audit-debt.md) (lands in capability `protocol-checklist-amend`):

> **§0.5 cite-and-read, not cite-and-trust.** When citing an upstream line range as protocol evidence, read those lines AND grep the broader file for contradictory or extending logic before deriving a wrapper-side type/method/enum from the claim. Wave 1's `IdentityChangedException` cited correct lines but didn't verify those lines exclude alternative interpretations.

## Decision 2 — `JsonPayment.Receipt`: nullable

### Upstream contract

[`JsonPayment.java`](file:///C:/Users/ivank/Нова%20папка/signal-cli/src/main/java/org/asamk/signal/json/JsonPayment.java) (read at `bda4e7fc`):

```java
public record JsonPayment(String note, byte[] receipt) { ... }
```

Java has no NRT; both `note` and `receipt` can be `null` at runtime. STJ source-gen for C# 14 reference-type properties does NOT enforce `byte[]` non-nullability — `"receipt": null` deserializes to `null` assigned into the slot. Consumer pattern `payment.Receipt.Length` NREs.

### Change

`Envelope.cs:155` from `byte[] Receipt` to `byte[]? Receipt`. Two new serialization tests pin the `null` and missing-field cases.

### Semver classification

Technically a public-API contract widening (`byte[]?` is a superset of `byte[]`). Old consumer code reading `.Length` directly will now produce CS8602 (NRT warning) — that's a **compile-time signal**, not a runtime break. Plus: `JsonPayment` shipped in 4.9.0 (3 days ago in this change's timeline), no production users had time to ship code against the old shape. **Effectively non-breaking**; documented in CHANGELOG as such.

## Decision 3 — `OnNotificationReceived` refactor: extract helper

### Current shape

13 near-identical blocks of:

```csharp
if (data.SomeUnionMember is not null)
{
    var evt = new SomeEventArgs(subscriptionId, account, data.SomeUnionMember,
        jsonEnvelope.Source, jsonEnvelope.SourceNumber, /* 7 more envelope fields */);
    _someSubject.OnNext(evt);
    TryWriteOrDrop(_someChannel, evt, "some_label");
    emitted = true;
}
```

Each Wave-7b addition compounded the duplication. The 6 pre-existing branches predate this change and would be migrated atomically with the 7 new ones.

### Helper

```csharp
private bool DispatchUnionMember<TPayload, TArgs>(
    TPayload? payload,
    Func<TPayload, TArgs> makeArgs,
    Subject<TArgs> subject,
    Channel<TArgs> channel,
    string kindLabel)
    where TPayload : class
{
    if (payload is null) return false;
    var args = makeArgs(payload);
    subject.OnNext(args);
    TryWriteOrDrop(channel, args, kindLabel);
    return true;
}
```

Call sites become:

```csharp
emitted |= DispatchUnionMember(data.PollCreate,
    p => new PollCreateEventArgs(subscriptionId, account, p, jsonEnvelope.Source, /* ... */),
    _pollCreates, _pollCreateChannel, "poll_create");
```

### Why not a tuple-of-envelope-fields shared closure

Considered hoisting the 9 envelope-field reads (`Source`, `SourceNumber`, `SourceUuid`, ...) into a single tuple/local-record and threading it through the helper. **Rejected** because the EventArgs ctors have positional parameters in a fixed order tied to inheritance from `BaseSignalEventArgs`; threading a tuple through requires either deconstruction-at-callsite (verbose) or a builder-style `WithEnvelope(...)` method on EventArgs (cross-cutting refactor outside scope). Closure-capture from the enclosing scope is the path of least resistance and existing presence-based-union dispatch tests cover all 13 emit paths.

### Guard against regression

No new test needed — `EventApiSymmetryWave7bTests` + the existing presence-based dispatch suites cover the 13 paths. The refactor is behavior-preserving by construction; if a branch's lambda incorrectly captures the wrong field or wrong type, compile fails.

## Decision 4 — `CaptchaRequiredException` dispatch test

Trivial symmetry fix with [`NewTypedRpcErrorsTests.cs:70-77`](Tests/SignalCli.Tests/Exceptions/NewTypedRpcErrorsTests.cs:70):

```csharp
[Fact]
public Task InvokeMethodAsync_Code_Minus6_ThrowsCaptchaRequired() =>
    AssertDispatchedException<CaptchaRequiredException>(
        """{"id":"1","error":{"code":-6,"message":"Captcha required"}}""",
        ex => Assert.Equal(JsonRpcErrorCode.CaptchaRejected, ex.KnownCode));
```

No production change. Pins the existing `JsonRpcClient.cs:510` dispatch arm.

## Decision 5 — `signal-cli-protocol.md` 8th fact + checklist amend

[.claude/rules/signal-cli-protocol.md](.claude/rules/signal-cli-protocol.md) currently lists 7 upstream-pinned facts + "When bumping `<SignalCliVersion>` re-verify each of the seven facts." Adding:

**8th fact** (after the Java-25 fact):

> - **No JSON-RPC distinction between first-contact-unknown identity and re-installed identity (key change).** [`SendMessageResultUtils.java:60 @ bda4e7fc`](file:///C:/Users/ivank/Нова%20папка/signal-cli/src/main/java/org/asamk/signal/util/SendMessageResultUtils.java#L60) throws `UntrustedKeyErrorException("Failed to send message due to untrusted identities")` — fixed string, plural, no variation. [`JsonSendMessageResult.Type.IDENTITY_FAILURE`](file:///C:/Users/ivank/Нова%20папка/signal-cli/src/main/java/org/asamk/signal/json/JsonSendMessageResult.java#L46) is a single enum value. Distinguishing the two cases requires client-side `listIdentities` cross-reference. Wave-1's `IdentityChangedException` (deprecated 4.10.0, removed 5.0) was a speculative split with no upstream distinguisher.

**Checklist amendment** (appended to the footer paragraph):

> Additionally re-grep upstream for the load-bearing exception-message substrings used in `JsonRpcClient.InvokeMethodAsync`'s typed-exception dispatch switch — currently `"admin"` (case-insensitive) for `GroupAdminRequiredException`. If upstream changes the wording, the substring match silently demotes the typed exception back to base `JsonRpcException`; re-grep `org.asamk.signal.commands/Group*Command.java` confirms the load-bearing token still appears.

Doc-only change. Future versions of this rules file gain new pinned facts when newly verified.

## Risks / Mitigations

| Risk | Mitigation |
|---|---|
| Consumer who somehow had `catch (IdentityChangedException)` working (impossible per upstream, but defensive) breaks after we mark `[Obsolete]` | The catch block still compiles — `[Obsolete]` is a warning, not an error. The behavior change is zero (was never caught anything). Documented in CHANGELOG. |
| `JsonPayment.Receipt` nullability adds CS8602 warning to consumer code | Documented in CHANGELOG as a deliberate honesty fix; `?.Length` / null-check is the migration. Wave 7b just shipped (4.9.0); blast radius effectively zero. |
| Refactor introduces subtle bug not caught by existing tests | `EventApiSymmetryWave7bTests` + presence-based dispatch tests cover all 13 paths positively + negatively. The helper's signature forces same field ordering as before; closure-captured envelope fields can't go wrong unless you swap subjects/channels (which the helper's generic parameters constrain). |

## Migration

None required.
- Existing `catch (UntrustedIdentityException)` blocks continue to function — the deprecated `IdentityChangedException` still derives from it.
- Existing `payment.Receipt.Length` calls produce CS8602 warning — fix with `?.Length` or null-check (5-second edit per call site).
- No public-API symbols are removed or renamed in 4.10.0.

## Open questions

None — upstream verification closed the substring question; the rest are straightforward implementations.
