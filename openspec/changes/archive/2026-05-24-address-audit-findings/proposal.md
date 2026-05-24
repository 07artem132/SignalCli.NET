## Why

A code audit of SignalCli.NET found that — after the missing `MimeTypeHelper` and `TextStyleParser` files were restored (build now succeeds, 77/77 tests pass) — several correctness, privacy, and cross-platform defects remain. The most serious cause silent data loss (attachment events are swallowed), leak private message content into logs, and make the library unusable on Linux/macOS despite the documented cross-platform support.

## What Changes

- **Event dispatch**: a received message that carries both text and an attachment/reaction/sticker must raise *every* applicable observable, not just the first one. Today `SignalEventService` returns after the text branch, so captioned attachments never reach `Attachments` subscribers.
- **Logging privacy**: stop logging full RPC params/results and raw stdin/stdout lines at `Debug`/`Information`. Message bodies, phone numbers, and base64 attachment payloads must not appear in normal logs. **BREAKING** for anyone relying on current verbose log content.
- **Cross-platform startup**: `Config.ResolveJavaPath()` must resolve Java on Linux/macOS (currently throws on non-Windows), or the cross-platform claim must be removed. Wire up the unused `DefaultJavaPath = "java"` fallback.
- **Attachment handling**: prevent path traversal via attacker-influenced `FileName` in `AttachmentEntry.SaveToTempFile`/`ToDataUri`; clarify the inline-vs-tempfile size threshold and reconcile it with the documented 100 MB limit.
- **Process argument safety**: build signal-cli process arguments without unescaped string interpolation so a `"` in a configured path cannot break or inject arguments.
- **Text style parsing**: emit style names with `ToUpperInvariant()` (current `ToUpper()` corrupts `ITALIC`/`STRIKETHROUGH` under tr-TR), and fix escape handling for `\\` sequences.
- **Runtime acquisition (new `SignalCli.Runtime` package)**: the download scripts fetch `signal-cli` over HTTPS with **no checksum/signature verification** — a supply-chain risk for a security-sensitive Signal wrapper; add SHA-256 verification against the published hash. Also: the `.sh` uses `wget` (absent by default on macOS) and lacks `set -euo pipefail` so failures pass silently while still exiting 0; the MSBuild `DownloadSignalCli` target runs on every build (network dependency).
- **Housekeeping (non-spec)**: bump `Newtonsoft.Json` 13.0.1 → 13.0.3; use `Process.WaitForExitAsync`; fix nullable annotations driving the build warnings; align README/csproj/badge URLs and example API usage; pin GitHub Actions to commit SHAs (workflow grants `contents: write` and uses third-party actions on mutable tags).

## Capabilities

### New Capabilities
- `event-dispatch`: routing of incoming Signal envelopes to the correct reactive observables, including composite messages.
- `logging-privacy`: rules for what message data may be logged and at which level.
- `cross-platform-startup`: resolving the Java executable and launching signal-cli on Windows, Linux, and macOS.
- `attachment-handling`: safe conversion of outgoing attachments to data URIs or temp files.
- `process-argument-safety`: safe construction of the signal-cli command-line arguments.
- `text-style-parsing`: parsing markdown-style markers into Signal text-style ranges.
- `runtime-acquisition`: downloading and packaging the signal-cli runtime via the `SignalCli.Runtime` package and its MSBuild targets.

### Modified Capabilities
<!-- None: no specs exist yet in openspec/specs/. -->

## Impact

- Code: `Services/Signal/SignalEventService.cs`, `Services/Signal/SignalService.cs`, `Services/Rpc/JsonRpcClient.cs`, `Models/Config.cs`, `Services/FileSystem/AttachmentEntry.cs`, `Services/Signal/SignalMessage.cs`, `Utilities/TextStyleParser.cs`, `Utilities/MimeTypeHelper.cs`, `Services/SignalCli/ProcessWrapper.cs`.
- Packaging/docs: `SignalCli.csproj` (dependency + version), `README.md`, badge/repository URLs.
- Behavior: subscribers may receive additional events; verbose logs become quieter. No public API signature changes are required for the core fixes.
