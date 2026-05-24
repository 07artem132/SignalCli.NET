# Design — json-hardening-source-gen-attribute

## Method

One capability, one source-side edit (the `[JsonSourceGenerationOptions]`
attribute on `SignalJsonContext`), one test addition pinning the new behavior,
one inline-doc cleanup (the "КАВЕAT" block in `JsonSerializationTests.cs` that
documented the bypass becomes obsolete), and one CLAUDE.md rule-#18 phrasing
update so the dual-site requirement is explicit.

This is a **safety net** change: production code on the JSON-RPC wire is
working correctly today because signal-cli (Jackson 2.20.2) is itself
duplicate-key-free by construction (records have one property per name, no
manual `JsonNode`-style writes). The motivation is defense-in-depth against:

1. **Malicious MITM** on the stdio channel (unlikely but cheap to harden against).
2. **Future signal-cli protocol drift** if a new envelope variant introduces
   non-record types that could theoretically emit duplicates.
3. **Trusting CLAUDE.md rule #18 to actually be enforced.** Today the rule says
   "MUST fail loudly with JsonException, never silently follow last-wins semantics"
   but the only path that fires this is unused in production. Closing the gap
   makes the documentation honest.

## Affected files

| File | Change | Lines |
|------|--------|-------|
| `src/SignalCli/Serialization/SignalJsonContext.cs:23` | Add `AllowDuplicateProperties = false` to existing `[JsonSourceGenerationOptions(...)]` attribute | 1 |
| `Tests/SignalCli.Tests/JsonSerializationTests.cs` (RG05 block) | Replace the inline "КАВЕAT" block; add third fact `SignalJsonContext_AllowDuplicateProperties_ThrowsOnDuplicateKey`; the two existing facts stay | ~25 |
| `CLAUDE.md` rule #18 | Tighten phrasing to require both `SignalJson.Options` flag AND `[JsonSourceGenerationOptions]` attribute | ~3 |

## API surface

[`JsonSourceGenerationOptionsAttribute.AllowDuplicateProperties`](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonsourcegenerationoptionsattribute.allowduplicateproperties)
(verified via MS Learn `.NET 10` documentation page) — type `bool`, default `true`,
"Specifies the default value of [AllowDuplicateProperties] when set." The
generator reads this at compile-time and emits a duplicate-check into the
generated `Utf8JsonReader` loop.

Important note from the same MS Learn page: this is a **default** that the
runtime `JsonSerializerOptions.AllowDuplicateProperties` (when explicitly set
on a passed-in `JsonSerializerOptions` instance) can still override. But our
production code always uses `SignalJsonContext.Default.X` (which uses the
attribute-time default), not `JsonSerializer.Serialize(_, options, _, ...)` with
custom options — so the attribute is the binding contract.

## Validation strategy

Three tests after this change:

1. `SignalJsonOptions_AllowDuplicateProperties_IsFalse` (audit v2.1 — unchanged) —
   pins the runtime flag.
2. `JsonDocumentOptions_AllowDuplicateProperties_False_ThrowsOnDuplicateKey`
   (audit v2.1 — unchanged) — proves the .NET 10 underlying API works.
3. `SignalJsonContext_AllowDuplicateProperties_ThrowsOnDuplicateKey` (NEW) —
   actually exercises the source-gen path that production uses.

The three facts pin three orthogonal layers — runtime-flag, framework-level API,
source-gen-level attribute. Removing any single layer surfaces immediately.

## Risk analysis

**Risk 1: existing tests break because some test feeds duplicate keys.**
Mitigation: a grep across `Tests/SignalCli.Tests/**/*.cs` for the literal pattern
of repeated keys in a single JSON object string (manual review of strings around
`Deserialize`, `Parse`, `JsonRpcResponse`) before changing the attribute. If any
test does, we either fix the test's input or document why duplicate-key was
intentional. Expectation based on test-suite review at audit v2.1 time: zero
sites — none of the 286 tests inject duplicate keys.

**Risk 2: the source-gen-generated code is larger / slower.**
Mitigation: the duplicate-check is a `BitArray` (or equivalent compact-bitmap)
per type, sized to property count. For our DTOs (most have ≤ 10 properties),
the overhead is one bitmap-write per assignment-call — sub-microsecond per
deserialize. Negligible vs. the JSON-RPC RTT (typically ≥ 5ms even for `version`).
No need to measure — overhead is dominated by signal-cli IPC.

**Risk 3: `[JsonSourceGenerationOptions]` attribute may not respect
AllowDuplicateProperties in all .NET 10 patch versions.**
Mitigation: the third test (`SignalJsonContext_AllowDuplicateProperties_ThrowsOnDuplicateKey`)
IS the validation. If the attribute is broken in a future patch, this test fails
loud and we know immediately.

## Why one commit

Three-line attribute change + test addition + doc tightening — too small to
split. Mirrors `audit-followup-2026 §json-hardening` shape (also single-commit).
