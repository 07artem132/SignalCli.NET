## ADDED Requirements

### Requirement: `JsonPayment.Receipt` SHALL be `byte[]?` (nullable)

`src/SignalCli/Models/Signal/Envelope.cs` SHALL declare:

```csharp
public record JsonPayment(
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("receipt")] byte[]? Receipt   // ← nullable
);
```

Rationale: upstream Java declares `record JsonPayment(String note, byte[] receipt)` — Java has no NRT contract, both fields can be `null` at runtime. STJ source-generation for C# reference-type properties does NOT enforce non-null assignment; a wire envelope with `"receipt": null` or missing-`receipt` deserializes to `null` assigned into the slot. Honoring this in the C# type system prevents consumer NRE when reading `.Length` on a malformed envelope.

`PaymentEventArgs.Receipt` (and any consumer-facing projection) SHALL propagate the same nullability.

#### Scenario: Wire envelope with `"receipt": null` deserializes to `JsonPayment` with null Receipt

- **GIVEN** wire JSON `{"note":"hi","receipt":null}` arrives
- **WHEN** `JsonSerializer.Deserialize(json, SignalJsonContext.Default.JsonPayment)` runs
- **THEN** the result is non-null `JsonPayment` instance
- **AND** `.Note == "hi"`
- **AND** `.Receipt is null`

#### Scenario: Wire envelope omitting `receipt` field deserializes to JsonPayment with null Receipt

- **GIVEN** wire JSON `{"note":"hi"}` arrives (no `receipt` key)
- **WHEN** deserialization runs through `SignalJsonContext.Default.JsonPayment`
- **THEN** `.Note == "hi"` and `.Receipt is null`

#### Scenario: Wire envelope with valid base64 receipt deserializes to JsonPayment with byte[] payload

- **GIVEN** wire JSON `{"note":"Thanks!","receipt":"dGVzdC1ieXRlcw=="}`
- **WHEN** deserialization runs
- **THEN** `.Note == "Thanks!"` and `Encoding.UTF8.GetString(p.Receipt!) == "test-bytes"`

### Requirement: Existing happy-path serialization test SHALL continue to pass

`Tests/SignalCli.Tests/Serialization/ReceiveDecodersSerializationTests.JsonPayment_NewShape_HasNoteAndReceipt` SHALL remain green — the existing fixture asserts non-null Receipt access; nullability change is widening, not narrowing.

#### Scenario: Pre-existing receipt deserialization test still asserts correct bytes

- **WHEN** the existing `JsonPayment_NewShape_HasNoteAndReceipt` test runs against the updated nullable shape
- **THEN** assertion `Encoding.UTF8.GetString(p.Receipt) == "test-bytes"` passes (compiler may require `Receipt!` null-forgiveness suffix; non-functional change)
