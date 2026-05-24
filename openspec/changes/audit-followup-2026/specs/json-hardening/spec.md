## ADDED Requirements

### Requirement: Production JsonSerializerOptions SHALL reject duplicate JSON properties

`SignalJson.Options` (the production-path `JsonSerializerOptions` instance used by `JsonRpcClient`, `SignalEventService`, and any other type that calls `JsonSerializer.Serialize`/`Deserialize` from `src/SignalCli/**`) SHALL set `AllowDuplicateProperties = false` (new in .NET 10).

A JSON payload that declares the same property name twice SHALL fail deserialization with `JsonException`, not silently follow last-wins semantics.

#### Scenario: Duplicate `"jsonrpc"` key fails fast
- **GIVEN** the input `{"jsonrpc":"2.0","jsonrpc":"X","id":"1","result":{}}`
- **WHEN** `JsonSerializer.Deserialize<JsonRpcResponse>(input, SignalJson.Options)` is called
- **THEN** a `JsonException` is thrown
- **AND** the exception message indicates a duplicate property

#### Scenario: Test-only OptionsForTests SHALL also reject duplicates
- **GIVEN** `SignalJson.OptionsForTests` is the test-only `JsonSerializerOptions` with reflection fallback
- **WHEN** a test deserializes a payload with duplicate keys through that instance
- **THEN** `JsonException` is thrown — the test surface respects the same hardening
