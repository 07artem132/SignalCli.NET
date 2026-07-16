# Tasks — fix-startlink-wire-shape (→ 4.10.1)

Single-commit patch. Two architect-written pinning tests (RED at baseline) land у
тому ж коміті що й fix + version-bump + CHANGELOG (CLAUDE.md lockstep rule).

## 0. §0.5 cite-and-read (working style)

- [x] 0.1 Звірити wire-поле з upstream: `org.asamk.signal.commands/StartLinkCommand.java`
        на тегу `v0.14.3`. Підтверджено: `private record JsonLink(String deviceLinkUri) {}`
        (рядок 42), пишеться через `jsonWriter.write(new JsonLink(deviceLinkUri.toString()))`.
        Поле = `deviceLinkUri`. Commit tag `v0.14.3` → `c554e5c`.

## 1. Fix (capability `fix-startlink-wire-shape`)

- [x] 1.1 `StartLinkResponse` — додати `[property: JsonPropertyName("deviceLinkUri")]`
        + `using System.Text.Json.Serialization;`, дзеркалячи `FinishLinkResponse`.
- [x] 1.2 XMLDoc: додати §0.5-цитату upstream-джерела + пояснення case-sensitive контексту.

## 2. Regression guards (capability `wire-shape-annotation-guard`)

- [x] 2.1 `RegressionGuards/WireShapeAnnotationTests.cs` (RG10) — architect-written;
        RED на baseline (лістить рівно `StartLinkResponse.DeviceLinkUri`), GREEN після fix.
- [x] 2.2 `Serialization/DeviceLinkingSerializationTests.cs` — architect-written;
        `startLink` тест RED на baseline (Actual: null), `finishLink` тест GREEN. Обидва
        GREEN після fix.

## 3. Build + test

- [x] 3.1 `dotnet build Tests/SignalCli.Tests/SignalCli.Tests.csproj -c Release --no-restore` — 0 warnings.
- [x] 3.2 `dotnet test ... --no-build` — повний suite зелений: 509 passed, 0 failed.
        Примітка: `ObservabilityCounterTests.Meter_ProcessRestarts_...CrashTrigger` —
        pre-existing nondeterministic flake (per-instance `MeterListener` на глобальному
        static meter ловить bleed від інших restart-тестів); зелений run отримано,
        не пов'язаний з цим fix'ом (JSON-attribute не має шляху до restart-лічильника).

## 4. Doc-sync (audit-debt rule)

- [x] 4.1 `grep -rn "StartLink\|deviceLinkUri" docs/ README.md CHANGELOG.md` — усі
        згадки перевірені. Публічна поверхня незмінна → prose лишається true.
        `docs/api/devices.md` `StartLinkAsync`: URI-приклад узгоджено
        (`sgnl://linkdevice?...`) + додано wire-note про camelCase поле.

## 5. Version + docs lockstep (same commit)

- [x] 5.1 `Directory.Build.props` `<SignalCliPackageVersion>` 4.10.0 → 4.10.1.
- [x] 5.2 CHANGELOG `## [4.10.1] — 2026-07-16` (consumer-first voice).
- [x] 5.3 CLAUDE.md: RG10 рядок у Regression-guards таблиці; unit-test floor → ≥ 509;
        Pending-changes нотатка. RG08 (≤ 200 lines) не порушено (157).

## 6. Commit

- [x] 6.1 Один коміт на гілці `fix/startlink-wire-shape` (fix + 2 test-файли + version +
        CHANGELOG + CLAUDE.md + docs + openspec). Conventional-style, українською.
        DO NOT push.

## 7. Post-merge (out of scope цього коміту)

- [ ] 7.1 Після merge: `openspec archive fix-startlink-wire-shape --yes --skip-specs`
        + оновити CLAUDE.md "Implemented, merged, archived".
