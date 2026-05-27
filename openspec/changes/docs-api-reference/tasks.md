# Tasks — docs-api-reference

## 0. Setup

- [x] 0.1 Запустити `npx -y @fission-ai/openspec@latest validate docs-api-reference --strict` — confirm green перед будь-якими source-edit'ами.
- [x] 0.2 Зафіксувати baseline: `wc -l README.md` (587 рядків), `find docs -name '*.md' | wc -l` (1 — лише `cloud-development.md`), `dotnet test Tests/SignalCli.Tests --filter "Category!=E2E"` (503 passed).

## 1. Capability `docs-api-reference` — content creation

- [x] 1.1 Read all 9 target interfaces + XML docs: `ISignalMessage`, `ISignalAccounts`, `ISignalDevices`, `ISignalGroups`, `ISignalContacts`, `ISignalEventService`, `ISignalResources`, `ISignalStickers`, `ISignalCliClient`. Confirm signatures match `Tests/SignalCli.Tests/RegressionGuards/SignalCli.public-api.txt`.
- [x] 1.2 Read builder shapes for canonical examples: `TextMessageOptions.Builder`, `ReactionOptions.Builder`, `ReceiptOptions.Builder`, `TypingOptions.Builder`, `RemoteDeleteOptions.Builder`, `SendPollCreateOptions.Builder`, `SendPinMessageOptions.Builder`, `SendAdminDeleteOptions.Builder`, `UpdateGroupOptions.Builder`, `UpdateContactOptions.Builder`, `UpdateProfileOptions.Builder`, `UpdateAccountOptions.Builder`, `GetAvatarOptions.Builder`.
- [x] 1.3 Create `docs/api/messaging.md` — `ISignalMessage` (14 send-методів). Кожен метод: signature → опис → spec-винятки (типізовані) → приклад → `<X>Command.java @ bda4e7fc` citation.
- [x] 1.4 Create `docs/api/accounts.md` — `ISignalAccounts` (13 методів). Highlight `EnableDestructiveOperations` gate для 8 методів.
- [x] 1.5 Create `docs/api/devices.md` — `ISignalDevices` (6 методів). Mental model: secondary linking (`Start/FinishLink`) vs primary management (`AddDevice/ListDevices/RemoveDevice/UpdateDevice`).
- [x] 1.6 Create `docs/api/groups.md` — `ISignalGroups` (4 методи). §F8 idempotent quit; §F13 OnlyRequested trinary; §F14 dual-mode create/update.
- [x] 1.7 Create `docs/api/contacts.md` — `ISignalContacts` (8 методів). XOR mutex для Trust*; §F18 avatar XOR; §F9 RemoveContactMode enum.
- [x] 1.8 Create `docs/api/events.md` — `ISignalEventService` (Subscribe/Unsubscribe + 17 event-kind'ів × 2 поверхні). Таблиця pairings; explanation коли яку поверхню обирати; DataMessage union-семантика (CLAUDE.md rule #4).
- [x] 1.9 Create `docs/api/resources-stickers.md` — `ISignalResources` (3) + `ISignalStickers` (3) + `ISignalCliClient.VersionAsync` + raw `InvokeMethodAsync<TRequest,TResponse>` з AOT-safe pattern (CLAUDE.md rule #15).
- [x] 1.10 Create `docs/api/di-options.md` — 4 extension'и (`AddSignalCli` × 2 + `AddSignalCliWithBundledRuntimeDefaults` + `AddSignalEvents`) + full `SignalCliOptions` reference (all properties з range/default constraints).
- [x] 1.11 Create `docs/examples/worker-auto-reply.md` — переміщений з README §369-457 content-preserving (за вийнятком final pointer-links section'у внизу).
- [x] 1.12 Create `docs/README.md` — індекс зі зведеною таблицею + convention для нових docs (cite RG09 invariant).

## 2. README.md trimming

- [x] 2.1 Edit `README.md` ToC (рядки 11-26): замінити рядок `- [Інтерфейси бібліотеки](...)` → `- [Документація API](#-документація-api)`; видалити рядок `- [Розширений приклад — worker з авто-відповіддю](...)`.
- [x] 2.2 Edit `README.md`: замінити розділ "🧩 Інтерфейси бібліотеки" (поточні рядки 181-275) на новий "📚 Документація API" зі зведеною таблицею-покажчиком до `docs/api/*.md` файлів + короткий `IRecipient` reminder + `SendMessageResponse`-shape-нота.
- [x] 2.3 Edit `README.md`: видалити розділ "📝 Розширений приклад — worker з авто-відповіддю" + "Device-link flow" (поточні рядки 369-457).
- [x] 2.4 Verify `wc -l README.md` показує ~ 416 рядків (down from 586).

## 3. Capability `docs-coverage-guard` — RG09 regression test

- [x] 3.1 Create `Tests/SignalCli.Tests/RegressionGuards/DocsApiCoverageTests.cs` за патерном `EventApiSymmetryTests` (RG06) + `ClaudeMdSplitConsistencyTests` (RG08, для `LocateRepoRoot()`-helper). Один `[Fact] EveryPublicApiMethod_IsMentionedInDocsApi` — reflectively enumerate'ить 9 target interfaces + 3 named extensions; substring-match по `docs/api/*.md` content.
- [x] 3.2 Filter: виключити `IsSpecialName` (property getter'и), inherited методи з `Microsoft.*` namespaces (`IHostedService.StartAsync` / `StopAsync` від `ISignalEventService`).
- [x] 3.3 `dotnet build Tests/SignalCli.Tests/SignalCli.Tests.csproj` — confirm CA1859 / xUnit1031 clean.
- [x] 3.4 `dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj --filter "FullyQualifiedName~DocsApiCoverageTests"` — passes (sanity).
- [x] 3.5 Edit `CLAUDE.md` "Regression guards" таблиця — додати рядок RG09 з file-path + опис.

## 4. Validation

- [x] 4.1 `dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj` — confirm 504 passed (503 + RG09).
- [x] 4.2 `wc -l README.md docs/api/*.md docs/examples/*.md docs/README.md` — report final sizes.
- [x] 4.3 Run full test suite incl. RG08 (`ClaudeMdSplitConsistencyTests`) — confirm root CLAUDE.md still ≤ 200 lines after RG09 row addition (mais додаємо ~ 1 рядок, тому safe).
- [x] 4.4 Verify negative-case (manual sanity): тимчасово sed-renamed `SendReactionAsync` → `SendReactionAsyncRENAMED` у `docs/api/messaging.md`, run RG09 → FAILED з `ISignalMessage.SendReactionAsync` у failure-list (after hardening match до `\b`-anchored regex; substring-match був занадто лояльним і пропускав rename'и). Restored.
- [x] 4.5 Re-run `npx -y @fission-ai/openspec@latest validate docs-api-reference --strict` — re-confirmed green.

## 5. Post-merge (out of this PR's scope, but documented)

- [ ] 5.1 After PR merges to main: archive change via `npx -y @fission-ai/openspec@latest archive docs-api-reference --yes --skip-specs`.
- [ ] 5.2 Update root CLAUDE.md "Implemented, merged, archived" list to add entry pointing to `openspec/changes/archive/<date>-docs-api-reference/`.
- [ ] 5.3 Bump `<SignalCliPackageVersion>` if shipping як окремий release (per CLAUDE.md "Version-CHANGELOG lockstep" rule — docs-only patch counts; consider 4.10.0). CHANGELOG entry per `.claude/rules/openspec-workflow.md § CHANGELOG voice template`.
