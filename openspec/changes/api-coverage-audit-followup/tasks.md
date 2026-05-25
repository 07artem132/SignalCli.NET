# Tasks — api-coverage-audit-followup (→ 4.10.0)

5 capabilities, ordered low-risk first. Each capability lands as its own commit per `.claude/rules/audit-debt.md` § Working style. Final commit bundles version bump + CHANGELOG + baseline regen.

## Capability 1 — `protocol-checklist-amend` (doc-only, lowest risk)

- [ ] 1.1 Edit `.claude/rules/signal-cli-protocol.md` — insert 8th pinned fact after the Java-25 bullet, citing `SendMessageResultUtils.java:60` + `JsonSendMessageResult.Type.IDENTITY_FAILURE`
- [ ] 1.2 Edit same file's footer paragraph — append "re-grep `\"admin\"` substring stability" instruction to the `<SignalCliVersion>`-bump checklist
- [ ] 1.3 Edit `.claude/rules/audit-debt.md` — add "§0.5 cite-and-read, not cite-and-trust" working-style bullet (the lesson from #1)
- [ ] 1.4 Commit: `docs(rules): pinned fact #8 + version-bump exception-substring stability`

## Capability 2 — `captcha-dispatch-test` (test-only)

- [ ] 2.1 Add `[Fact] InvokeMethodAsync_Code_Minus6_ThrowsCaptchaRequired` to `Tests/SignalCli.Tests/Exceptions/NewTypedRpcErrorsTests.cs` mirroring the GroupAdminRequired-dispatch test shape (positive path + assert `KnownCode == CaptchaRejected`)
- [ ] 2.2 `dotnet test --filter NewTypedRpcErrorsTests` — confirm new test green, no others broken
- [ ] 2.3 Commit: `test(rpc): captcha dispatch test — symmetry with GroupAdminRequired`

## Capability 3 — `event-dispatch-refactor` (internal refactor)

- [ ] 3.1 Add `DispatchUnionMember<TPayload, TArgs>(...)` private helper to `SignalEventService.cs` per design.md §Decision 3
- [ ] 3.2 Replace 13 dispatch branches in `OnNotificationReceived` (6 pre-existing: text/reaction/sticker/attachment/remoteDelete/quote + 7 Wave-7b: pollCreate/pollVote/pollTerminate/payment/pinMessage/unpinMessage/adminDelete) with helper calls
- [ ] 3.3 `dotnet build -p:TreatWarningsAsErrors=true` — green
- [ ] 3.4 `dotnet test` — all 503 unit tests green; `EventApiSymmetryWave7bTests` + presence-based dispatch suites pass; **no public-api baseline diff** (refactor is internal)
- [ ] 3.5 Commit: `refactor(events): collapse 13 dispatch branches via generic helper`

## Capability 4 — `json-payment-receipt-nullable` (1-line DTO change + tests)

- [ ] 4.1 Edit `src/SignalCli/Models/Signal/Envelope.cs:155` — `byte[] Receipt` → `byte[]? Receipt`
- [ ] 4.2 Update `JsonPayment` XMLDoc remarks — note nullable wire-contract reflecting upstream Java's no-NRT permissiveness
- [ ] 4.3 Check propagation: `PaymentEventArgs.Receipt` and any consumer-facing projections — update to `byte[]?` if needed
- [ ] 4.4 Extend `Tests/SignalCli.Tests/Serialization/ReceiveDecodersSerializationTests.cs` — add `JsonPayment_NullReceipt_DeserializesToNull` + `JsonPayment_MissingReceipt_DeserializesToNull` Facts
- [ ] 4.5 `dotnet build -p:TreatWarningsAsErrors=true` — fix any new CS8602 warnings in production paths if surfaced
- [ ] 4.6 Regenerate `Tests/SignalCli.Tests/RegressionGuards/SignalCli.public-api.txt` baseline
- [ ] 4.7 Commit: `fix(envelope): JsonPayment.Receipt nullable — honor upstream wire-contract`

## Capability 5 — `identity-changed-deprecation` (Obsolete shim + XMLDoc + cref removal)

- [ ] 5.1 Edit `src/SignalCli/Exceptions/IdentityChangedException.cs`:
  - Add `[Obsolete("Speculative type that is never dispatched — upstream signal-cli has no protocol-level distinction between first-contact-unknown and re-installed identity (both → UntrustedKeyErrorException \"Failed to send message due to untrusted identities\"). Catch UntrustedIdentityException instead and disambiguate via ISignalContacts.ListIdentitiesAsync if needed. Will be removed in 5.0.", DiagnosticId = "SIGNALCLI001")]`
  - Replace misleading XMLDoc `<remarks>` block with honest "never dispatched; deprecated shim; removed 5.0" wording, citing the new pinned fact #8 in `signal-cli-protocol.md`
- [ ] 5.2 Edit `src/SignalCli/Interfaces/Signal/ISignalMessage.cs:162` — remove `<exception cref="IdentityChangedException">` from `SendReactionAsync` (and any other ISignalMessage methods if present); leave `<exception cref="UntrustedIdentityException">` in place
- [ ] 5.3 Edit `src/SignalCli/Exceptions/UntrustedIdentityException.cs:16` — remove the misleading reference to "опт-ін різнення re-install"; replace with simple "sealed-removed для прийнятної consumer subtyping" note
- [ ] 5.4 Suppress `CS0618` (Obsolete usage) at the existing `IdentityChangedException` construction sites in `Tests/SignalCli.Tests/Exceptions/NewTypedRpcErrorsTests.cs:18,48` with `#pragma warning disable CS0618` + justification comment ("retained for hierarchy regression-guard during 4.x deprecation period")
- [ ] 5.5 `dotnet build -p:TreatWarningsAsErrors=true` — green; `ObsoleteMessageConsistencyTests` (R04) confirms "5.0" > current major 4
- [ ] 5.6 Regenerate `SignalCli.public-api.txt` baseline (Obsolete attribute may surface in baseline format)
- [ ] 5.7 Commit: `deprecate: IdentityChangedException — Obsolete shim, never dispatched, removed 5.0`

## Release commit (final)

- [ ] 6.1 Bump `<SignalCliPackageVersion>` in `Directory.Build.props`: `4.9.0` → `4.10.0`
- [ ] 6.2 Add `## [4.10.0] — YYYY-MM-DD` section to `CHANGELOG.md` per `.claude/rules/openspec-workflow.md` § CHANGELOG voice template:
  - Bold leading bullets, consumer-first voice
  - **Якщо ти catch'аєш `IdentityChangedException` — мігруй на `UntrustedIdentityException`.** Тип ніколи не диспатчився (upstream сам не розрізняє first-contact vs re-install); deprecated, видаляється у 5.0.
  - **Якщо ти читаєш `payment.Receipt.Length` — додай null-check.** Wire-контракт чесніший: `byte[]?`. Wave 7b shape (3 days old), realistic blast radius zero.
  - Refactor/test/doc-only items grouped under single "Internal hygiene" bullet
- [ ] 6.3 `dotnet build -p:TreatWarningsAsErrors=true` final pass; `dotnet test` full suite (503+ tests including new captcha-dispatch + 2 nullable-payment serialization tests)
- [ ] 6.4 `npx -y @fission-ai/openspec@latest validate api-coverage-audit-followup --strict`
- [ ] 6.5 Commit: `chore(release): 4.10.0 — api-coverage-audit-followup (5 capabilities)`
- [ ] 6.6 (Post-merge, separate workflow per `.claude/rules/openspec-workflow.md` § post-merge): archive change + update CLAUDE.md "Implemented, merged, archived" list

## Notes

- **No version bump until 6.1.** Every individual capability commit is non-version-changing — version moves only at the release commit so a partial-merge cannot create a half-versioned tree.
- **Test count delta:** +3 unit tests (1 captcha + 2 nullable-payment). 503 → 506.
- **Public-API baseline delta:** non-zero — `Obsolete` attribute on `IdentityChangedException` + `byte[]?` shift on `JsonPayment.Receipt`. Both expected; regenerate in 4.6 + 5.6.
