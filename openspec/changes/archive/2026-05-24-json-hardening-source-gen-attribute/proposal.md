# Source-gen JSON hardening — close the AllowDuplicateProperties bypass

## Why

`audit-followup-2026 §6` shipped `json-hardening` which sets
`SignalJson.Options.AllowDuplicateProperties = false` and was supposed to make
the production JSON deserializer fail-loud on duplicate keys per JSON-RPC 2.0
spec (CLAUDE.md rule #18). The audit-followup-2026 spec scenario reads:

> **GIVEN** the input `{"jsonrpc":"2.0","jsonrpc":"X","id":"1","result":{}}`
> **WHEN** `JsonSerializer.Deserialize<JsonRpcResponse>(input, SignalJson.Options)` is called
> **THEN** a `JsonException` is thrown

`audit v2.1` discovered during T03 implementation that **this scenario does
NOT actually fire on the production code path**. The `AllowDuplicateProperties`
property on `JsonSerializerOptions` is a runtime flag consulted by the
reflection-based metadata resolver and by source-gen `GenerationMode = Metadata`.
But `SignalJsonContext` is annotated with
`[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]`
(`src/SignalCli/Serialization/SignalJsonContext.cs:23`), which selects the
fast-path generator. Fast-path emits its own `Utf8JsonReader`-based parser that
**does not consult the runtime flag** — it generates direct token-by-token
property assignment without a duplicate-key check.

Net effect: `SignalJson.Options.AllowDuplicateProperties = false` is set in
production code but is dead-flag for the only call-sites that matter
(`SignalJsonContext.Default.JsonRpcResponse`, `SignalJsonContext.Default.JsonRpcNotificationRaw`,
all the other 27 source-gen-bound types). A malformed `{"jsonrpc":"2.0","id":"1","id":"2",...}`
response from a hypothetically-broken signal-cli (or from a malicious MITM on
the stdio channel) would silently follow last-wins semantics — exactly the
behavior CLAUDE.md rule #18 was written to prevent.

The fix is one-line: add `AllowDuplicateProperties = false` to the
`[JsonSourceGenerationOptions(...)]` attribute on `SignalJsonContext`.
[`JsonSourceGenerationOptionsAttribute.AllowDuplicateProperties`](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonsourcegenerationoptionsattribute.allowduplicateproperties)
is the source-gen-time equivalent that propagates the flag into the generated
parser. Verified via MS Learn (.NET 10 docs).

`audit v2.1 RG05` (`JsonSerializationTests.cs`) shipped two facts to pin this:

1. `SignalJsonOptions_AllowDuplicateProperties_IsFalse` — pinned the runtime flag.
2. `JsonDocumentOptions_AllowDuplicateProperties_False_ThrowsOnDuplicateKey` —
   proved the underlying .NET 10 API behaves as documented (using
   `JsonDocument.Parse` directly, independent of source-gen).

Neither asserts behavior through `SignalJsonContext.Default.*` because that path
silently swallows duplicates today. After this change, a third fact pinning the
source-gen-path becomes possible.

## What Changes

Single capability, one source edit + two test edits + a one-line CLAUDE.md
rule #18 update.

- **`json-hardening-source-gen`**:
  - `[JsonSourceGenerationOptions(...)]` on `SignalJsonContext` gains
    `AllowDuplicateProperties = false`.
  - `JsonSerializationTests` gains a third fact —
    `SignalJsonContext_AllowDuplicateProperties_ThrowsOnDuplicateKey` — that
    deserializes a dual-`id` JSON through `SignalJsonContext.Default.JsonRpcResponse`
    and asserts `JsonException`. (Today this would FAIL because the flag is
    bypassed; after the source-gen attribute is set, it passes.)
  - The two existing audit-v2.1 facts (`…_IsFalse` and `JsonDocumentOptions_…`)
    keep their current scope — they remain valid orthogonal guards.
  - CLAUDE.md rule #18 wording tightened: "**both** `SignalJson.Options.AllowDuplicateProperties`
    SHALL be `false` AND `SignalJsonContext` SHALL be annotated with
    `[JsonSourceGenerationOptions(AllowDuplicateProperties = false)]`" — to make
    the dual-site requirement explicit. The inline caveat block currently in
    `JsonSerializationTests.cs` ("КАВЕAT: source-gen Default fast-path…") is
    deleted — it stops being true.

No public API change. No new dependencies. No runtime behavior change for
well-formed signal-cli responses (signal-cli's Jackson serializer never emits
duplicate keys; this is purely defensive against protocol violation).

## Capabilities

### New Capabilities

- **`json-hardening-source-gen`**: `SignalJsonContext` (the production
  source-generated JSON resolver) SHALL reject duplicate JSON property names
  via the `[JsonSourceGenerationOptions(AllowDuplicateProperties = false)]`
  attribute. This SHALL be enforced for every type registered in the context,
  not just for reflection-based or `Metadata`-mode call-sites.

### Modified Capabilities

- **`json-hardening`** (originally from `audit-followup-2026`): the existing
  requirement that `SignalJson.Options.AllowDuplicateProperties = false` SHALL
  remain. The new source-gen-level guard is additive — both layers must hold
  so that even hypothetical `OptionsForTests`-via-reflection call-paths AND
  source-gen-via-`SignalJsonContext` call-paths fail-loud on duplicates.

## Out of scope

- **Adopting `JsonSerializerOptions.Strict` preset.** Strict implies
  `JsonUnmappedMemberHandling.Disallow`, which is incompatible with signal-cli's
  habit of adding new envelope fields between versions (forward-compat). This
  decision is already documented in CLAUDE.md rule #18 — unchanged.
- **Rejecting duplicate keys for `JsonRpcRequest` serialization output.** STJ
  source-gen `Serialize` never emits duplicate property names by construction
  (records have one property per name); this concern is read-side only.
- **A reflection-based regression guard that scans `[JsonSourceGenerationOptions]`
  attributes across the assembly to enforce the flag everywhere.** Today we have
  exactly one such attribute on exactly one type; a runtime guard is overkill.
  Re-evaluate if a second `[JsonSerializerContext]` appears in `src/SignalCli/**`.
