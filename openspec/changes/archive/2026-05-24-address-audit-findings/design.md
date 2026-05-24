## Context

SignalCli.NET wraps `signal-cli` (a Java app) over JSON-RPC on stdin/stdout, with a hosted-service pipeline: `SignalCliHostedService` launches the process and exposes a `StreamPair`; `JsonRpcClient` reads notifications and correlates request IDs; `SignalEventService` fans notifications out to `System.Reactive` subjects; `SignalMessage` builds `send` requests. The build is green and 77 unit tests pass, so the remaining issues are behavioral/quality rather than blockers. Constraints: keep the public API stable where possible, keep Newtonsoft.Json (a System.Text.Json migration is a separate effort), and preserve the existing DI registration shape.

## Goals / Non-Goals

**Goals:**
- No silent data loss in the reactive event stream for composite messages.
- No private message content (bodies, numbers, attachment bytes) in logs at default levels.
- The library starts on Linux/macOS or stops claiming to.
- Remove the path-traversal and argument-injection footguns.
- Make text-style output locale-independent.

**Non-Goals:**
- Migrating to System.Text.Json.
- Implementing the many unimplemented signal-cli verbs listed in the README.
- Redesigning the restart/health-monitor state machine (it is sound).

## Decisions

- **Composite event dispatch**: in `SignalEventService.OnNotificationReceived`, replace the `if (...) { ...; return; }` chain inside the `DataMessage` block with independent `if` checks (no early `return`), so a message with both a caption and an attachment raises both `TextMessages` and `Attachments`. Reaction/sticker/attachment remain independent emissions. Alternative considered: a single composite event type — rejected as a larger breaking API change.
- **Logging**: downgrade raw RPC line / `{@Params}` / `{@Result}` logging to `Trace`, and redact attachment data-URIs and message bodies even at `Trace`. Introduce a small helper that logs method name + request id only at `Debug`. Rationale: a messenger library must default to privacy; opt-in verbosity is acceptable but should never be the README's recommended config.
- **Java resolution**: extend `ResolveJavaPath()` with a non-Windows branch that checks `JAVA_HOME/bin/java` then falls back to the existing `DefaultJavaPath = "java"` (resolved via PATH). Alternative: require the caller to always set `JavaExecutable` — rejected because `CreateDefault()` is part of the documented happy path.
- **Argument safety**: keep `ProcessStartInfo.Arguments` for now but route every interpolated path through a quote-escaping helper (double internal quotes / reject embedded quotes), or migrate to `ProcessStartInfo.ArgumentList` which avoids manual quoting entirely. Prefer `ArgumentList` as the cleaner long-term fix.
- **Attachment paths**: sanitize `FileName` with `Path.GetFileName()` before composing temp paths and the data-URI `filename=` field, so `../` and separators cannot escape the per-call GUID temp directory. Clarify the size branch: pick one threshold, name it as a constant, and align messaging with the documented limit.
- **Text styles**: switch `tokenType.ToString().ToUpper()` to `ToUpperInvariant()`; add a unit test under tr-TR culture. Tidy the `\\` escape edge case.

## Risks / Trade-offs

- Emitting extra events for composite messages → subscribers that assumed mutual exclusivity may double-handle a message. Mitigation: document the change; it matches Signal's actual data model.
- Quieter logs → harder field debugging. Mitigation: keep an explicit opt-in `Trace` path and document it.
- `ArgumentList` migration changes how args reach the JVM on edge cases. Mitigation: cover with a ProcessConfig unit test asserting the produced argument vector.
- Java PATH fallback may pick an unexpected JRE. Mitigation: prefer `JAVA_HOME` first, log the resolved path.
