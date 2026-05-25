# Changelog

Формат заснований на [Keep a Changelog](https://keepachangelog.com/),
проєкт дотримується [семантичного версіонування](https://semver.org/lang/uk/).

## [4.2.0] — 2026-05-25

Minor-реліз. **Якщо тобі треба з .NET керувати членством у Signal-групах — приєднуватися, оновлювати, виходити — тепер це нативно у бібліотеці.** 3 нові методи на `ISignalGroups`. Backward-compatible — `ListGroupsAsync` без змін.

### ✨ Нове

- **`ISignalGroups` отримав 3 нові методи: `JoinGroupAsync`, `UpdateGroupAsync`, `QuitGroupAsync`.** Wire-shape pinned до signal-cli source @ `bda4e7fc` (читали Java records напряму, §0.5 anti-hallucination protocol).
  ```csharp
  // Join за invitation-посиланням
  var join = await signalGroups.JoinGroupAsync("+380...", "https://signal.group/#CjQK...");
  if (join.OnlyRequested == true) { /* pending admin approval */ }

  // Create-OR-update (§F14 dual-mode — те саме API)
  var update = await signalGroups.UpdateGroupAsync(
      new UpdateGroupOptions.Builder("+380...")
          .WithGroupId("base64==")    // omit → CREATE-then-update
          .WithName("New name")
          .WithMembers(["+380999..."])
          .WithLinkState(GroupLinkState.EnabledWithApproval)
          .WithPermissionAddMember(GroupPermission.OnlyAdmins)
          .Build());
  if (update.GroupId is { } newId) { /* щойно створили групу newId */ }

  // Idempotent quit (§F8): якщо вже не member — success-no-op без винятку
  var quit = await signalGroups.QuitGroupAsync("+380...", "base64==", deleteLocally: true);
  if (quit.WasAlreadyNotMember) { /* нічого не робили — це OK */ }
  ```

- **2 нові wire-enum типи: `GroupLinkState`** (Enabled / EnabledWithApproval / Disabled) **і `GroupPermission`** (EveryMember / OnlyAdmins). Mapping .NET PascalCase → kebab-case wire string відбувається у сервісі (`SignalGroups.ToWireLinkState`/`ToWirePermission`); DTO передає `string?` поля що читаються upstream'ом argparse'ом.

### 🛠 Інше

- **§F14 dual-mode `UpdateGroupAsync`.** Один і той самий метод — і create і update. Якщо `Options.GroupId == null` — upstream викликає `createGroup(name, members, avatar)` ПЕРШИМ, далі `updateGroup(...)` з рештою полів. У відповіді `UpdateGroupResponse.GroupId` присутній ТІЛЬКИ на create-path (іначе `null`). XMLDoc на методі чітко документує цю двозначність — consumer бачить її у IDE.

- **§F8 idempotent `QuitGroupAsync` per CLAUDE.md rule #14.** Upstream silently catches `NotAGroupMemberException` і повертає `{}`. У .NET це деsеріалізується як `QuitGroupResponse` з обома полями `null`, а зручний `WasAlreadyNotMember` property повертає `true`. Consumer не отримує винятку — `quit на групі де ти не member` — це OK.

- **§F13 dimorphic `JoinGroupResponse.OnlyRequested: bool?`.** `null` = direct join (full member), `true` = pending admin approval. Wire ніколи не повертає `false` — це специфіка upstream `Map.of(...)` branches у `JoinGroupCommand.java:61-76`. Тип nullable обов'язково — `bool` тут було б брехнею.

- **`QuitGroupAsync` параметр `delete` перейменовано на `deleteLocally`** (CA1716 — `delete` collides з C++ reserved keyword, погіршує consumer interop). Wire-field залишається `"delete"` через `[JsonPropertyName]`.

### 🛡️ Захист від регресій

- **`GroupsCrudSerializationTests` — 9 тестів** пінять wire-shape: camelCase param fields (`groupId`/`removeMembers`/`setPermissionAddMember`), kebab-case enum values на wire (`"enabled-with-approval"`/`"only-admins"`), dimorphic `OnlyRequested`-absent-not-false, dimorphic `GroupId`-present-only-on-create, idempotent `{}` deserializes у QuitGroupResponse без помилки.

- **`SignalGroupsCrudTests` — 9 service-level тестів** з Moq: parameters mapping, enum→kebab пер LinkState/PermissionAddMember, §F14 create vs update path, §F8 idempotent NotAGroupMember не кидає виняток (CLAUDE.md rule #14), arg validation.

- **`UpdateGroupOptionsTests` — 5 builder тестів:** happy path, no-groupId-is-valid (create-mode), required-account, negative-expiration rejected, expiration-zero-allowed.

### 📊 Тестова статистика

- Unit: 333 → **358** (+25 тести).
- Integration: 8 (без змін; усі mock-only за wave-2 ризик-планом).
- Public API baseline: 1239 → ~1380 lines (+enums, +Options-builder, +3 method signatures).
- Build на src/ + Tests/: 0 warnings, 0 errors з `TreatWarningsAsErrors=true`.

---

## [4.1.0] — 2026-05-25

Minor-реліз. **Якщо твій бот колись хотів зробити "👍 read + typing-indicator + remote-delete" без shell'у в signal-cli CLI — тепер це нативно у бібліотеці.** 4 нові send-методи на `ISignalMessage` + 3 typed exceptions для consumer-actionable error-codes. Backward-compatible — жодних змін до існуючих send-методів, тестів чи DI-композиції.

### ✨ Нове

- **`ISignalMessage` отримав 4 нові методи: `SendReactionAsync`, `SendReceiptAsync`, `SendTypingAsync`, `SendRemoteDeleteAsync`.** Кожен — тонкий typed wrapper навколо відповідного signal-cli RPC (`sendReaction` / `sendReceipt` / `sendTyping` / `remoteDelete`). Builder-style options:
  ```csharp
  await signalMessage.SendReactionAsync(
      new ReactionOptions.Builder("+380...", "👍", targetTimestamp: 1717181920000L)
          .WithRecipients(["+380999..."])
          .WithNotifySelf()
          .Build());
  ```
  Wire-shape, field names, enum values, error codes pinned до signal-cli source @ `bda4e7fc` (читали Java records напряму, не вгадували) — кожен новий метод цитує `org.asamk.signal.commands.<X>Command.java` у XMLDoc remarks для re-verify на 1 хв. *([signal-cli-api-coverage](openspec/changes/signal-cli-api-coverage/proposal.md), `messaging-interactive` capability)*

- **3 нові typed exceptions для consumer-actionable patterns:**
  - `IdentityChangedException : UntrustedIdentityException` — opt-in subset для розрізнення "re-install уже відомого контакту" vs "first-contact untrusted" (upstream signal-cli обидва кидає кодом `-4`; розрізнення — client-side concern, готується для Wave 3 `ISignalContacts`).
  - `GroupAdminRequiredException : JsonRpcException` — code `-1` (UserError) з message-substring "admin". Дозволяє `catch (GroupAdminRequiredException) { /* escalate */ }` замість inspect-message-text по локалізованих рядках.
  - `CaptchaRequiredException : JsonRpcException` — code `-6` (CaptchaRejected). XMLDoc лінкує consumer flow через signalcaptchas.org → token → submitRateLimitChallenge (Wave 8).
  - `JsonRpcClient` dispatch switch розширено: автоматично кидає правильний derived-тип за кодом+message.

### 🛠 Інше

- **`UntrustedIdentityException` тепер `un-sealed`.** Потрібно для derivation `IdentityChangedException`. Чисто-additive change: existing `catch (UntrustedIdentityException)` працює без змін; new `catch (IdentityChangedException)` працює як строгіший subset.

- **Wave-cycle прецедент: всі 4 нові методи перевикористовують існуючий `SendMessageResponse`** замість додавання 4-х майже-ідентичних response-типів. Research §0.5 (читання `SendMessageResultUtils.java @ bda4e7fc`) підтвердило: wire shape `{ timestamp, results }` ідентичний для усіх 4-х. Уникнено boilerplate × 4.

### 🛡️ Захист від регресій

- **`MessagingInteractiveSerializationTests` — 8 тестів пінять kebab-case wire-shape** (`note-to-self`, `target-author`, `target-timestamp`, `group-id`, `recipient` як singular для receipt'у, `stop` як boolean для typing). Уся wire-shape парситься через `JsonDocument` field-by-field (не raw substring), тож тести не chrupkі до STJ-encoder defaults.
- **`SignalMessageInteractiveTests` — 8 service-level тестів** з Moq pinning'ом method-name → RPC-method, Options → Parameters mapping, derived-exception propagation, null-response гарду.
- **`InteractiveOptionsTests` — 18 builder + validation тестів** для 4-х Options-типів. Покриті: обов'язкові поля, "хоча б один recipient-source" guard, default values (Type=Read, Stop=false), §F7 singular-Recipient invariant.
- **`NewTypedRpcErrorsTests` — 9 тестів** на inheritance-контракти (`IdentityChangedException : UntrustedIdentityException : JsonRpcException`), sealed-маркер, та реальний dispatch через `JsonRpcClient.ProcessMessageAsync` для кодів `-6`/`-1+admin`/`-1 plain`.

### 📊 Тестова статистика

- Unit: 290 → **333** (+43 тести).
- Integration: 8 (без змін; усі mock-only за wave-1 ризик-планом).
- Build на src/ + Tests/: 0 warnings, 0 errors з `TreatWarningsAsErrors=true`.

### 📦 Що в Wave 1 / що в наступних wave'ах

| Wave | Capability | RPC методи | Release | Risk |
|---|---|---|---|---|
| **1** | **`messaging-interactive`** | **sendReaction, sendReceipt, sendTyping, remoteDelete** | **4.1.0 (цей реліз)** | low |
| 2 | `groups-crud` | joinGroup, updateGroup, quitGroup | 4.2.0 (next) | medium |
| 3 | `contacts-identity` | listContacts, listIdentities, trust, updateContact, removeContact, updateProfile, block, unblock | 4.3.0 | low |
| 4 | `sticker-packs` + `binary-resource-fetch` | upload/list/addStickerPack, get{Attachment,Avatar,Sticker} | 4.4.0 | low |
| 5 | `device-management` | add/list/remove/updateDevice | 4.5.0 | medium |
| 6 | `account-lifecycle` *(opt-in gated)* | 8 destructive методів | 4.6.0 | HIGH |
| 7 | `polls` + `messaging-power-user` + receive-decoders | 8 send + 7 receive event-streams | 4.7.0 | medium |
| 8 | `utility-rpc` | getUserStatus, submitRateLimitChallenge, sendContacts | 4.8.0 | low |

Target після Wave 8: **53/54 = 98% coverage** signal-cli JSON-RPC surface (єдиний пропущений — `receive` polling, бо `subscribeReceive` є його кращою альтернативою).

---

## [4.0.3] — 2026-05-25

Patch-реліз. **Нульовий impact на consumer'ів — це чисто developer/agent ergonomics**:
агент-instruction memory (`CLAUDE.md`) переструктурована за Anthropic Memory guidance в
slim root + 9 path-scoped topic files під `.claude/rules/`. NuGet-пакети ідентичні за
runtime-поведінкою.

### 🛠 Інше

- **Якщо ти підтримуєш fork — `CLAUDE.md` тепер 150 рядків замість 592.** Решта 9 topic-файлів живуть у `.claude/rules/*.md` з `paths:` frontmatter — кожен файл вантажиться у Claude Code context лише коли редагуєш файли під його глобом. Сесія що чіпає лише `Tests/**` отримує `testing.md` (а не всі 19+ KB протокольних фактів про signal-cli). Net effect: швидші відповіді агентів на дрібних PR, менше шуму. Spec за Anthropic [Memory docs](https://code.claude.com/docs/en/memory.md). *([claude-md-rules-restructure](openspec/changes/claude-md-rules-restructure/proposal.md), `agent-memory-pathscoping` capability)*

- **CLAUDE.md тепер документує 4 раніше-неявних патерни кодової бази.** Додано (через `claude-md-pattern-additions` change): DI-registration idioms (TryAddSingleton vs AddSingleton, one-instance-two-roles), namespace hierarchy + DTO/EventArgs/test-class naming, exception-derivation heuristic (коли деривувати `XxxException : JsonRpcException` vs залишати base), README voice + drift rules. *([claude-md-pattern-additions](openspec/changes/claude-md-pattern-additions/proposal.md))*

### 🛡️ Захист від регресій

- **RG08 `ClaudeMdSplitConsistencyTests` пінує shape сплиту.** 3 facts: root CLAUDE.md ≤ 200 lines, кожен topic-файл має валідний `paths:` frontmatter АБО `<!-- always-load: no paths -->` marker, substring `Critical rule #N` зустрічається тільки в root (numeric anchors резолвляться лише там). Re-merging topic-content у монолітний CLAUDE.md = build failure. *(RG08)*

### 📊 Тестова статистика

- Unit: 287 → **290** (+3 RG08 facts).
- Integration: 8 (без змін).
- Build на src/ + Tests/: 0 warnings, 0 errors.

### Pending follow-up

- **`/memory` validation у Claude Code session.** Phase 2 design передбачає одноразову перевірку: відкрити сесію що редагує єдиний test-файл і пересвідчитися що `testing.md` завантажено а `signal-cli-protocol.md` — ні. Якщо path-scoping не працює у поточному CLI — додати fallback note в `cloud-dev.md`. Виконується вручну після релізу.

---

## [4.0.2] — 2026-05-24

Patch-реліз без breaking changes. **Якщо ти використовуєш `SignalCli.NET.HealthChecks` — онови його разом з main package**: до 4.0.2 версії розійшлися й змішування пакетів давало runtime crash. Решта — 8 нових захисних тестів і одна реальна знахідка про JSON-валідацію, яка мовчки не fire'ила у продакшені.

### 🐛 Виправлено

- **Якщо консьюм'иш обидва пакети (`SignalCli.NET` + `SignalCli.NET.HealthChecks`) — онови їх разом.** До 4.0.2 main lib був `4.0.1`, а health-checks-adapter — `3.0.0`. Змішування пакетів кидало `MissingMethodException` на першому health-check probe (бо adapter читає internal'и main lib через `[InternalsVisibleTo]`). Тепер обидва завжди ходять в lockstep через єдиний MSBuild property у `Directory.Build.props`. Bug-class виключено: reflection-тест `VersionLockstepTests` ловить розбіжність до merge'у. *(NF-003, RG07)*

- **JSON duplicate-key захист (rule #18) тепер реально діє на production-шляху.** З 4.0.0 ми оголошували що malformed signal-cli response з повтореним ключем fail'ить deserialize. У реальності — fail'ило тільки на reflection-шляху, а production деsеріалізує через source-gen fast-path який власний runtime-flag не консумує. Тепер захист увімкнено на обох рівнях (runtime flag + source-gen attribute). *Без впливу на нормальні signal-cli responses* — Jackson на upstream-стороні фізично не може емітити дублікати; fix чисто defensive проти MITM/corruption. Знайшли під час audit'у коли наш же RG-тест спочатку failed. *([json-hardening-source-gen-attribute](openspec/changes/archive/2026-05-24-json-hardening-source-gen-attribute/proposal.md))*

- **CI pipeline нарешті пушить `SignalCli.NET.HealthChecks` на NuGet feed разом з main package.** До 4.0.2 `publish-nuget.yml` workflow мав pack-steps для 5 пакетів (`SignalCli.NET`, `SignalCli.Runtime`, `SignalCli.Runtime.Native`, `SignalCli.Runtime.Jre.{win-x64,osx-arm64}`) — але `SignalCli.NET.HealthChecks` був відсутній. Net effect: csproj-сторона мала версії в lockstep (NF-003 main fix), але consumer-сторона все одно отримувала stale `3.0.0` HealthChecks-пакета з registry бо нічого свіжіше не пушилось. Цей gap робив весь NF-003 lockstep косметичним на consumer-стороні. Тепер крок присутній — після першого `gh release create v4.0.2` HealthChecks теж потрапить на feed. *(nf003-completion follow-up)*

### 🛡️ Захист від регресій

8 нових unit + 1 E2E-тести закривають declared-але-untested invariants з CLAUDE.md "Future development guardrails":

- **Паралельні JSON-RPC виклики тепер pinned проти реального signal-cli** — не лише проти mock'а на unit-рівні. Новий E2E запускає bundled JRE, кидає 10 паралельних `version`-викликів через справжній signal-cli virtual-thread dispatcher, асертить що всі 10 повертають свою власну відповідь. Якщо хтось у майбутньому замінить `ConcurrentDictionary<id, TCS>` на `Queue<TCS>` "бо порядок збережений" — unit-тести з in-order `Subject<T>`-моком цього б не побачили; цей E2E одразу впаде. *([e2e-coverage-expansion](openspec/changes/archive/2026-05-24-e2e-coverage-expansion/proposal.md))*

- **Observability counters тепер pinned на всіх 3 тригерах перезапуску.** Раніше privacy-guards перевіряли лише *відсутність* PII у Meter-tags, а фактичне інкрементування `signalcli.process.restarts{trigger=…}` було pinned тільки на `trigger=force`. Тепер `trigger=crash` (через event-raise на mock IProcess) і `trigger=health` (через `FakeTimeProvider.Advance` повз `HealthCheckIntervalSeconds`) теж покриті. *(G4 subcases — T04, T05)*

- **Event-API симетрія enforced reflection-тестом.** CLAUDE.md правило "кожен `IObservable<T>` event-kind має парний `IAsyncEnumerable<T>` метод" раніше enforcъ'вся code-review'ом. Тепер `EventApiSymmetryTests` reflectively сканує `ISignalEventService` і fail'ить build якщо хтось додав 11-й event без парного методу. *(NF-002, RG06)*

- **JSON duplicate-key захист — 3-шаровий guard.** Окрім самого fix'у з "Виправлено" вище, додано 3 facts у `JsonSerializationTests`: `SignalJsonOptions_…IsFalse`, `JsonDocumentOptions_…ThrowsOnDuplicateKey`, `SignalJsonContext_…ThrowsOnDuplicateKey`. Видалення будь-якого з 3 шарів (runtime flag, .NET 10 API contract, source-gen attribute) surface'иться як failed test. *(G12, RG05)*

- **JSON-RPC error wins over result.** Захисний test пінує що при malformed-response (одночасно `result` І `error`) JsonRpcClient кидає exception, а не повертає result. CHANGELOG [4.0.1] стверджував покриття; `grep` показав що тесту не існувало. Тепер дійсно існує. *(NF-001, G9)*

### 🛠 Інше

- **Test-проект тепер ловить warnings як errors** (як main lib давно). Раніше тихо акумулювалися 3 `xUnit1031` violations у `SyncDisposeDuringCleanupTests.cs` (deadlock-prone `.GetAwaiter().GetResult()`). Виправлено: метод-сигнатури `async Task` + `await`; `service.Dispose()` навмисно залишається SYNC бо саме його тести й вимірюють. *(NF-004)*

- **`Microsoft.Extensions.*.Testing` bumped 9.0.0 → 10.0.0** — закриває last mismatched-major у dependency graph. Public-surface unchanged per [MS Learn](https://learn.microsoft.com/dotnet/core/extensions/timeprovider-testing). *(NF-005)*

- **Версія обох NuGet-пакетів тепер централізована** в `Directory.Build.props → <SignalCliPackageVersion>`. Bump = single-file edit. Hardcoded `<Version>` у будь-якому csproj заборонено.

- **CLAUDE.md розширено двома секціями для майбутніх контрибуторів + новим правилом:**
  - **"Audit baseline"** — мінімальна планка яку PR не може опустити (test counts, regression guards, архітектурні invariants).
  - **"How we discovered these issues"** — 5 failure-mode-сценаріїв з PR-time checklists, щоб не повторювати ті самі паттерни.
  - **"Version-CHANGELOG lockstep" + "CHANGELOG voice template"** — bump версії МУСИТЬ йти з CHANGELOG-секцією в одному коміті; новий template для consumer-first voice (bold leading claim, технічна обгортка, internal-IDs у дужках).
  - Виправлено константа `MaxInlineEncodedAttachmentBytes`: 15 000 000 → **12 000 000** (drift з 4.0.0 — реальне значення в коді змінилось у `signal-cli-protocol-alignment §5`, doc відстав). *(NF-006)*

- **CHANGELOG voice tightened** — починаючи з [4.0.2] (цей entry), пишемо у consumer-first voice. Старіші версії [4.0.1]/[4.0.0] перероблено в тому ж коміті задля консистенції.

### 📊 Тестова статистика

- Unit: 279 → **287** (+8 захисних тестів).
- Integration: 7 → **8** (+1 E2E на паралельний RPC).
- Build на src/ + Tests/: 0 warnings, 0 errors.

### Pending follow-up

_Нічого — усі audit v2.1 знахідки закриті._

---

## [4.0.1] — 2026-05-24

Patch-реліз без breaking changes. **Дві user-facing покращення:** AOT-публікація через `AddSignalCli(IConfiguration)` нарешті дійсно AOT-safe (warning'и зняті); вкладення з небезпечними іменами файлів (NUL byte, RTL-override) фільтруються від UI-spoofing-атак. Решта — 25 нових захисних тестів + 5 нових E2E.

### 🐛 Виправлено

- **`AddSignalCli(IConfiguration)` тепер дійсно AOT-safe — атрибути `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` зняті.** У 4.0.0 ми оголошували що цей шлях AOT-friendly через `<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>`, але насправді **флаг не був доданий у csproj**. У 4.0.1 нарешті присутній — source-gen перехоплює `OptionsBuilder.Bind` call-site і substitutes reflection-free generated binder ([MS Learn](https://learn.microsoft.com/dotnet/core/extensions/configuration-generator): *"all APIs that eventually call into these various binding methods are intercepted and replaced with generated code"*). AOT-targeting consumers тепер використовують overload без warning'ів. *(`configuration-binder-aot-completion`)*

- **Вкладення з небезпечними іменами файлів захищені від UI-spoofing.** `AttachmentEntry.SafeFileName` тепер відкидає:
  - **Control characters** (U+0000..U+001F + U+007F) — NUL byte у середині імені труньчить файл на багатьох ОС, дозволяючи "невидиме" розширення (`evil\0.jpg.exe` стає `evil`).
  - **Bidi-override markers** (U+202A..U+202E, U+2066..U+2069) — класична UI-spoofing атака: `evil<U+202E>gpj.exe` у Explorer відображається як `evilexe.jpg`, користувач думає що відкриває картинку.
  - **Path-invalid chars** — крос-платформенний union через `Path.GetInvalidFileNameChars()` (Windows строгіший за POSIX).

  Реалізовано через `System.Buffers.SearchValues<char>` (zero-alloc fast-path на чистих іменах) + посимвольну фільтрацію на повільному шляху. За повністю-небезпечного імені — fallback на літерал `"attachment"`. *(`safe-filename-hardening`)*

### 🛡️ Захист від регресій

- **`AttachmentEntryTests` розширено з 3 → 12 тестів.** Кожен bidi/control-символ покрито Theory; повністю-небезпечне ім'я fallback'ує на `"attachment"`; heap-buffer-path при іменах > 256 chars; `SaveToTempFile` re-entry → `InvalidOperationException`.

- **5 нових unit-тестів закривають останні CLAUDE.md untested invariants:**
  - **`ForceRestartAsync` no-op** на нерестартабельних станах (Theory × 4: NotStarted/Starting/Stopping/Stopped) — не змінює стан і не стартує новий процес.
  - **`NotificationChannelCapacity=1`** (мінімум) усе ще FIFO-доставляє всі повідомлення (FullMode=Wait блокує producer'а доки consumer відчитає).
  - **`SubscribeAsync` leader cancellation propagation** — коли leader's CT скасовується mid-RPC, follower отримує **той самий** `OperationCanceledException` через TCS rollback path.

- **3 нових observability-counter тести.** Раніше privacy-guards перевіряли лише *відсутність* PII у Meter-tags. Тепер pinned і фактичне інкрементування:
  - `signalcli.rpc.duration` записує позитивне значення на happy-path RPC.
  - `signalcli.events.dropped{event_type=…}` тикає exactly N разів при overflow (capacity=1024, 1100 typing-нотифікацій → counter=76).
  - `signalcli.process.restarts{trigger=force}` тикає при `ForceRestartAsync()`.

- **2 нові тести на sync-Dispose race** — пінують що `SignalCliHostedService.Dispose()` sync-path дренує `_operationLock` із 50ms fallback'ом і не дедлокає навіть з held-lock.

- **5 нових E2E тестів** (skip-gated тим самим runtime-availability gate що `SignalCliE2EVersionTests`). Без bundled JRE / native бінарника тести return early з `[SKIP] …` маркером; інакше виконують повний E2E:
  - **Start → ForceRestart → Stop** цикл проти реального signal-cli з unique PID між циклами.
  - **External `Process.Kill()`** real signal-cli; чекаємо watchdog tick; асертимо новий PID + новий процес відповідає на `version`.
  - **HealthMonitor cadence** — `HealthCheckIntervalSeconds=2`, чекаємо 6с, асертимо `LastPingResult.Ok==true` + fresh timestamp.
  - **`DisposeAsync` mid-flight** — fire `VersionAsync` паралельно з `host.DisposeAsync()`, асертимо що OS-процес гарантовано exit'ив.
  - **`AddSignalCli(IConfiguration)` end-to-end** — `InMemoryCollection`-bound options → real signal-cli start → `version` повертає "0.14.x" (валідує `configuration-binder-aot-completion`).

### 🛠 Інше

- `<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>` нарешті у `SignalCli.csproj` — root-cause-фікс проблеми з 4.0.0.

### 📊 Тестова статистика

- Unit: 254 → **279** (+25).
- Integration: 2 → **7** (+5 skip-gated E2E).

### Pending follow-up

_Нічого — усі 4 follow-up'и з [4.0.0] повністю закриті._

---

## [4.0.0] — 2026-05-24

**Major з breaking changes.** Три речі для консумерів:

1. **Усі `[Obsolete]`-shim'и з in-flight-to-4.0 видалено** — `Config`, `AddSignalCli(Action<Config>?)`, `Version()` і ще 5 Async-suffix-less методів. Migration table нижче — все механічно через `sed`.
2. **Graceful shutdown нарешті працює без silent Kill** — критичний correctness bug ще з 1.0: **кожен** Stop фактично робив `Process.Kill()`, що означало потенційну SQLite corruption у локальному signal-cli store. Якщо ти страждав від dataset corruption між рестартами — це причина.
3. **JSON-RPC error codes тепер типізовані** — новий enum `JsonRpcErrorCode` + два derived exceptions (`RateLimitException`, `UntrustedIdentityException`) для catch-by-type замість inspect-message-text.

Реліз cargo'ить три OpenSpec changes: `audit-followup-2026` + `signal-cli-protocol-alignment` + `deprecated-shim-removal`.

### ⚠️ Breaking changes — migration table

Усе in-flight-to-4.0 з CLAUDE.md "Backward compatibility convention" нарешті видалено — один-мажорний-грейс відпрацьовано. Усі замінники існували принаймні з 3.0.

| Видалено | Заміна / migration |
|---|---|
| `SignalCli.Models.Config` (клас) | `SignalCliOptions` (існує з 2.1.0). Internal resolver-логіка переїхала в `JavaPathResolver`; mapping → `SignalCliOptionsExtensions.ToProcessConfig`. |
| `ServiceCollectionExtensions.AddSignalCli(Action<Config>?)` | **Для bundled-runtime консумерів:** новий `AddSignalCliWithBundledRuntimeDefaults(Action<SignalCliOptions>?)` — auto-resolve JRE/JAVA_HOME/PATH + delegate-override. **Без bundled-runtime:** `AddSignalCli(Action<SignalCliOptions>?)` напряму. |
| `SignalCliOptionsExtensions.ToOptions(Config)` / `ToIOptions(Config)` | — (адаптери, видалені з Config-типом). |
| `ServiceCollectionExtensions.CopyFrom(SignalCliOptions, SignalCliOptions)` | — (internal helper, був тільки для legacy flow). |
| `SignalCliOptions.ToConfig()` | — (shim). |
| `ISignalCliClient.Version()` | `s/\.Version(/\.VersionAsync(/g` |
| `ISignalAccounts.ListAccounts` / `SyncAccount` | `s/\.ListAccounts(/\.ListAccountsAsync(/g`, `s/\.SyncAccount(/\.SyncAccountAsync(/g` |
| `ISignalDevices.StartLink` / `FinishLink` | `s/\.StartLink(/\.StartLinkAsync(/g`, `s/\.FinishLink(/\.FinishLinkAsync(/g` |
| `ISignalGroups.ListGroups` | `s/\.ListGroups(/\.ListGroupsAsync(/g` |

### ✨ Додано

- **Типізовані RPC-помилки — `catch (RateLimitException)` замість inspect'у `ex.Message`.** Новий public enum `SignalCli.Exceptions.JsonRpcErrorCode` з 10 значеннями: 5 JSON-RPC 2.0 standard (`ParseError -32700`, `InvalidRequest -32600`, `MethodNotFound -32601`, `InvalidParams -32602`, `InternalError -32603`) + 5 signal-cli specific (`UserError -1`, `IoError -3`, `UntrustedIdentity -4`, `RateLimit -5`, `CaptchaRejected -6` — цит. `SignalJsonRpcCommandHandler.java:35-280`). Нова public property `JsonRpcException.KnownCode { get; }` мапить wire-code на enum (null для unknown — forward-compat). Два derived exceptions — **`RateLimitException`** (-5) і **`UntrustedIdentityException`** (-4) — для типових retry-with-backoff / verify-safety-number-flow'ів. *(`typed-rpc-errors`)*

- **`AddSignalCliWithBundledRuntimeDefaults(Action<SignalCliOptions>? = null)`** — public extension як replacement для видаленого `AddSignalCli(Action<Config>?)`. Wires defaults (`AppHome = AppContext.BaseDirectory`, `LibDirectory = "SignalCli/lib"`, `JavaExecutable` auto-resolved через bundled JRE → JAVA_HOME → Windows Oracle → PATH) + consumer override через delegate. *(`config-auto-resolve-migration`)*

### 🐛 Виправлено

- **Graceful shutdown нарешті дійсно працює — критичний correctness bug ще з 1.0.** `SignalCliHostedService.StopProcessInternalAsync` писав літеральне `"exit"` на signal-cli stdin. signal-cli **не має** JSON-RPC методу `exit` і парсить кожен stdin-рядок як JSON — наш літерал виробляв `-32700 Parse error` response на stdout, процес залишався живий, наш wait-for-exit timeout вистрілював, ми завжди falled through на `Kill(entireProcessTree: true)`. **Кожен** graceful shutdown насправді був hard-kill (TerminateProcess на Windows, SIGKILL на Unix), bypass'ачи signal-cli shutdown hooks → **потенційна SQLite corruption** у локальному signal-cli store. **Fix:** закриваємо stdin (`StandardInput.Close()`) замість `WriteLine("exit")` — signal-cli reader-loop природньо завершується, JVM exit clean. Перевірено новим E2E `SignalCliE2EGracefulShutdownTests` через real bundled JRE. *(`graceful-shutdown-fix`)*

- **`AddSignalCli` нарешті дійсно idempotent.** Pre-fix guard ніколи не fire'ив (`IOptions<T>` зареєстровано open-generic, не concrete), тож кожен повторний виклик re-run'ив configure delegate (second-wins на options) і додавав 3 duplicate `IHostedService` descriptor'и → подвійний startup. CHANGELOG `[3.0.0]` оголошення idempotency було over-broad. **Fix:** private sentinel-type marker. *(`addsignalcli-idempotency-fix`)*

- **Coverage badges у README тепер render'яться поза github.com.** Relative paths `.github/badges/*.svg` працювали тільки на github.com — інші renderers (NuGet.org, IDE previewers, third-party gallery sites) інтерпретували `.github` як hostname → broken `http://.github/…` URLs. Тепер absolute `raw.githubusercontent.com` URLs у README + 4 emission sites у CI workflow. Bonus: `<PackageReadmeFile>README.md</PackageReadmeFile>` додано — README тепер у NuGet pack, build warning *"missing a readme"* зник. *(`badge-url-fix`)*

### 🛡️ Defensive hardening

- **JSON duplicate-key захист увімкнено.** `SignalJson.Options.AllowDuplicateProperties = false` (новий .NET 10 flag). Малформовані signal-cli responses з повтореним ключем тепер кидають `JsonException` замість silent last-wins. *Caveat виявлений пізніше у [4.0.2]:* flag fire'ить лише на reflection-шляху; повний захист для source-gen path landед у [4.0.2]. *(`json-hardening`)*

- **Attachment inline-vs-temp-file threshold знижено з 15M → 12M.** signal-cli's Jackson 2.20.2 enforces `maxStringLength = 20_000_000` per STRING TOKEN. Base64 inflation 4/3: 12M raw × 4/3 = 16M encoded → 4M margin для решти `send` JSON envelope. Old 15M давало 20M encoded — exactly at cap, zero margin → occasional `StreamConstraintsException` на attachments близьких до межі. *(`attachment-threshold-margin`)*

- **`JsonRpcClientHostedService._client` тепер `volatile`** — захист thread-safety reads з `SignalCliHealthMonitor` + `SignalEventService` на ARM64 (.NET 10 first-class). На x64 reference-read атомарний; на ARM64 без acquire/release-семантики reader міг би побачити stale null. `volatile` додає memory barrier з near-zero cost на x64. *(`field-barrier-hardening`)*

- **`SignalCliHostedService.Dispose()` тепер дренує `_operationLock`** перед `DisposeCore()` із 50ms fallback timeout — синхронізує read `_currentProcess` з паралельними write'ами під lock. Worst case identical to pre-fix. *(`field-barrier-hardening`)*

### 🛠 Інше

- **CLAUDE.md новий H2 розділ "signal-cli protocol behavior we depend on"** — 7 cited facts про upstream signal-cli (stdin EOF graceful, stdout pure-JSON line-flushed, virtual-thread parallel dispatch, `subscribeReceive` non-idempotent at protocol level, Jackson `maxStringLength = 20M`, custom error codes `-1..-6`, Java 25 requirement). Кожен факт pin'ить до signal-cli source file:line @ commit `bda4e7f`. Bumping `<SignalCliVersion>` має сопровождатися re-verify-pass.

- **3 нові reflection-based regression guards** — будь-яка drift тепер ловиться build-time:
  - **`ObsoleteMessageConsistencyTests`** — сканує кожен `[Obsolete]` attribute, parses "will be removed in N.0", asserts N > current major. Закриває drift-class коли M-1 version-message лишається у source після релізу.
  - **`EventIdBlockTests`** — Theory × 12 `*Log.cs` classes, asserts EventId лежить у reserved block per CLAUDE.md.
  - **`PublicApiSurfaceTests`** — reflective walker генерує line-per-public-member, diffs проти baseline (1087 lines). Accidental public-API drift → fail з unified diff.

- **6 stale `[Obsolete("…will be removed in 3.0")]` повідомлень переписано на `4.0`** — codebase вже був 3.0.0 коли message казало 3.0; drift trained agents to disbelieve `[Obsolete]` lifetime claims.

- **`<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>` згадано в csproj-документації, але** `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` на `AddSignalCli(IConfiguration)` довелося лишити: `OptionsBuilder.Bind<T>(IConfiguration)` сам framework-annotated, і source-gen цей call-site не перехоплює. AOT-targeting consumers MUST use `AddSignalCli(Action<SignalCliOptions>?)`. *(У [4.0.1] виявлено що флаг насправді не був доданий у csproj — root-cause-fix landed там.)*

- **Edge-case test coverage додано:** `AtomicCounter` int32 wrap, JSON-RPC `error.data` field preservation, attachment encoded-size boundary, `EnvironmentVariables` read-only-snapshot semantics, `AddSignalCli` idempotency × 3.

- **Race-prober `Client_ConcurrentAccessUninitialized_DoesNotThrowNullRef`** (50 parallel readers, `JsonRpcClientHostedService`) — пінує volatile-семантику.

- **Integration E2E `SignalCliE2EGracefulShutdownTests`** — валідує `graceful-shutdown-fix` через real signal-cli runtime (skip-gated).

- **Example/Program.cs** тепер typed lambda `(SignalCliOptions o) => {...}` замість cast — overload resolution однозначний без annotation noise.

- **CLAUDE.md "Future development guardrails"** — каталог untested invariants для майбутніх PRs.

### 📊 Тестова статистика

- Unit: 215 baseline → **254** (+39 net new).

### Pending follow-up

_Усі 4 позиції закриті у [4.0.1]:_
- ✅ Configuration-binder full AOT fix → `configuration-binder-aot-completion`.
- ✅ 4 з 5 integration-tests-expansion E2E → `SignalCliE2EAdditionalTests`.
- ✅ 6 з 12 edge-case-coverage tests → `safe-filename-hardening` + `observability-counter-assertions`.
- ✅ 1 з 2 race-prober tests → `SyncDisposeDuringCleanupTests`.

---

## [3.0.0] — 2026-05-24

Друга велика хвиля модернізації — фокус на correctness/observability/agent-friendly-API. **Містить breaking changes**, перерахованих нижче.
Реалізація триває; цей розділ оновлюється у міру викочування кластерів. Див. `openspec/changes/post-modernize-tuning/`.

### ⚠️ Breaking

- `FinishLinkResponse.number` → **`FinishLinkResponse.Number`** (PascalCase property; JSON wire-name збережено через `[JsonPropertyName("number")]`).
- `SubscribeReceiveResponse.id` → **`SubscribeReceiveResponse.Id`** (так само PascalCase).
- `BaseSignalEventArgs.Account` тепер `string` (non-nullable). Те ж саме поширено на всі 10 `*EventArgs`-records. Раніше було `string?`, що змушувало кожного підписника null-чекати гарантовано-присутнє значення.
- `Config.EnvironmentVariables` і `SignalCliOptions.EnvironmentVariables` тепер `IReadOnlyDictionary<string,string>` на читання. Для мутації — `Config.WithEnvironment(IDictionary<string,string>)` (defensive copy + fluent return). Раніше можна було `.Add(key, value)` на shared посилання після DI-capture.
- `JsonRpcException(string, Exception?)` ctor з нестандартним кодом **-32000** видалено. Замість нього — три CA1032-стандартні ctors: `()`, `(string)`, `(string, Exception)` — усі з канонічним JSON-RPC 2.0 кодом **-32603** ("Internal error"). Консумери, що каталі legacy-конструктор, мають мігрувати на CA1032-ctors або передавати власний `JsonRpcError`.
- `SignalEventService.SubscribeAsync(account)` тепер **ідемпотентний** — повторні виклики для того самого облікового запису повертають той самий `subscriptionId` без RPC. Раніше другий виклик кидав `InvalidOperationException`. `catch (InvalidOperationException) when (msg.Contains("вже підписаний"))` більше не зловить — операція тепер успіх.
- `JsonRequired` на always-present полях `Envelope.cs`: `JsonRemoteDelete.RemoteDeleteId`, `Offer.Type`/`Offer.Opaque`, `Answer.Opaque`, `IceUpdate.Opaque`, `Hangup.Type`. Якщо signal-cli колись поверне ці поля з `null` — десеріалізація фолтиться з `JsonException` замість тихо пропустити `null` у non-nullable property.
- `UserRecipient`/`GroupRecipient` ctor: null → `ArgumentNullException`, empty → `ArgumentException`. Раніше empty теж кидав `ArgumentNullException` (порушення контракту обох типів).
- `SignalCliHostedService` тепер `sealed` — інхеріт не підтримується.
- Стандартний шлях `dotnet publish /p:PublishAot=true` ще не enable'нений (deferred — потребує redesign на `JsonTypeInfo<T>` overloads), але всі предумови (drop Nito.AsyncEx, drop `.ValidateDataAnnotations()`, source-gen JSON fast-path) на місці.
- **(round 9 §4.7)** `CancellationToken`-property + `WithCancellationToken`-Builder-method видалено з `TextMessageOptions` / `AttachmentMessageOptions` / `StickerMessageOptions`. Єдиний шлях скасування — параметр `Send*Async(options, cancellationToken)`. `[Obsolete]`-shim після одного major-релізу (як обіцяно в CLAUDE.md "Backward compatibility convention"). Migration: `.WithCancellationToken(ct).Build(); → .Build();` + передати `ct` другим аргументом.
- **(round 9 §4.23/4.24)** `ISignalMessage.{SendText,SendAttachment,SendSticker}MessageAsync` повертають `Task<SendMessageResponse>` (single response), а не `Task<List<SendMessageResponse>>` — все одно завжди було `[response]`-wrap. Migration: `(await SendTextMessageAsync(opts))[0] → await SendTextMessageAsync(opts)`.
- **(round 9 §4.27)** Generic-параметри `InvokeMethodAsync` поміняли порядок: `<TResponse, TRequest>` → `<TRequest, TResponse>` на `ISignalCliClient`, `IJsonRpcSender`, обох impls + ~22 callsites. Узгоджено з `JsonSerializer.Deserialize<TValue>`-конвенцією. **Shim неможливий** — C# не розрізняє overload'и за порядком typeparam'ів (same runtime signature). Migration: розверни `<X, Y>` → `<Y, X>` на кожному виклику.

### ✨ Додано

#### Observability (capability `observability`)
- Єдиний `internal static readonly ActivitySource SignalCliDiagnostics.ActivitySource = new("SignalCli.NET", AssemblyVersion)` — спани `rpc.<method>`, `signalcli.process.start`, `signalcli.healthcheck.ping`, `signalcli.subscribe`. Теги: method name, status enum, integer id, exception type name — без PII.
- Єдиний `internal static readonly Meter SignalCliDiagnostics.Meter = new("SignalCli.NET", AssemblyVersion)`:
  - `Counter<long> signalcli.rpc.requests` (теги `method`, `status` ∈ {`ok`,`timeout`,`error`})
  - `Histogram<double> signalcli.rpc.duration` (мс, тег `method`)
  - `Counter<long> signalcli.process.restarts` (тег `trigger` ∈ {`force`,`crash`,`health`})
  - `Counter<long> signalcli.events.dropped` (тег `event_type` ∈ 10 значень) — замінює приватний `_droppedCount`.
  - `ObservableGauge<int> signalcli.subscriptions.active`.
- Документація: `docs/cloud-development.md` має нову секцію Observability з drop-in OTel-snippet.

#### RPC robustness
- `SignalCliOptions.NotificationChannelCapacity` (default 1024). Між stdout-парсером і fan-out-споживачем — bounded Channel; повільний підписник створює back-pressure аж до signal-cli.
- `JsonRpcClient` приймає `TimeProvider` — `CancellationTokenSource(_requestTimeout, _timeProvider)` робить timeout-шлях віртуалізованим у тестах.
- `SignalCliHostedService.StopProcessInternalAsyncNoLock` теж використовує `CancellationTokenSource(_, _timeProvider)`.
- `BeginScope(RpcMethod, RpcRequestId)` у `JsonRpcClient.InvokeMethodAsync` — кожний нижчий `JsonRpcClientLog.*` несе structured-properties.

#### Subscription race safety
- Reservation placeholder pattern у `SignalEventService.SubscribeAsync` через `Dictionary<string, TaskCompletionSource<int>> _pendingSubscribes`. Конкурентні виклики для того самого облікового запису роблять РІВНО 1 RPC; усі N викликачів отримують той самий ID.
- `ObjectDisposedException.ThrowIf(_disposed, this)` на `SubscribeAsync`/`UnsubscribeAsync` (audit C6).

#### Async-suffix shims (one-major-grace)
- `ISignalAccounts.ListAccountsAsync`/`SyncAccountAsync`, `ISignalDevices.StartLinkAsync`/`FinishLinkAsync`, `ISignalGroups.ListGroupsAsync` — нові методи + `[Obsolete]` DIM-shims на старі імена ("will be removed in 4.0").

### 🛠 Внутрішнє

- `ProcessStateManager`: snapshot-then-emit (OnNext поза локом — System.Threading.Lock не реентрантний). `_disposed` всюди → `int` з `Interlocked.Exchange` (lock-free disposal short-circuit). Catch `ObjectDisposedException` з OnNext (documented disposal race window).
- `_disposed` стандартизовано як `int + Interlocked.Exchange` у `SignalCliHostedService`, `JsonRpcClient`, `JsonRpcClientHostedService`, `SignalEventService`.
- `Nito.AsyncEx` видалено. `JsonRpcClient._sendLock` і `SignalCliHostedService._operationLock` → `SemaphoreSlim(1,1)` з `WaitAsync`/`Release`.
- `.ValidateDataAnnotations()` видалено з options-pipeline — `[OptionsValidator]` source-gen самостійно перевіряє `[Required]`/`[Range]` без reflection. Знято останній AOT-blocker у options-шляху.
- `SignalJsonContext.GenerationMode = Default` (fast-path emission + metadata) замість Metadata-only.
- `SignalEventService`, `ProcessWrapper`, `ProcessFactory`, `JsonRpcClientFactory`, `SignalAccounts`, `SignalDevices`, `SignalGroups` — sealed (CA1052).
- `Config.BuildClasspath` кешує classpath; `Directory.GetFiles` викликається рівно 1 раз на `Config`-інстанс.
- `ValidateRecipients`: single-pass materialization + один `foreach` на user/group split (раніше — 3 пройдення).
- `ArgumentException.ThrowIfNullOrEmpty` boundary checks у `SignalDevices.FinishLinkAsync`/`SignalGroups.ListGroupsAsync`.
- `JetBrains.Annotations` PackageReference `PrivateAssets="all"` — більше не leak у consumer dependency graph.
- `Example/Program.cs` повністю переписаний на `async Task Main`/`await host.StopAsync()`/awaited `SendTextMessageAsync` — LLM-агенти, що копіюють приклад, успадковують правильні async-патерни.
- Forward-slash MSBuild paths у `SignalCli.runtime.csproj` і `SignalCli.Native.targets` — Linux-збірки runtime-пакетів більше не ламаються тихо.
- **(round 8)** Logging-perf analyzer rules: `CA1848 → warning` (блокує regression на direct `_logger.Log*` — кожна нова log-callsite має йти через `[LoggerMessage]`); `CA1873 → suggestion` (analyzer не розпізнає manual `IsEnabled`-guards, тож `warning`-рівень дає false-positives на legitimate Trace-only eager-eval сайтах). Trade-off задокументовано в `.editorconfig`.
- **(round 8)** Manual `if (_logger.IsEnabled(LogLevel.Trace))` guards над `string.Join(", ", response)` у `SignalAccounts.ListAccountsAsync` + `SignalGroups.ListGroupsAsync` — `[LoggerMessage]`-внутрішній IsEnabled живе всередині generated-methоду, тож callsite-level allocation платилася на КОЖНОМУ виклику (навіть Info-level). Економить N × string-allocation на listAccounts/listGroups.
- **(round 8)** `ObservabilityPrivacyTests` flake-fix: lock-snapshot pattern для `_capturedActivities`/`_capturedMeasurements`. ActivityListener/MeterListener реєструються глобально на ActivitySource/Meter, тож callback'и можуть прилітати з потоків паралельних тестів — `List<T>` не thread-safe → `Collection was modified` intermittent. Всі writes тепер під `Lock`, читачі enumerate'ять snapshot.
- **(round 9 §4.9)** `.editorconfig` піднято `CA2007 (ConfigureAwait)` до `warning` після audit-перевірки: 0 missing-sites у `src/SignalCli/**`. Тепер регресія неможлива — будь-який майбутній bare `await` ловиться build-warning'ом.
- **(round 9 §4.25)** `[StringSyntax(StringSyntaxAttribute.Uri)]` на `TextMessageOptions.PreviewUrl`, `PreviewImage`, та параметрах `Builder.WithPreview(previewUrl, …, previewImage)`. Zero runtime cost; IDEs тепер валідують URL-syntax.
- **(round 9 §4.26)** XMLDoc'и на 3 `Send*Async`-методах в `ISignalMessage` отримали `<exception cref="TimeoutException">` із посиланням на `SignalCliOptions.RequestTimeoutSeconds`. Closes audit-doc-gap.
- **(round 10 §6.12)** Новий test-file `JsonContextRegistrationTests` — рефлексивно стверджує, що кожен `*Parameters`/`*Response` DTO у `SignalCli.Models.Signal.*` зареєстрований у `SignalJsonContext` через `[JsonSerializable(typeof(...))]`. Захист від "silent {}"-регресії, коли source-gen контекст не знає тип і `JsonSerializer.SerializeToElement` тихо повертає порожній об'єкт. Закриває audit N8.
- **(round 10 §7.4)** `Assert.Equal(1.0, …)` → `Assert.Equal(1.0, …, precision: 3)` у `MonitorLoop_ShouldRespectHealthCheckInterval`. CA2243 best-practice — explicit precision на double-asserts.
- **(round 10 §4.9 follow-up)** Scope-обмеження `CA2007 → warning` тільки для `src/SignalCli/**`; у `Tests/**` залишили `none` (xUnit-runner запускає тести без SynchronizationContext, тож `.ConfigureAwait(false)` no-op'не).
- **(round 11 §4.20)** `ListAccountsResponse` і `ListGroupsResponse` тепер wrapper-records над `IReadOnlyList<T>` із custom `JsonConverter` (зберігає плоский JSON-array на wire). Раніше успадковували `List<T>` — консумер міг мутувати дані сервера через `.Add`/`.Clear`/`.Sort`. **Breaking для тих, хто мутував** (compile-time error). Реєстрації `List<Account>` + `List<Group>` додано в `SignalJsonContext` (source-gen у .NET 7+ не має reflection-fallback). `Account` отримав `[JsonPropertyName("number")]` — корекція wire-shape на write. 2 нові round-trip тести в `JsonSerializationTests`.
- **(round 11 §8b.3)** Новий public overload `AddSignalCli(this IServiceCollection, IConfiguration)` — канонічний шлях для `appsettings.json`-конфігурації. Robить `AddOptions<SignalCliOptions>().Bind(section)` із тими ж валідаційними правилами (XOR Java/Native + source-gen validator + ValidateOnStart), що й `Action<SignalCliOptions>`-overload. Залежність `Microsoft.Extensions.Options.ConfigurationExtensions 10.0.0` додано в `SignalCli.csproj`. 3 нові тести в `OptionsValidationTests`. Closes audit B5 hookup.
- **(round 12 §8c.9)** Test: `SendTextMessageAsync_StatefulEnumerableRecipients_AreEnumeratedExactlyOnce` — захист §8c.5 single-pass-materialization від регресії до 3-х проходів (validate + 2× Where) на stateful IEnumerable.
- **(round 12 §8c.10)** Test: `ToProcessConfig_CachesClasspath_SecondCall_DoesNotEnumerateFiles` — observable-pattern (delete jar between calls), захист §8c.8 classpath-кешування.
- **(round 12 §9.6/§11.C.5)** CLAUDE.md "Established patterns" — нова **Observability** subsection: single ActivitySource/Meter `"SignalCli.NET"`, canonical tag-key set `{method, status, trigger, event_type}` (pinned by `MeterTagValues_AreOnlyKnownEnumLiterals`), HealthChecks adapter як ОКРЕМИЙ optional-package (NEVER hard dep на `Microsoft.Extensions.Diagnostics.HealthChecks` у core), lock+snapshot pattern для listener-fan-out тестів.
- **(round 13 §7.2/§7.3)** Reflection helpers `GetPrivateField<T>`/`SetPrivateField` видалено з `SignalCliHostedServiceTestsBase`. Замість них — `internal IProcess? SignalCliHostedService.CurrentProcessForTests` + `CurrentStreamPairForTests` (typed test-seam, видимий через `InternalsVisibleTo("SignalCli.Tests")`). 35 reflection-сайтів у 7 test-файлах перекинуто на типовий доступ. Renames приватних полів тепер ламають білд (compile-error), а не повертають мовчазний null.

#### AOT (capability `aot-readiness`) — round 14

- **(round 14 §6.7) `<IsAotCompatible>true</IsAotCompatible>` УВІМКНЕНО** в `SignalCli.csproj`. Library тепер ship'иться як AOT-сумісна — консумери можуть `dotnet publish /p:PublishAot=true` свої app'и без IL2026/IL3050 warnings, що приходять із нас. **Cold-start win**, **smaller native binary**, **WASM/iOS-friendly**.
- ⚠️ **(round 14 §6.7)** Breaking: `ISignalCliClient.InvokeMethodAsync<TReq, TResp>` тепер вимагає 2 нових параметри — `JsonTypeInfo<TRequest> requestTypeInfo` + `JsonTypeInfo<TResponse> responseTypeInfo`. Те ж саме на `IJsonRpcSender`. Migration: `client.InvokeMethodAsync<FooReq, FooResp>("m", req, ct)` → `client.InvokeMethodAsync("m", req, SignalJsonContext.Default.FooReq, SignalJsonContext.Default.FooResp, ct)`. Це **enables AOT-safety** — generic-overload `JsonSerializer.Serialize<T>(_, options)` (reflection-based) повністю відсутній з production-path.
- **(round 14 §6.4)** `SignalJson.Options.TypeInfoResolver` тепер **тільки** `SignalJsonContext.Default` — reflection fallback видалено. Будь-який тип, що крос-уйде JSON-кордон з `src/SignalCli/**` MAY бути зареєстрований у `SignalJsonContext`, інакше — runtime `NotSupportedException` (захист через `JsonContextRegistrationTests` (§6.12)).
- **(round 14 §6.10)** Новий `SignalJson.OptionsForTests` property (`[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`) — test-only path із reflection-fallback для анонімних типів. Анонімні-payload usages у `JsonRpcClientTests` (8 сайтів) замінено на `TestProbeRequest`/`TestProbeResponse`-records у новому `Tests/SignalCli.Tests/TestSerializationContext` (test-local `JsonSerializerContext`, не забруднює production).
- **(round 14 §6.11)** CLAUDE.md rule #6 оновлено: source-gen-only invariant + `OptionsForTests`-test-path задокументовано.
- **(round 14 §8b.10/§11.D.2/§9.4)** З AOT-увімкненим: library build = 0 IL2026/IL3050 warnings (включно з `Diagnostics/`, options-pipeline). `AddSignalCli(IConfiguration)` overload позначено `[RequiresUnreferencedCode]` (бо `Bind` тягне reflection) — для AOT-deploy використовуйте `AddSignalCli(Action<SignalCliOptions>)`.

#### Deferred-cluster (round 15) — усі тести з 2026-05-23 audit реалізовано

- **(round 15 §1.6/§7.7)** `BackPressureTests.NotificationBurst_WithSlowSubscriber_AllMessagesDeliveredInOrder`: 100-message burst через приватний ProcessMessageAsync (reflection), bounded channel capacity=8, sync-subscriber 5ms/msg → всі 100 доставлено в FIFO-порядку. Захист від drop'ів і реордерінгу при slow-consumer back-pressure.
- **(round 15 §1.8)** `TimeoutVirtualizationTests` × 2: `_InvokeMethodAsync_TimeoutPath_VirtualizedByFakeTimeProvider_ThrowsTimeoutException` (FakeTimeProvider.Advance(61s) триггерить timeoutCts → TimeoutException без real wall-clock) + sanity `_CallerCancellation_DoesNotFalselyAttributeToTimeout`. Сертифікує §1.7 TimeProvider-CTS wire-up.
- **(round 15 §2.5/§7.5)** `StateManagerReentrancyTests` × 2: synchronous Rx-subscriber виклика повторний `UpdateState` із OnNext-handler — ланцюг доходить до Stopping за <2с (інакше WaitAsync фейлить як deadlock). Concurrent-callers contention теж покрито.
- **(round 15 §4.15)** `*Options.Builder.Build()` post-mutation guard на 3 типах — кидає `InvalidOperationException` якщо обов'язкові поля обнулено між ctor і Build (захист від reflection / record-`with` mutation).
- **(round 15 §5.12)** `ScopeCaptureTests.InvokeMethodAsync_OpensScope_WithRpcMethod_AndRpcRequestId` через `FakeLogger<JsonRpcClient>` (пакет `Microsoft.Extensions.Diagnostics.Testing`) — фіксує що `RpcMethod` + `RpcRequestId` structured-scope-properties присутні на кожному log-entry, як обіцяно §5.11.
- **(round 15 §8a.6)** `BackgroundServiceLifecycleTests.StopAsync_BlocksUntilExecuteAsync_ObservesCancellation`: FakeTimeProvider-driven tick → ping observed → StopAsync → ExecuteTask.IsCompleted upto 5s real-time. Доказує що base.StopAsync блокує до завершення ExecuteAsync.
- **(round 15 §8a.8)** `StopProcessTimeoutVirtualizationTests.StopAsync_WhenWaitForExitTimesOut_KillsProcess_OnVirtualClock`: mock-process'у `WaitForExitAsync` блокує на CancellationToken.Register; `fakeTime.Advance(StopTimeoutSeconds + 1)` тригерить kill-branch. Сертифікує §1.7/§8a.7 TimeProvider-CTS wire-up на StopProcessInternalAsync.

#### Hosting modernization + CI smoke (round 16) — закриває останні 4 пункти

- **(round 16 §8a.2)** `SignalCliHostedService` і `JsonRpcClientHostedService` тепер `IHostedLifecycleService` (extends `IHostedService` із 4 додатковими phase-методами: `StartingAsync`/`StartedAsync`/`StoppingAsync`/`StoppedAsync`). Реалізації — no-op (поточна поведінка не зміняється); generic-host автоматично детектить interface і викликає phase'и у визначеному order'і. Foundation для майбутніх ordering-refinement'ів (warm-up ping після всіх start'ів тощо).
- **(round 16 §8a.3)** `SignalCliHostedService` тепер реалізує **обидва** `IAsyncDisposable` + `IDisposable`. `DisposeAsync` дренує `_operationLock.WaitAsync` із 2с-fallback-timeout (in-flight `Start/Stop/Restart` має шанс завершитися cleanly перед kill'ом); потім — спільний `DisposeCore` із sync-cleanup'ом. `Dispose()` — sync-only, без drain'у. **CLAUDE.md rule #9** (no sync-over-async in disposal) дотримано: обидва шляхи мають незалежні реалізації, спільне ядро. DI-контейнер preferр'ить `DisposeAsync` при scope-tear-down. Новий log-event `DisposeAsyncDrainTimeout` (EventId 132). 5 нових тестів у `AsyncDisposalLifecycleTests`.
- **(round 16 §8d.13/§8d.14)** Новий GitHub Actions workflow `.github/workflows/runtime-smoke.yml` із двома Linux-job'ами:
  - `native-runtime-delivery` — повна `dotnet build SignalCli.sln`; assertion: `signal-cli-native/signal-cli` дойшов у consumer TargetDir і має executable-bit. Захист від forward-/back-slash-регресії у MSBuild `Include`/`PackagePath` (closes audit N1 §8d.1).
  - `jre-guard-corruption` — build jre-runtime, delete `bin/java*`, re-build expected to fail із actionable message. Захист від видалення/деградації §8d.10 post-extract `<Error Condition>`-guard.
  - Path-filtered (`src/SignalCli.runtime*`, `src/build/`), `workflow_dispatch` для manual-run. `actions/*` pinned до commit-SHA per §8d.9 supply-chain.

Tests: **215/215 ✅** (baseline 180 → 215). Окрім тестів у workflow'ах — всі OpenSpec-таски виконано.

## [2.1.0] — неопубліковано

**Agent-friendly modernization** — п'ять незалежно вмикаємих кластерів, що приводять
бібліотеку у відповідність до сучасних патернів .NET 10 / C# 14 і підвищують
discoverability для AI-агентів і людей. Усі зміни (окрім трьох дрібних, явно
позначених нижче) — additive: старий код продовжує працювати з `[Obsolete]`-warning-ами.

### ✨ Додано

#### Agent-friendly API (cluster A)
- `ISignalCliClient.VersionAsync(CancellationToken)` — новий метод; старий `Version()`
  лишається як `[Obsolete]`-shim до 3.0.
- `ISignalMessage.Send{Text,Attachment,Sticker}MessageAsync` отримали явний параметр
  `CancellationToken cancellationToken = default` (лінкується з deprecated
  `options.CancellationToken` через `CreateLinkedTokenSource`).
- `TextStyleMode` enum замість stringly-typed `string? textMode = "styled"` (internal).
- `[CallerArgumentExpression]` у валідаторах — `ArgumentException.ParamName` тепер
  автоматично береться з виразу-аргументу.
- Усі `TaskCompletionSource<T>.TrySetCanceled` у `JsonRpcClient` тепер передають
  токен — викликач бачить причину скасування через `OperationCanceledException.CancellationToken`.
- `AtomicCounter` спрощено: `unchecked Interlocked.Increment` без CAS-reset гілки.

#### Background monitor (cluster B)
- `SignalCliHealthMonitor` тепер `BackgroundService` із `PeriodicTimer(interval, TimeProvider)`
  замість ручного `Task.Run` + `while (!ct.IsCancellationRequested) await Task.Delay(...)`.
- `SignalCliHostedService` приймає опціональний `TimeProvider`; усі `Task.Delay`/таймери
  всередині пропущені через нього (включно з вікном стабільності рестартів — раніше
  було сирий `Task.Run` + `Task.Delay`). Тестам можна підкласти `FakeTimeProvider`.

#### Async-stream events (cluster E)
- `ISignalEventService` розширено десятьма `*Async`-методами
  (`TextMessagesAsync`, `ReactionAsync`, `AttachmentsAsync`, …), які повертають
  `IAsyncEnumerable<TEventArgs>` поверх `Channel.CreateBounded<T>(1024, DropOldest)`.
  Стандартний C# `await foreach`, back-pressure (чого `Subject<T>` не має), drop-oldest
  при переповненні з лічильником у Debug-логах. Існуючі `IObservable<T>`-API залишаються
  для fan-out-сценаріїв.

#### Options pattern (cluster D)
- Новий `SignalCliOptions` (звичайні setter-и + `[Required]`/`[Range]`
  DataAnnotations) + `AddSignalCli(Action<SignalCliOptions>?)`-overload з
  `ValidateDataAnnotations() + Validate(...) + ValidateOnStart()`. Помилки конфігу
  фейляться на старті хоста з `OptionsValidationException` (не на `ToProcessConfig()`).
- `[OptionsValidator]` source-gen-валідатор: DataAnnotations перевіряються без
  reflection (AOT-safe).
- **D.4 повна міграція:** усі внутрішні сервіси
  (`SignalCliHostedService`, `SignalCliHealthMonitor`, `JsonRpcClientFactory`, `JsonRpcClient`)
  тепер приймають `IOptions<SignalCliOptions>` замість `Config`.
- Внутрішні сервіси читають `_options.Value` один раз у конструкторі (immutable).

#### Source-generated logging (cluster C)
- Усі 109 `ILogger` callsites переведено на `[LoggerMessage]`-`partial`-методи в
  `src/SignalCli/Logging/*Log.cs` (11 файлів, по одному на сервіс). Фіксовані
  EventId-блоки за сервісами: 100s — HostedService, 200s — HealthMonitor,
  300s — JsonRpcClient, 400s — JsonRpcClientHostedService, 500s — SignalEventService,
  600s — SignalService, 700s — SignalMessage, 800s — Accounts/Devices/Groups,
  900s — ProcessRunner/ProcessStateManager.
- Закриває CA1848 (`LoggerMessage`) і CA1873 (`AvoidExpensiveLogging`).
- `SignalEventService.OnNotificationReceived` тепер обгортає обробку нотифікації
  в `ILogger.BeginScope` зі структурованими `SubscriptionId`/`Account` —
  усі downstream-логи успадковують контекст.

### ⚠️ Несумісні зміни (BREAKING)
- **`IJsonRpcClient` більше не успадковує `IDisposable`** — лише `IAsyncDisposable`.
  Сторонні споживачі мають використовувати `await using` замість `using`. Прибрано
  внутрішній sync-over-async `Dispose()` (`DisposeAsync().GetAwaiter().GetResult()`).
- **Фасади `SignalAccounts`/`SignalDevices`/`SignalGroups`/`SignalService`/`SignalMessage`
  більше не імплементують `IDisposable`** (вони не тримали ресурсів; порожні `Dispose()`
  лише плутали). Зовнішні `using (signalAccounts)` тепер не компілюються — приберіть.
- **`IJsonRpcClientFactory.CreateAsync` → `Create()`** (синхронний). Фабрика не робила
  async-роботи; фейк-Async-суфікс прибрано.
- `Microsoft.Extensions.Options.DataAnnotations` 10.0.0 — нова залежність бібліотеки.

### 🛠 Інше
- `Config` лишається як `[Obsolete]`-shim, що мапиться у `SignalCliOptions` через
  адаптер. Буде видалений у 3.0.
- `*Options.CancellationToken` (`TextMessageOptions`, `AttachmentMessageOptions`,
  `StickerMessageOptions`) та `WithCancellationToken`-білдери позначено `[Obsolete]` —
  передавайте токен прямо в `Send*Async(options, ct)`. Буде видалено в 3.0.
- Тести: 173 → 180 (нові `OptionsValidationTests` × 4, `AsyncEnumerableEventDispatchTests` × 3).
  Усі стабільні; раніше flaky `ForceRestart*Delay*` тести переведено на `FakeTimeProvider`.

## [2.0.0] — неопубліковано

### ⚠️ Несумісні зміни (BREAKING)
- **Цільова платформа `net9.0` → `net10.0` (LTS).** Споживачам потрібен .NET 10 SDK/рантайм.
- **Прибрано залежність `Newtonsoft.Json`** — серіалізація повністю на `System.Text.Json`
  (з source-generated контекстом). Моделі тепер використовують `[JsonPropertyName]`.
- `JsonRpcRequest.Params` і `JsonRpcResponse.Result` тепер `System.Text.Json.JsonElement`
  (раніше `Newtonsoft.Json.Linq.JToken`).
- Узагальнене обмеження `InvokeMethodAsync<TResponse, TRequest>` змінено з `where TResponse : class`
  на `where TResponse : notnull` (тепер підтримує value-типи, напр. `JsonElement`).

### ✨ Додано
- **Native-режим без Java:** `Config.SignalCliExecutable` запускає нативний (GraalVM)
  бінарник signal-cli напряму, без JVM. Новий пакет **`SignalCli.Runtime.Native`**
  бандлить офіційний native-білд (Linux x64, SHA-256-перевірений). `Config.CreateDefault()`
  більше не вимагає Java — її відсутність не кидає виняток на етапі реєстрації.
  *(Офіційних native-білдів для Windows/macOS немає — там потрібна Java.)*
- **Bundled-JRE варіанти без системної Java (Windows/macOS):** нові пакети
  **`SignalCli.Runtime.Jre.win-x64`** та **`SignalCli.Runtime.Jre.osx-arm64`** містять
  вбудований Eclipse Temurin 25 JRE (SHA-256-перевірений) разом із signal-cli. Це
  drop-in заміна `SignalCli.Runtime`: достатньо підключити пакет — `Config.JavaExecutable`
  автоматично резолвиться у `jre/bin/java[.exe]` (новий метод `Config.ResolveBundledJava`),
  системна Java не потрібна. Перевірено наскрізно на Windows (signal-cli стартує під
  вбудованим JRE, JSON-RPC працює).
- **Важливо:** signal-cli 0.14.3 скомпільовано під **Java 25** (class-file version 69.0),
  тож JVM-режим тепер потребує **JDK/JRE 25+** (раніше в документації значилось 21+).
- signal-cli оновлено до **v0.14.3** із перевіркою цілісності завантаження (SHA-256).
- Граційне завершення signal-cli: ізоляція в окремій групі процесів (Windows, .NET 10)
  + конфігурований таймаут `Config.StopTimeoutSeconds` перед примусовим завершенням.
- Кросплатформний пошук Java (Windows/Linux/macOS): `JAVA_HOME` → `PATH`.
- `CLAUDE.md`, `.editorconfig` та аналізатори для якості коду; бібліотека warning-clean
  (`TreatWarningsAsErrors`).

### 🐛 Виправлено
- **Приватність:** тіла повідомлень, номери та вкладення більше не логуються вище за `Trace`.
- **Втрата подій:** одне повідомлення з текстом + вкладенням тепер піднімає всі відповідні
  реактивні події (раніше — лише першу).
- **Path traversal** у тимчасових файлах вкладень (`AttachmentEntry`).
- **Безпека аргументів процесу:** перехід на `ProcessStartInfo.ArgumentList`.
- Локаленезалежні назви стилів тексту (`ToUpperInvariant`).
- Уніфіковано стан процесу: `ProcessStateManager` — єдине джерело істини.

### 🔧 Інше
- `Newtonsoft.Json` 13.0.1 → видалено; `Microsoft.Extensions.*` → 10.0.0.
- `ProcessWrapper` використовує `Process.WaitForExitAsync`.
