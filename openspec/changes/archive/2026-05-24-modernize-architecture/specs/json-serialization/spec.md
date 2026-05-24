## ADDED Requirements

### Requirement: System.Text.Json is the only serializer
The library SHALL serialize and deserialize all JSON-RPC traffic and Signal models with System.Text.Json. The public API and package SHALL NOT depend on Newtonsoft.Json.

#### Scenario: No Newtonsoft dependency
- **WHEN** the package is built
- **THEN** it does not reference Newtonsoft.Json

### Requirement: Source-generated serialization context
A source-generated `JsonSerializerContext` (metadata mode) SHALL provide serialization metadata for all RPC, model, and event types, and SHALL be used by the serializer instead of runtime reflection.

#### Scenario: Type resolved via context
- **WHEN** an RPC response or notification type is (de)serialized
- **THEN** it is resolved through the source-generated context

### Requirement: Protocol round-trips losslessly
Requests, responses, error objects, and `receive` notifications SHALL round-trip correctly per the signal-cli JSON-RPC protocol: camelCase property names, case-insensitive deserialization, null values omitted on write, and the sync-message enum read/written as its string name.

#### Scenario: Outgoing request shape
- **WHEN** a `send` request is serialized
- **THEN** it produces `{"jsonrpc":"2.0","method":"send","id":...,"params":{...}}` with camelCase params and null fields omitted

#### Scenario: Incoming notification deserialization
- **WHEN** a `receive` notification with an `envelope` containing a `dataMessage` is received
- **THEN** it deserializes into the envelope model with the data message populated

#### Scenario: Composite envelope preserved
- **WHEN** an envelope contains both a message body and attachments
- **THEN** both are present after deserialization (presence-based union, no data loss)

#### Scenario: Sync message enum as string
- **WHEN** a sync message with type `CONTACTS_SYNC` is deserialized
- **THEN** the enum value is parsed from its string name
