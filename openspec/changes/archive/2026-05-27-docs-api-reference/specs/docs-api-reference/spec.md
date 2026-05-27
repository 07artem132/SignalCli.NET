# Spec — docs-api-reference

## ADDED Requirements

### Requirement: Every public RPC method SHALL be documented in `docs/api/<category>.md`

Документація публічного API SHALL жити під `docs/api/` у файлах-категоріях. Cross-domain split (не per-interface) — щоб майбутні non-interface API (extension-методи, builders, EventArgs records) мали природне місце поряд із relevant interface-методами.

Покриваються 9 target interfaces:
- `SignalCli.Interfaces.Signal.ISignalMessage` → `docs/api/messaging.md`
- `SignalCli.Interfaces.Signal.ISignalAccounts` → `docs/api/accounts.md`
- `SignalCli.Interfaces.Signal.ISignalDevices` → `docs/api/devices.md`
- `SignalCli.Interfaces.Signal.ISignalGroups` → `docs/api/groups.md`
- `SignalCli.Interfaces.Signal.ISignalContacts` → `docs/api/contacts.md`
- `SignalCli.Interfaces.Signal.ISignalEventService` → `docs/api/events.md`
- `SignalCli.Interfaces.Signal.ISignalResources` → `docs/api/resources-stickers.md`
- `SignalCli.Interfaces.Signal.ISignalStickers` → `docs/api/resources-stickers.md`
- `SignalCli.Interfaces.SignalCli.ISignalCliClient` → `docs/api/resources-stickers.md`

Плюс 3 DI extensions — у `docs/api/di-options.md`:
- `SignalCli.Extensions.ServiceCollectionExtensions.AddSignalCli` (обидва overload'и: `Action<SignalCliOptions>?` + `IConfiguration`)
- `SignalCli.Extensions.ServiceCollectionExtensions.AddSignalCliWithBundledRuntimeDefaults`
- `SignalCli.Extensions.ServiceCollectionExtensions.AddSignalEvents`

Глибина per метод — **середня** per CLAUDE.md §0.5 anti-hallucination protocol:
- C# signature у backtick fenced block (синтаксис відповідає `Tests/SignalCli.Tests/RegressionGuards/SignalCli.public-api.txt` baseline).
- 1-3 речення опису.
- signal-cli source citation за патерном `<X>Command.java @ <commit-sha>` (зараз — `bda4e7fc`).
- Хоча б один code-приклад що компілюється проти current 4.9.0 API.
- Перелік типізованих винятків якщо метод їх кидає (`RateLimitException` `-5`, `UntrustedIdentityException` `-4`, `CaptchaRequiredException` `-6`, `GroupAdminRequiredException` `-1` + "admin", `InvalidOperationException` для destructive-gated, etc.).

Extended worker-приклад з device-link flow SHALL жити окремо у `docs/examples/worker-auto-reply.md`. `docs/README.md` SHALL існувати як таблиця-покажчик до всіх інших docs/-файлів.

README.md SHALL посилатися на ці файли через purpose-built section "Документація API" зі зведеною таблицею. README's quick-start SHALL залишитися ≤ 30 рядків коду (consumer-facing, NuGet teaser); README SHALL NOT дублювати full per-method docs.

#### Scenario: Кожен публічний метод 9 інтерфейсів задокументований

- **GIVEN** будь-який публічний метод `M` на одному з 9 target-interface'ів
- **WHEN** `grep -l "$M" docs/api/*.md` запускається
- **THEN** результат не порожній (хоча б один файл згадує `M`)
- **AND** згадка не випадкова false-positive — або signature у fenced code-block, або згадка у prose з backtick'ах

#### Scenario: DI extensions задокументовані у `docs/api/di-options.md`

- **GIVEN** методи `AddSignalCli` (×2 overload'и), `AddSignalCliWithBundledRuntimeDefaults`, `AddSignalEvents` на `ServiceCollectionExtensions`
- **WHEN** читається `docs/api/di-options.md`
- **THEN** файл містить signature і ≥ 1 code-приклад використання для кожного з 4 методів
- **AND** файл містить повну таблицю `SignalCliOptions` properties (≥ 16 рядків — `AppHome`, `LibDirectory`, `JavaExecutable`, `SignalCliExecutable`, ... до `EnableDestructiveOperations` + `EnvironmentVariables`)

#### Scenario: README зменшується, але quick-start залишається self-contained

- **GIVEN** README.md після цього change
- **WHEN** `wc -l README.md` запускається
- **THEN** результат < 500 рядків (down from 586 baseline)
- **AND** quick-start section (`## 🚦 Швидкий старт за 30 рядків`) має ≤ 30 рядків fenced C# code
- **AND** README містить нову section `## 📚 Документація API` з таблицею-посиланням на ≥ 8 `docs/api/*.md` файлів
- **AND** README НЕ містить розділ "🧩 Інтерфейси бібліотеки" (переміщено) і НЕ містить розділ "📝 Розширений приклад — worker з авто-відповіддю" (переміщено)

#### Scenario: signal-cli source citations preserved verbatim

- **GIVEN** кожен метод задокументований у `docs/api/*.md`
- **WHEN** прочитати citation у `docs/api/<file>.md`
- **THEN** citation посилається на той самий `<X>Command.java` файл і той самий commit SHA (`bda4e7fc`) що цитується у XMLDoc цього методу у `src/SignalCli/Interfaces/Signal/*.cs`
- **AND** жоден citation не вигаданий (anti-hallucination per CLAUDE.md §0.5)

### Requirement: `DocsApiCoverageTests` (RG09) SHALL pin docs-vs-code coverage at build time

Новий regression-guard test `Tests/SignalCli.Tests/RegressionGuards/DocsApiCoverageTests.cs` SHALL використовувати reflection для enumeration публічних методів на 9 target-interfaces + 3 named extension methods, AND asserts кожен зустрічається substring-match у хоча б одному `docs/api/*.md` файлі. При додаванні нового публічного методу — RG09 fails build допоки author не оновить відповідний docs-файл.

Тест SHALL виключити з coverage перевірки:
- Property accessors (`IsSpecialName == true` — `get_X` / `set_X` для `IObservable<T>`-property'ів на `ISignalEventService`).
- Методи inherited з `Microsoft.*` namespaces — конкретно `StartAsync`/`StopAsync` від `IHostedService` на `ISignalEventService`.

Тест SHALL приєднатись до regression-guards table у CLAUDE.md як **RG09** з file-path і опис.

#### Scenario: RG09 додає 1 fact до тестової бази

- **GIVEN** post-merge стан після цього change
- **WHEN** `dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj --filter "Category!=E2E"` запускається
- **THEN** total passed ≥ 504 (503 baseline + RG09 × 1 fact)
- **AND** filter `FullyQualifiedName~DocsApiCoverageTests` ловить 1 fact `EveryPublicApiMethod_IsMentionedInDocsApi` що passes

#### Scenario: RG09 ловить omission нового публічного методу

- **GIVEN** hypothetical новий метод `SendVoiceMessageAsync(VoiceMessageOptions opts, ...)` додано до `ISignalMessage` БЕЗ оновлення `docs/api/messaging.md`
- **WHEN** `dotnet test` запускається
- **THEN** RG09 fails з failure-message що містить точно `ISignalMessage.SendVoiceMessageAsync`
- **AND** жоден інший test не fails через цю omission

#### Scenario: RG09 виключає inherited infrastructure методи

- **GIVEN** `ISignalEventService` inherits `StartAsync`/`StopAsync` from `Microsoft.Extensions.Hosting.IHostedService`
- **WHEN** RG09 enumerate'ить методи `ISignalEventService`
- **THEN** `StartAsync` / `StopAsync` НЕ потрапляють у coverage-check list
- **AND** `docs/api/events.md` НЕ зобов'язаний згадувати ці методи (вони — інфраструктура hosting'у, не Signal RPC surface)

#### Scenario: CLAUDE.md "Regression guards" таблиця має рядок RG09

- **GIVEN** post-merge `CLAUDE.md`
- **WHEN** `grep -n "^| RG09 " CLAUDE.md` запускається
- **THEN** ≥ 1 рядок повертається
- **AND** рядок містить `RegressionGuards/DocsApiCoverageTests.cs` як file-path
- **AND** рядок містить опис що згадує "docs/api/" + "drift" як ключові слова

### Requirement: Docs convention SHALL bе документована у `docs/README.md`

`docs/README.md` (індексний файл) SHALL існувати і SHALL містити:

- Таблицю-покажчик з one-line summary для кожного `docs/api/*.md` і `docs/examples/*.md` файлу.
- Convention для нових docs — посилання на CLAUDE.md §0.5 (depth) + RG09 (coverage invariant).
- Cross-link до existing `docs/cloud-development.md` (operational topic).

#### Scenario: docs/README.md як authoritative index

- **GIVEN** `docs/README.md` post-merge
- **WHEN** файл читається
- **THEN** він містить hyperlinks до всіх 8 файлів `docs/api/*.md` + до `docs/examples/worker-auto-reply.md` + до `docs/cloud-development.md`
- **AND** файл явно згадує RG09 як enforcement-механізм для "кожен публічний метод задокументований"
