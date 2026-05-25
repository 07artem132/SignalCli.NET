---
paths:
  - "openspec/**"
  - "CHANGELOG.md"
  - "CLAUDE.md"
---

# OpenSpec workflow + CHANGELOG + README voice

## Planning (OpenSpec)

This repo uses [OpenSpec](https://github.com/Fission-AI/OpenSpec) for change planning under `openspec/changes/`. For non-trivial work, create/extend a change (proposal → design → specs → tasks) and run `npx -y @fission-ai/openspec@latest validate <change> --strict` before implementing.

When you start a new material piece of work, create a new `openspec/changes/<change-name>/` directory with `proposal.md` / `design.md` / `tasks.md` / `specs/<capability>/spec.md`, mirror the structure of an archived change (e.g. `archive/2026-05-24-agent-friendly-modernization/`), and run `openspec validate <change> --strict` before implementing.

**Post-merge archive workflow (canonical):**

```bash
# 1. After PR merges to main:
git checkout main && git pull
# 2. Archive (uses today's date as prefix, --skip-specs matches repo pattern —
#    we do NOT maintain a top-level openspec/specs/ tree; spec content lives
#    inside each change directory and moves with it to archive/):
npx -y @fission-ai/openspec@latest archive <change-name> --yes --skip-specs
# 3. Commit the file moves:
git add -A && git commit -m "chore(openspec): archive <change-name> → YYYY-MM-DD"
# 4. Rebase against coverage-bot auto-commit, then push:
git pull --rebase origin main && git push origin main
# 5. Update root CLAUDE.md "Implemented, merged, archived" list to add the new
#    entry with archive-path pointer, in a follow-up commit.
```

`--skip-specs` is mandatory in this repo: previous changes never synced delta-specs to `openspec/specs/`, and switching now would create two sources of truth. Spec content is read from `openspec/changes/archive/<date>-<name>/specs/<capability>/spec.md` when referenced.

## CHANGELOG voice template

Кожен `## [X.Y.Z]` entry пишеться для **трьох аудиторій** у такому порядку пріоритету:

1. **Library consumers** (хто залежить від NuGet-пакету) — *що змінилось у МОЄМУ житті? Чи треба мені оновлюватись? Чи це мене зламає?* Це перші, що читають release notes на nuget.org.
2. **Contributors** (хто працює в репо) — *яка була underlying технічна зміна?*
3. **Future maintainers / AI agents** (читають цей список через 6 місяців) — *де file:line якорі для re-verify?*

### Правила per bullet

- **Починай кожен bullet з bold-claim'у user-facing мовою:**
  - ✅ "Якщо ти використовуєш X — онови Y разом, інакше runtime crash"
  - ✅ "Видалено: ось як мігрувати: `s/old/new/g`"
  - ✅ "Тепер працює без silent Kill — критичний bug з 1.0"
  - ❌ "Capability `xyz-fix-123` (NF-007 / G2)" — internal taxonomy, consumer'и bounce'нуть на першому абзаці
  - ❌ "Refactored `JsonRpcClient.cs:494` per audit v2.1 RG05 implementation" — meta-narrative про audit-процес, не про change

- **Потім 1–2 речення plain-language пояснення** *що / навіщо*. Acronyms expand'нуті на першому use.

- **Потім (опційно) технічна обгортка** в italics або parens для tracer'ів: file:line, method names, OpenSpec capability slug. **Cap: 2–3 lines max.** Walls of code-citations без "why this matters" framing'у — рефактори.

- **Internal IDs (`NF-XXX`, `RG05`, `T01`, `G4`, capability slugs)** — в кінці bullet'у, в italics-parens: `*(NF-003, RG07)*` або `*([openspec-name](openspec/changes/...))*`.

- **Не більше одного `#### Capability \`name\`` subheader'у per `###` секція.** Consumer'и які скан'ять CHANGELOG для impact'у потребують flat readable lists, не nested taxonomy. Якщо у тебе багато дрібних items в одній capability — згрупуй їх під ОДНИМ bold leading bullet'ом, з вкладеним списком plain-language sub-items.

### Example — bad vs good

❌ **Bad** (audit v2.0/v2.1 default style — те, що я писав initially у [4.0.2] before C-rewrite):

```
#### Capability `healthchecks-version-sync` (NF-003)
- **`SignalCli.NET.HealthChecks` версія більше НЕ хардкодиться — централізована в `Directory.Build.props`.**
  До 4.0.2 main lib був `4.0.1`, а adapter csproj мав хардкод `<Version>3.0.0</Version>` — divergent versions
  = `MissingMethodException` на першому health-check-probe у консумерах (adapter читає internal'и main lib
  через `[InternalsVisibleTo("SignalCli.HealthChecks")]`). Тепер обидва csproj читають
  `$(SignalCliPackageVersion)`...
```

✅ **Good** (those same facts, consumer-first voice):

```
- **Якщо ти використовуєш `SignalCli.NET.HealthChecks` — онови його разом з main package.**
  До 4.0.2 версії розійшлися (4.0.1 vs 3.0.0), і змішування пакетів давало `MissingMethodException`
  на першому health-check probe. Тепер обидва ідуть в lockstep — `4.0.2` для обох. Bug-class виключено:
  новий тест `VersionLockstepTests` ловить розбіжність версій до merge'у. *(NF-003, RG07)*
```

"Good" версія на ~20% коротша, але перші 12 слів кажуть consumer'у все що йому потрібно знати; технічна обгортка приходить як supporting context, не як wall.

### Коли в одній capability багато дрібних items

Use **single bold leading bullet + nested plain-language sub-items**, НЕ nested `####` capability headers per item:

```
- **Тестова hygiene tightened — 3 невидимих warnings зникли:**
  - Test csproj тепер відмовляється builder'итись при warnings (як main lib давно).
  - Виправлено 3 deadlock-prone `.GetAwaiter().GetResult()` у `SyncDisposeDuringCleanupTests`.
  - `Microsoft.Extensions.*` test packages bumped 9.0.0 → 10.0.0 (no breaking changes per MS Learn).
  *(NF-004, NF-005)*
```

### Чому це matter

NuGet.org підтягує перші ~200 символів CHANGELOG-секції як `<PackageReleaseNotes>`. Якщо там "Capability `xyz` (NF-007)" замість "Якщо ти використовуєш X — онови Y разом", consumer на nuget.org бачить шум і пропускає release-картку. Версії [4.0.0], [4.0.1], [4.0.2] переписано в цьому стилі задля консистенції; будь-яка майбутня версія дотримується того ж шаблону.

## README voice + drift rules

- **README is consumer-facing; CLAUDE.md is contributor-facing.** Different audience, different voice. README answers "what is this, do I want it, how do I use it?" — first 200 chars must hook (NuGet.org renders this as package teaser via `<PackageReadmeFile>`). CLAUDE.md answers "I'm editing the code, what must I not break?" — verbosity acceptable, internal IDs acceptable.

- **No internal IDs in README body** — `NF-003`, `RG05`, capability-slug references, audit-version mentions belong in CHANGELOG / OpenSpec / CLAUDE.md, NOT in README prose. **One exception**: the 4.0 migration `<details>` collapsible can reference capability slugs as historical migration anchors (consumer who's upgrading from 3.x benefits from "this is what we called the cleanup").

- **Quick-start must compile against current API verbatim.** Single copy-paste-working block, ≤30 lines, no deleted-type references (`Config`, `*Async`-suffix-less methods, `(Action<X>)` casts that existed only for disambiguation against removed overloads). Drift discovered during audit v2.1 cleanup: README had 16 broken API sites that survived three releases (3.0.0 → 4.0.0 → 4.0.1) because no regression-guard pins README content. A future RG for code-block compilation against current surface was considered + rejected (AST infrastructure cost > value; see proposal's "Out of scope"); PR-time review is the enforcement.

- **README-update PR-time triggers.** Re-check README and refresh examples when ANY of the following lands:
  - Change to public surface of `ISignalCliClient` / `ISignal*` / `ISignalEventService` interfaces → re-verify API-example signatures compile.
  - Change to `AddSignalCli*` extensions (new overload, removed overload, signature change) → re-verify DI setup snippets.
  - New top-level pattern shipped (event-kind, derived exception type, options field, new optional package like `SignalCli.NET.HealthChecks`) → consider "API capabilities" table addition + Quick-start / Extended-example mention.
  - `<SignalCliPackageVersion>` bump → re-check README's version mentions, migration tables, "TestBaseline ≥ N" claims.

- **Badges + NuGet pack pairing.** Badges MUST use absolute `https://raw.githubusercontent.com/<owner>/<repo>/main/.github/badges/*.svg` URLs — relative paths (`.github/badges/...`) render correctly on github.com but break on nuget.org / IDE previewers / third-party gallery sites (interpreted as hostname → broken `http://.github/...`). README ships in NuGet pack via `<PackageReadmeFile>README.md</PackageReadmeFile>` + `<None Include="..\..\README.md" Pack="true" PackagePath="\" />` on each packable csproj (`SignalCli.csproj` + `SignalCli.HealthChecks.csproj` — see `.claude/rules/csproj-build.md` § csproj/MSBuild conventions).
