# fix-startlink-wire-shape

## Why

E2E-проба `startLink` через `SignalCliNet.WsRpcServer` (2026-07-16) повернула
`"result": {}` — RPC начебто успішний, але порожній. Корінь: `StartLinkResponse`
десеріалізувався у `DeviceLinkUri = null` тихо, без винятку й без логу.

Механізм — комбінація двох фактів:

1. **Wire-поле camelCase.** signal-cli 0.14.3 віддає результат `startLink` як
   `{"deviceLinkUri":"sgnl://..."}`. §0.5 cite-and-read (читав, не лише цитував):
   `org.asamk.signal.commands/StartLinkCommand.java:42 @ v0.14.3` (`c554e5c`) —
   `private record JsonLink(String deviceLinkUri) {}`, записується через
   `jsonWriter.write(new JsonLink(deviceLinkUri.toString()))`.
2. **Case-sensitive source-gen контекст.** `SignalJsonContext` не має
   `PropertyNamingPolicy` і працює case-sensitive. `record StartLinkResponse(string DeviceLinkUri)`
   **не мав** `[JsonPropertyName]`, тож PascalCase-властивість не збіглася з
   camelCase-полем на wire → `System.Text.Json` присвоїв `null` (пропущене поле),
   без помилки.

Це той самий клас багу, що вже виправляли для `FinishLinkResponse.number` та
`SubscribeReceiveResponse.id` (див. `.claude/rules/obsolete-shims.md` — "lowercase
wire shape → PascalCase property + `[JsonPropertyName]`"), але `StartLinkResponse`
його прогавив. `JsonContextRegistrationTests` (R01) не ловить це: тип **зареєстрований**
у контексті — просто без per-property annotation.

## What Changes

- **`fix-startlink-wire-shape`** — production fix:
  - `StartLinkResponse` отримує `[property: JsonPropertyName("deviceLinkUri")]`
    (+ `using System.Text.Json.Serialization;`), дзеркалячи `FinishLinkResponse`.
    Публічна поверхня незмінна — property лишається `DeviceLinkUri` (PascalCase);
    міняється лише wire-мапінг. Baseline `SignalCli.public-api.txt` без diff'у.
  - XMLDoc несе §0.5-цитату upstream-джерела.

- **`wire-shape-annotation-guard`** — durable-артефакт (RG10):
  - `RegressionGuards/WireShapeAnnotationTests.cs` — reflection-гвардія: кожна
    публічна властивість кожного DTO, зареєстрованого у `SignalJsonContext`,
    МУСИТЬ нести явний `[JsonPropertyName]` (або `[JsonIgnore]`). Wrapper-record'и
    з власним `[JsonConverter]` на типі звільнені (їхню wire-форму визначає
    конвертер). Це закриває цілий клас silent-null-багів, а не одну властивість.
  - `Serialization/DeviceLinkingSerializationTests.cs` — пінить обидві wire-форми
    device-linking (`startLink` `deviceLinkUri`, `finishLink` `number`) через
    продакшн-шлях `SignalJsonContext.Default`.

## Impact

- Тести: 506 → 509 (RG10 +1, DeviceLinking +2).
- Версія: 4.10.0 → 4.10.1 (patch); CHANGELOG `## [4.10.1]` у тому ж коміті.
- CLAUDE.md: RG10 рядок у таблиці Regression guards; unit-test floor → ≥ 509.
- Docs: `docs/api/devices.md` `StartLinkAsync` — узгоджено URI-приклад
  (`sgnl://linkdevice?...`, було inconsistent `tsdevice:/?...`) + додано wire-note.

## Out of scope

- Зміна публічного імені property (`DeviceLinkUri` лишається — це не breaking fix).
- Додавання `PropertyNamingPolicy` до контексту. Явні `[JsonPropertyName]` —
  свідомий вибір: wire-контракт видимий на місці, а RG10 його форсить. Global
  naming policy приховала б контракт і не покрила б поля, чиє wire-ім'я не є
  простою camelCase-трансформацією PascalCase (напр. `number`, `id`).
