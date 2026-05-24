## ADDED Requirements

### Requirement: SignalJsonContext SHALL reject duplicate JSON properties at source-gen level

`SignalJsonContext` SHALL be annotated with `[JsonSourceGenerationOptions(AllowDuplicateProperties = false)]` so that every `SignalJsonContext.Default.<TypeName>` call-site (the production code path used by `JsonRpcClient.ProcessMessageAsync`) rejects duplicate JSON property names with `JsonException` instead of silently following last-wins semantics.

This requirement is **additive** to the existing `json-hardening` capability from `audit-followup-2026`: the runtime `SignalJson.Options.AllowDuplicateProperties = false` flag remains in place. Both layers are required because they cover orthogonal code paths — the runtime flag covers reflection-based call-sites (e.g. `OptionsForTests`), the source-gen attribute covers every `SignalJsonContext.Default.X` call-site.

#### Scenario: Duplicate `"id"` key fails fast through SignalJsonContext
- **GIVEN** the input `{"id":"1","id":"2","jsonrpc":"2.0"}`
- **WHEN** `JsonSerializer.Deserialize(input, SignalJsonContext.Default.JsonRpcResponse)` is called
- **THEN** a `JsonException` is thrown
- **AND** the exception originates from the source-generated parser
  (NOT from any reflection-based fallback)

#### Scenario: Both runtime flag AND source-gen attribute SHALL be enforceable
- **GIVEN** the production assembly `SignalCli.dll`
- **WHEN** `SignalJson.Options.AllowDuplicateProperties` is read
- **THEN** it returns `false`
- **AND WHEN** the `[JsonSourceGenerationOptions]` attribute on `SignalJsonContext` is reflected
- **THEN** its `AllowDuplicateProperties` named-arg is `false` (or the attribute carries the constructor-level default with the property explicitly set to `false`)

#### Scenario: A well-formed signal-cli response continues to deserialize successfully
- **GIVEN** the input `{"jsonrpc":"2.0","id":"1","result":{"version":"0.14.3"}}`
- **WHEN** `JsonSerializer.Deserialize(input, SignalJsonContext.Default.JsonRpcResponse)` is called
- **THEN** no exception is thrown
- **AND** the resulting `JsonRpcResponse.Id` equals `"1"`
- **AND** `JsonRpcResponse.Result` contains the version field

Regression-guard tests live in `Tests/SignalCli.Tests/JsonSerializationTests.cs` (RG05 block):

- `SignalJsonOptions_AllowDuplicateProperties_IsFalse` (audit v2.1 — runtime flag).
- `JsonDocumentOptions_AllowDuplicateProperties_False_ThrowsOnDuplicateKey` (audit v2.1 — .NET 10 API proof).
- `SignalJsonContext_AllowDuplicateProperties_ThrowsOnDuplicateKey` (this change — source-gen path).
