# Per-category API reference under `docs/api/` + RG09 anti-drift guard

## Why

README.md (586 рядків після `claude-md-rules-restructure`) пробує одночасно бути: (a) NuGet teaser-перші-200-chars, (b) quick-start, (c) повним interface reference, (d) extended-worker-example. Це конфлікт audience'ів. CLAUDE.md `.claude/rules/openspec-workflow.md § README voice + drift rules` явно каже: "README is consumer-facing; CLAUDE.md is contributor-facing. Different audience, different voice." А `audit-debt.md` фіксує саме той клас регресій що нас вкусив у v2.1 cleanup: "Drift discovered during audit v2.1 cleanup: README had 16 broken API sites that survived three releases (3.0.0 → 4.0.0 → 4.0.1) because no regression-guard pins README content."

Бібліотека з 4.9.0 експонує **54 публічних RPC-методи** на 8 `ISignal*` facade-інтерфейсах (см. `Tests/SignalCli.Tests/RegressionGuards/SignalCli.public-api.txt`). README згадує лише 3 (`SendTextMessageAsync`, `SendAttachmentAsync`, `SendStickerAsync`); решта 51 не задокументовані ніде поза XML-doc'ами всередині .cs-файлів. Consumer'ів які роблять `dotnet add package SignalCli.NET` це залишає сліпими.

Окрема `docs/` (вже існує — `docs/cloud-development.md` живе там з 4.0.0) — Anthropic'ів канонічний шлях: link from README, deep dive у per-topic file'ах. Той самий патерн що ми застосували у `claude-md-rules-restructure` для CLAUDE.md.

**Anti-drift guard** обовʼязковий — інакше docs дрейфуватимуть від коду точно так як README дрейфував до v2.1 cleanup'у. CLAUDE.md `audit-debt.md § Missing regression guard для нового патерну (→ NF-002)` каже прямо: "кожен новий 'Established patterns' bullet МУСИТЬ мати regression guard. Якщо додаєш новий патерн — одразу додай guard." Тут ми додаємо `docs/api/` як новий patern → потрібен guard.

## What Changes

Дві paired capabilities — content (docs files) і enforcement (regression guard). Розділення мінімізує coupling якщо одна частина rolls back під час review.

- **`docs-api-reference`** — створення content under `docs/`:
  - 8 файлів `docs/api/<category>.md`, по одному на functional area: messaging, accounts, devices, groups, contacts, events, resources-stickers, di-options. Глибина — **середня** per CLAUDE.md §0.5 protocol: для кожного публічного методу signature, опис, ключові параметри, винятки (типізовані), приклад, signal-cli source citation (`<X>Command.java @ <commit-sha>`).
  - 1 файл `docs/examples/worker-auto-reply.md` — переміщений з README §369-457 без content edits.
  - 1 файл `docs/README.md` — індекс із cross-reference таблицею.
  - **README.md** скорочується: розділ "Інтерфейси бібліотеки" (рядки 181-275 у поточній версії) → замінений на таблицю-покажчик до `docs/api/`; розділ "Розширений приклад" (рядки 369-457) → пере́їжджає у `docs/examples/`. README зменшиться з 586 → ≈ 420 рядків.
  - **CLAUDE.md** не редагується для цієї capability — content усе ще під `.claude/rules/openspec-workflow.md § README voice + drift rules`, який вже декларує "README is consumer-facing".
- **`docs-coverage-guard`** — додаткова regression-guard infrastructure:
  - Новий тест `Tests/SignalCli.Tests/RegressionGuards/DocsApiCoverageTests.cs` (joins regression-guards як **RG09**) — reflectively enumerate'ить публічні методи 9 інтерфейсів (8 `ISignal*` facades + `ISignalCliClient`) + 3 extension-методи (`AddSignalCli`, `AddSignalCliWithBundledRuntimeDefaults`, `AddSignalEvents`), asserts кожен згаданий хоча б у одному `docs/api/*.md` файлі через substring-match.
  - **CLAUDE.md** "Regression guards" table — додаємо рядок RG09.
  - **Test count baseline** піднімається 503 → 504 (для всіх 9 інтерфейсів + extensions сумарно 1 нова facts).

Capability'и можуть mergeт'ися разом — `docs-api-reference` без guard стане drift-fuel за 6 місяців; `docs-coverage-guard` без content fail'ить на першому build. Тому в одній PR.

## Capabilities

### New Capabilities

- **`docs-api-reference`**: документація публічного API SHALL жити під `docs/` з one-file-per-category convention. Кожен публічний метод на `ISignalMessage` / `ISignalAccounts` / `ISignalDevices` / `ISignalGroups` / `ISignalContacts` / `ISignalEventService` / `ISignalResources` / `ISignalStickers` / `ISignalCliClient` SHALL бути задокументований у відповідному `docs/api/<category>.md` файлі з: (a) C# signature у backtick fenced block, (b) описом (1-3 речення), (c) cited signal-cli source path + commit-SHA, (d) хоча б одним робочим code-прикладом. DI extensions (`AddSignalCli` × 2 overload'и + `AddSignalCliWithBundledRuntimeDefaults` + `AddSignalEvents`) живуть у `docs/api/di-options.md`. Extended console-worker example з device-link flow живе у `docs/examples/worker-auto-reply.md`. README.md SHALL посилатись на ці файли через таблицю-покажчик і SHALL NOT дублювати їх content інакше ніж quick-start (≤ 30 рядків коду, як вже визначено `.claude/rules/openspec-workflow.md § README voice + drift rules`).

- **`docs-coverage-guard`**: regression-guard `DocsApiCoverageTests` (RG09) SHALL reflectively enumerate'ити публічні методи на 9 цільових інтерфейсах і 3 названих `ServiceCollectionExtensions` метода, asserts кожен зустрічається через substring-match у хоча б одному `docs/api/*.md` файлі. Тест SHALL виключати: (a) inherited методи з `Microsoft.*` namespaces (e.g. `IHostedService.StartAsync` на `ISignalEventService`), (b) property accessor'и (`get_X`/`set_X`). При додаванні нового публічного методу — author MUST оновити відповідний `docs/api/*.md` або тест fail'ить build. RG09 row SHALL з'явитись у CLAUDE.md "Regression guards" таблиці поряд із R01-R04+RG05-RG08.

### Modified Capabilities

- **None.** README's quick-start, install instructions, FAQ, Health-checks, OpenTelemetry, Migration table, License — все залишається. Existing regression-guards (R01-R04, RG05-RG08) — pin'ять code/CLAUDE.md invariants; не торкаються нових `docs/api/` файлів. RG09 — додаткова, не replacement.

## Out of scope

- **Перепис signal-cli source citations.** Citations беруться з existing XMLDoc'ів у `src/SignalCli/Interfaces/Signal/*.cs` (вже встановлені у `signal-cli-api-coverage` change через §0.5 anti-hallucination protocol). Цей change — content-preserving для citation'ів.
- **AST-based code-block compilation guard.** Розглядалось в `audit-followup-2026` proposal'ах, було rejected ("infrastructure cost > value; PR-time review is the enforcement"). Substring-match через method-name — мінімальний guard який ловить omission, не synatax-drift у sample коді. Якщо метод перейменовано — RG09 ловить; якщо приклад використовує stale fluent-builder method — людський review.
- **Auto-sync XML doc → markdown.** DocFX / .NET tool integration — окремий future change; зараз — manual stewardship з RG09 як floor.
- **Migration від 4.x examples.** Цей change документує current 4.9.0 API; не пише historical migration guides (вони в CHANGELOG).
