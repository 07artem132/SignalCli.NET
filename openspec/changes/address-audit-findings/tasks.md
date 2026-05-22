## 1. Event dispatch (high)

- [x] 1.1 In `SignalEventService.OnNotificationReceived`, replace the early-`return` chain in the `DataMessage` block with independent `if` checks so text + attachment/reaction/sticker all emit
- [x] 1.2 Add a unit test: envelope with non-empty `Message` and non-empty `Attachments` emits on both `TextMessages` and `Attachments`
- [x] 1.3 Add a unit test: envelope with `Reaction` and empty `Message` emits only on `Reaction`

## 2. Logging privacy (high)

- [x] 2.1 In `SignalService`, remove `{@Params}`/`{@Result}` from `Information`/`Debug` logs; log method only
- [x] 2.2 In `JsonRpcClient`, downgrade raw stdin/stdout line logging to `Trace`
- [x] 2.3 Ensure attachment data-URIs / message bodies are never written above `Trace`
- [x] 2.4 Update README example to stop recommending `LogLevel.Trace` + console as the default
- [x] 2.5 Update the JsonRpcClient send test to assert the raw JSON logs at `Trace` (not `Debug`)

## 3. Cross-platform startup (high)

- [x] 3.1 Add a non-Windows branch to `Config.ResolveJavaPath()` checking `JAVA_HOME/bin/java` then `java` on PATH (uses the `DefaultJavaPath` const) + a shared `ResolveOnPath` helper
- [x] 3.2 Make the "no Java found" exception name the platform and the variables checked
- [x] 3.3 Made `ResolveOnPath` internal and added tests for PATH resolution (found / not-found) — covers the cross-platform PATH-scanning core of Java resolution
- [x] 3.4 Reconcile README platform-support claims with actual behavior

## 4. Attachment handling (medium)

- [x] 4.1 Sanitize `FileName` with `Path.GetFileName` in `AttachmentEntry.SaveToTempFile` and the `ToDataUri` `filename=` field (via `SafeFileName`)
- [x] 4.2 Extract the inline-vs-tempfile size threshold into a named constant (`MaxInlineEncodedAttachmentBytes`)
- [x] 4.3 Add a unit test proving a traversal file name stays inside the GUID temp directory
- [ ] 4.4 Add a test for temp-file cleanup on send failure (finally-block cleanup already present in `SendUnifiedMessageAsync`)

## 5. Process argument safety (medium)

- [x] 5.1 Migrate `Config.ToProcessConfig()` / `ProcessRunner` to `ProcessStartInfo.ArgumentList` (added `ProcessConfig.ArgumentList`)
- [x] 5.2 Add a unit test asserting paths with spaces stay as single arguments

## 6. Text style parsing (medium)

- [x] 6.1 Replace `ToUpper()` with `ToUpperInvariant()` in `TextStyleParser.HandleToken`
- [x] 6.2 Fix the `\\` escaped-backslash edge case (rewrote `Parse`, removed buggy `prevChar` logic)
- [x] 6.3 Add unit tests under `tr-TR` culture and for the escape scenarios

## 7. Housekeeping (low)

- [x] 7.1 Bump `Newtonsoft.Json` 13.0.1 → 13.0.3 in `SignalCli.csproj`
- [x] 7.2 Replace `ProcessWrapper.WaitForExitAsync` polling with `Process.WaitForExitAsync(ct)`
- [~] 7.3 Fix nullable annotations: `MimeTypeHelper` `string? fileName` and `AttachmentEntry.FilePath` done; bulk model `CS8618` warnings on DTO records deferred (would touch many records / affect deserialization)
- [~] 7.4 README example API fixed (`AttachmentEntry` ctor, `Reaction.IsRemove`, Builder-based send); repository/badge URL alignment deferred (publishing org URLs may be intentional)

## 8. Runtime acquisition (high — supply chain) + bump to v0.14.3

- [x] 8.0 Bump signal-cli to v0.14.3 in both scripts + `SignalCli.runtime.csproj` version `0.14.3.1`
- [x] 8.1 Add SHA-256 verification in `download-signal-cli.ps1` (pinned `60a0a513…26c6f`; abort on mismatch)
- [x] 8.2 Add the same SHA-256 verification in `download-signal-cli.sh`
- [x] 8.3 Add `set -euo pipefail` + error checks to `download-signal-cli.sh`
- [x] 8.4 Use `curl` (fallback `wget`) for macOS compatibility; `sha256sum`/`shasum` detection
- [x] 8.5 Gate the MSBuild `DownloadSignalCli` target with `Condition` so it is skipped when the runtime is present
- [~] 8.6 Bumped `actions/cache@v3 → v4`; full SHA-pinning of third-party actions deferred (CI-only hardening)

## 9. Verification

- [x] 9.1 `dotnet build SignalCli.sln` succeeds, 0 errors (3 pre-existing Moq test warnings)
- [x] 9.2 `dotnet test` passes: 89/89 (77 original + 12 new)
- [x] 9.3 `openspec validate address-audit-findings` passes
