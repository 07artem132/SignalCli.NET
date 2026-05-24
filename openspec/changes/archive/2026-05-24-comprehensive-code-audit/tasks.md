## 0. Setup

- [x] 0.1 Inventory all `src/SignalCli/**` and `Tests/**` `.cs` files; confirm the subsystem decomposition (S1–S7) covers every file
- [x] 0.2 Pull the relevant Microsoft Learn pages via the Microsoft Docs MCP (async/cancellation, async-coordination-primitives, complete-your-tasks, common-async-bugs, System.Threading.Lock/IDE0330, Process redirect)

## 1. S1 — Process lifecycle

- [x] 1.1 Redirected stdout/stderr read without deadlock — OK (separate tasks); but reader loops uncancellable + stderr no try/catch (F4)
- [x] 1.2 `StartAsync` blocking — reviewed
- [x] 1.3 Process kill / process-group / graceful timeout — graceful stop burns full timeout (F9)
- [x] 1.4 Disposal / zombie — double-dispose of process stream (F4); Dispose hard-kills
- [x] 1.5 Cancellation / no async void — **`OnProcessExited` is async void + restarts outside the lock (F2)**

## 2. S2 — State & health

- [x] 2.1 State transitions race-free — `UpdateState` OnNext outside lock + unsynchronized Dispose (F25)
- [x] 2.2 Health-monitor loop — catches/continues; restart-budget never resets (F3)
- [x] 2.3 Subjects/timers disposed — reviewed

## 3. S3 — RPC transport

- [x] 3.1 Pending-request map — **no request timeout → hang (F1)**; id-overwrite theoretical (downgraded)
- [x] 3.2 `TaskCompletionSource` RunContinuationsAsynchronously — **correct (non-issue)**
- [x] 3.3 Reader loop one-bad-line resilience — OK; loops uncancellable (F4)
- [x] 3.4 Request timeout/disposal — missing (F1)

## 4. S4 — Serialization

- [x] 4.1 Source-gen coverage + reflection fallback — reviewed (F20)
- [x] 4.2 `JsonDocument`/`JsonElement` lifetime — **correct, STJ clones on deserialize (non-issue)**
- [x] 4.3 Deserialization hardening — read-path lacks explicit MaxDepth (minor)
- [x] 4.4 Nullable on DTOs — `Error`/`JsonRpc`/`Data` (F20)

## 5. S5 — Signal API

- [x] 5.1 Argument validation / exception types — missing ThrowIfNull (F12); wrong paramName (F24); dead branch (F8)
- [x] 5.2 Attachment temp-file lifecycle — **cleanup correct (non-issue)**; threshold math minor
- [x] 5.3 Event dispatch complete + thread-safe — **dispatch correct**; StartAsync not idempotent (F17); quote/edit dropped (F13)
- [x] 5.4 No private content above Trace — **violated in ListAccounts/ListGroups/SyncAccount (F5)**

## 6. S6 — Config & runtime

- [x] 6.1 Java/native/JRE resolution — required paths nullable (F10); defaults ignore AppHome (F11)
- [x] 6.2 Download scripts — SHA-pinned/ASCII/non-ASCII-path-safe (verified clean this session)
- [x] 6.3 Packaging zip-based — verified clean this session

## 7. S7 — Utilities & FS

- [x] 7.1 `TextStyleParser` — drops unmatched markers; verify offset units (F18)
- [x] 7.2 `MimeTypeHelper` — ISO-BMFF over-broad; stream under-read (F16)
- [x] 7.3 `AttachmentEntry` — **path traversal sanitized (non-issue)**; ctor null-guards (minor)

## 8. Cross-cutting

- [x] 8.1 Async/cancellation sweep — **clean** except F1/F2/F19
- [x] 8.2 IDisposable/IAsyncDisposable — F4 (no IAsyncDisposable on JsonRpcClient), F25
- [x] 8.3 DI lifetimes — no captive deps; double-registration ungated (F23)
- [x] 8.4 Logging — privacy violations F5; LoggerMessage/CA1848 deliberately suppressed (acceptable)
- [x] 8.5 Security — arg injection / traversal **mitigated (non-issue)**

## 9. Tests & integration

- [x] 9.1 Classify tests (real vs tautological) — ~110-120 real / ~25-35 weak (F15)
- [x] 9.2 Coverage / risky paths — gaps noted (F1/F4/F13)
- [x] 9.3 Integration-test gap documented — **no real signal-cli E2E (F7)**
- [x] 9.4 Concrete bundled-JRE E2E test proposed

## 10. Documentation quality

- [x] 10.1 XML-doc completeness — gaps + CS1591 suppressed (F21)
- [x] 10.2 README accuracy — **non-compiling snippets (F6)**
- [x] 10.3 CLAUDE.md / copilot-instructions — copilot stale BOM claim (F22)

## 11. Synthesis & validation

- [x] 11.1 Verify every High/Critical by reading cited lines (F1,F2,F3,F4,F5,F8 verified in-source)
- [x] 11.2 Write `AUDIT-FINDINGS.md` with Microsoft-Learn citations
- [x] 11.3 Mirror accepted findings as a follow-up remediation change skeleton (`address-audit-findings-2`)
- [x] 11.4 `openspec validate comprehensive-code-audit --strict` passes
