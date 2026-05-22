## ADDED Requirements

### Requirement: No private content in default logs
The library SHALL NOT log message bodies, recipient/sender phone numbers, or attachment payloads at any level enabled by default (`Information` and below in the documented configuration). Raw JSON-RPC request and response lines containing such content MUST only be logged at `Trace`.

#### Scenario: Sending a message at Information level
- **WHEN** the configured minimum log level is `Information` and a text message is sent
- **THEN** the emitted log entries contain the RPC method name and request id
- **AND** they do not contain the message body or recipient identifiers

#### Scenario: Attachment payload never logged verbatim
- **WHEN** an attachment is serialized to a data URI for sending
- **THEN** the base64 payload is never written to any log entry, including at `Trace`

### Requirement: Opt-in verbose diagnostics
Verbose diagnostic logging of raw RPC traffic SHALL be emitted only at `Trace`, and only when the consumer has explicitly enabled `Trace`. It MUST NOT be emitted at any higher level.

#### Scenario: Trace explicitly enabled
- **WHEN** the consumer sets the minimum level to `Trace`
- **THEN** raw RPC lines may be logged for debugging
