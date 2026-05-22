## ADDED Requirements

### Requirement: Attachment file names cannot escape the temp directory
When persisting an outgoing attachment to a temporary file, the library SHALL derive the on-disk name with `Path.GetFileName` so that directory separators or `..` segments in the supplied name cannot write outside the per-call temporary directory.

#### Scenario: Traversal attempt in file name
- **WHEN** an attachment is created with a file name containing `../` or an absolute path
- **THEN** the temp file is written inside the generated GUID temp directory only
- **AND** no file is created outside that directory

### Requirement: Deterministic inline-vs-tempfile selection
The library SHALL select between inline data-URI encoding and temp-file handоff using a single named size threshold, and the behavior SHALL be consistent with the documented maximum attachment size.

#### Scenario: Small attachment
- **WHEN** an attachment's encoded size is below the configured inline threshold
- **THEN** it is sent as an inline data URI

#### Scenario: Large attachment
- **WHEN** an attachment's encoded size is at or above the inline threshold
- **THEN** it is written to a temp file and the file path is passed to signal-cli
- **AND** the temp file is deleted after the send attempt completes

### Requirement: Temp file cleanup on failure
The library SHALL delete any temp files it created for an attachment even when the send operation throws.

#### Scenario: Send fails
- **WHEN** sending an attachment via temp file throws an exception
- **THEN** the created temp file and its directory are removed before the exception propagates
