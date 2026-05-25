---
paths:
  - "src/SignalCli/Services/**"
  - "src/SignalCli.runtime/**"
  - "src/SignalCli.runtime.native/**"
  - "src/SignalCli.runtime.jre.*/**"
---

# signal-cli protocol behavior we depend on

These are facts about the *upstream* signal-cli Java app that our wrapper relies on. Each is cited
to a specific signal-cli source file at commit `bda4e7f` (after 0.14.3). Re-verify against newer
signal-cli releases when bumping the pinned version in `SignalCli.runtime.csproj`.

- **Graceful shutdown trigger = stdin EOF or SIGTERM/SIGINT.** signal-cli has no `exit` JSON-RPC
  method and does not read literal text on stdin — every stdin line is parsed as JSON
  (`JsonRpcReader.java:59-75`). Our wrapper closes stdin (`StandardInput.Close()`) in
  `StopProcessInternalAsyncNoLock`; signal-cli's reader-loop terminates naturally on EOF, its
  dispatcher's finally-block clears subscriptions (`SignalJsonRpcDispatcherHandler.java:212-214`),
  and the JVM shuts down cleanly. Signal handlers (SIGINT/SIGTERM via `sun.misc.Signal` —
  `Shutdown.java:24-25`) are the second valid trigger but Windows has no POSIX signals, so we
  prefer stdin-close as the cross-platform path. **Critical rule:** never re-add
  `WriteLineAsync("exit")` — literal "exit" produces `-32700 Parse error` and the process keeps
  running. See `signal-cli-protocol-alignment` capability `graceful-shutdown-fix` for history.

- **Stdout = pure JSON-RPC, line-flushed.** signal-cli's `JsonWriterImpl.write` calls
  `writer.flush()` after every JSON line (`JsonWriterImpl.java:30`), so our `ReadLineAsync` loop
  observes each message promptly even though Java's default for non-TTY stdout is block-buffered.
  signal-cli never emits banner/version/log output on stdout — all diagnostics go to stderr via
  SLF4J/Logback. The `UnknownMessage` log line in our `ProcessMessageAsync` should fire
  approximately never in practice; if it does, suspect protocol drift in a newer signal-cli release.

- **Parallel request processing → match by `id`, not by order.** signal-cli's `JsonRpcReader`
  uses `Executors.newVirtualThreadPerTaskExecutor()` to handle requests
  (`JsonRpcReader.java:58`). Response arrival order is non-deterministic — multiple in-flight
  requests are dispatched to virtual threads that complete in execution-time order, not request
  order. Our `JsonRpcClient._pendingRequests : ConcurrentDictionary<string, TaskCompletionSource>`
  keyed by request `id` is mandatory; never refactor to a queue or order-based correlation.

- **`subscribeReceive` is NOT idempotent at the protocol level.** signal-cli returns a fresh ID
  via `AtomicInteger.getAndIncrement()` for every call
  (`SignalJsonRpcDispatcherHandler.java:143`). Our idempotency lives entirely in
  `SignalEventService._pendingSubscribes` (reservation TCS pattern). If our code path ever
  bypasses the reservation, signal-cli delivers duplicate `receive` notifications for each
  subscription ID — and unsubscribing one ID leaves the others active.

- **Jackson `maxStringLength = 20_000_000` PER STRING TOKEN.** signal-cli uses Jackson 2.20.2
  (`gradle/libs.versions.toml:10`) with `StreamReadConstraints` defaults — does NOT override
  `maxStringLength` (Util.java:51-56 creates the ObjectMapper minimally). Our
  `MaxInlineEncodedAttachmentBytes = 12_000_000` (after `attachment-threshold-margin`) keeps the
  base64-encoded attachment string ≤ 16M with 4M of margin for the rest of the `send` request.
  Total-JSON-line length is also checked in `JsonRpcClient.SendRequestAsync` against 20M (a
  separate, looser check) — both are needed because the constraints address different limits
  (per-token vs per-line).

- **Error codes outside JSON-RPC 2.0 standard.** signal-cli emits these in addition to
  `-32600..-32603` and `-32700` (`SignalJsonRpcCommandHandler.java:35-280`):
  - `-1` `UserError` (bad input, invalid number)
  - `-3` `IoError` (file system / network)
  - `-4` `UntrustedIdentity` (key verification failure) — surfaced as `UntrustedIdentityException`
  - `-5` `RateLimit` (server throttle) — surfaced as `RateLimitException`
  - `-6` `CaptchaRejected`
  All errors are sent on **stdout** (same channel as success responses), never stderr. The
  typed surface is `SignalCli.Exceptions.JsonRpcErrorCode` enum + `JsonRpcException.KnownCode`
  property; `RateLimitException` and `UntrustedIdentityException` are the two derived types
  for high-leverage codes.

- **Java 25 requirement.** signal-cli 0.14.0+ requires JDK 25 (`build.gradle.kts:7-8`).
  `signal-cli 0.14.3` (our pinned version in `SignalCli.runtime.csproj`) is the first 0.14.x.
  Bumping signal-cli later than 0.14.x without bumping JDK fails at JVM startup with
  `UnsupportedClassVersionError`. The bundled-JRE packages
  (`SignalCli.Runtime.Jre.{win-x64,osx-arm64}`) pin Temurin 25 SHA-256 in their csproj.

**When bumping `<SignalCliVersion>` in `SignalCli.runtime.csproj`:** re-verify each of the
seven facts above against the new signal-cli source. The PR description SHALL include a one-line
confirmation that these facts were re-verified, even if zero edits resulted. Discrepancies SHALL
be resolved either by adapting the wrapper or by updating this section + the commit citation.
