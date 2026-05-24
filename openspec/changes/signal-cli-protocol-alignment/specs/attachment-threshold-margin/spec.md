## ADDED Requirements

### Requirement: Inline-attachment threshold SHALL leave a 4M margin under Jackson's per-token limit

`SignalMessage.MaxInlineEncodedAttachmentBytes` SHALL be `12_000_000` (down from the previous `15_000_000`). Justification: signal-cli's Jackson 2.20.2 enforces `StreamReadConstraints.maxStringLength = 20_000_000` characters per string token. base64 encoding inflates raw bytes by 4/3, so 12M raw → 16M encoded, leaving 4M of margin for the surrounding `send` JSON envelope (recipient, message body, mentions, quote fields, sticker, etc.). The previous 15M value gave 20M encoded — exactly at Jackson's cap, with zero margin for envelope overhead — leading to occasional `StreamConstraintsException` on attachments near the threshold.

The downstream total-JSON-line check `json.Length > 20_000_000` in `JsonRpcClient.SendRequestAsync` remains unchanged. The two checks address different constraints: per-token (Jackson) and per-line (our defense against amplified small-fields-sum).

#### Scenario: An attachment one byte over the new boundary uses temp-file path
- **GIVEN** an attachment with raw size `MaxInlineEncodedAttachmentBytes * 3 / 4 + 1` bytes (one byte over)
- **WHEN** `SendAttachmentAsync` is called
- **THEN** the captured `parameters.Attachments[0]` is a filesystem path, not a `data:` URI

#### Scenario: A 14M raw attachment that would have been inline previously now goes to temp file
- **GIVEN** an attachment with raw size `14_000_000` bytes (under the old 15M threshold, over the new 12M threshold)
- **WHEN** `SendAttachmentAsync` is called
- **THEN** the captured `parameters.Attachments[0]` is a filesystem path
- **AND** the surrounding `send` JSON has 4M+ of headroom under Jackson's 20M per-token cap

### Requirement: The threshold value SHALL be documented with the Jackson-cap rationale

The `MaxInlineEncodedAttachmentBytes` constant SHALL carry an inline comment citing Jackson 2.20.2's `StreamReadConstraints.maxStringLength` default and the 4/3 base64 inflation factor. A future maintainer changing the value SHALL be able to reproduce the boundary math without reading external documentation.

#### Scenario: A maintainer can re-derive the threshold from the comment alone
- **GIVEN** the comment above `MaxInlineEncodedAttachmentBytes`
- **WHEN** a maintainer wants to verify the value
- **THEN** the comment lists: Jackson's per-token cap (20M), the base64 inflation factor (4/3), the desired margin for envelope overhead (4M), and the resulting raw-size threshold (12M = (20M - 4M) × 3/4)
