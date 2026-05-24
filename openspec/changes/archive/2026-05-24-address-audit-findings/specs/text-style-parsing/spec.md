## ADDED Requirements

### Requirement: Locale-independent style names
The text-style parser SHALL emit style names using invariant uppercasing so that the produced names are always `BOLD`, `ITALIC`, `MONOSPACE`, `STRIKETHROUGH`, and `SPOILER` regardless of the current thread culture.

#### Scenario: Turkish culture
- **WHEN** the current culture is `tr-TR` and the input contains `*italic*`
- **THEN** the emitted style range uses the name `ITALIC` (not `İTALİC`)

### Requirement: Escape handling for markers
The parser SHALL treat a backslash before a style marker as an escape that outputs the literal marker, and SHALL handle an escaped backslash (`\\`) as a literal backslash without consuming a following marker.

#### Scenario: Escaped marker
- **WHEN** the input contains `\*not italic\*`
- **THEN** the parsed text contains literal `*not italic*` and no italic range is produced

#### Scenario: Escaped backslash before marker
- **WHEN** the input contains `\\*italic*`
- **THEN** the parsed text begins with a literal backslash
- **AND** an italic range is produced for `italic`

### Requirement: Ranges use UTF-16 offsets
Style ranges SHALL be expressed as `start:length:STYLE` using UTF-16 code-unit offsets of the cleaned output text, consistent with signal-cli expectations.

#### Scenario: Range over ASCII text
- **WHEN** the input is `*hi*`
- **THEN** the produced range is `0:2:ITALIC` and the cleaned text is `hi`
