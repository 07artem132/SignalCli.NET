# api-coverage-audit-followup

## Why

Post-merge code-review (2026-05-25, після landing `signal-cli-api-coverage` 4.1.0 → 4.9.0) виявив 5 findings — один HIGH, один MEDIUM, три LOW. Кожен потребує закриття per `.claude/rules/audit-debt.md` § "Коли audit listує HIGH/MEDIUM finding": fix без regression-guard'у лишає одну файлову правку, fix з guard'ом дає permanent invariant.

Findings (від найвищого impact'у):

1. **🔴 HIGH — `IdentityChangedException` dead-throw API.** Тип у public-API baseline з 4.1.0, advertised у `<exception cref>` на [ISignalMessage.SendReactionAsync](src/SignalCli/Interfaces/Signal/ISignalMessage.cs:162), має type-hierarchy тести — **але жодне місце не кидає його**. Upstream verification ([SendMessageResultUtils.java:60](file:///C:/Users/ivank/Нова%20папка/signal-cli/src/main/java/org/asamk/signal/util/SendMessageResultUtils.java#L60) @ `bda4e7fc`) показує: signal-cli має ЄДИНУ fixed-string помилку `"Failed to send message due to untrusted identities"` для всіх `-4` випадків; жодного protocol-level distinguisher first-contact vs re-install не існує. Consumer'и які пишуть `catch (IdentityChangedException)` per документації — ніколи не спіймають нічого.

2. **🟡 MEDIUM — `JsonPayment.Receipt` non-nullable, але wire може delivery'ати null.** [Envelope.cs:155](src/SignalCli/Models/Signal/Envelope.cs:155) декларує `byte[] Receipt` non-nullable; STJ source-gen для reference-type не enforce'ить NRT; malformed wire envelope з `"receipt": null` присвоює `null` у non-nullable property, consumer'и читають `.Length` → NRE.

3. **🟡 LOW — `SignalEventService.OnNotificationReceived` boilerplate.** 13 near-identical dispatch branches (6 pre-existing + 7 з Wave 7b у `signal-cli-api-coverage`). Refactor у generic helper стискує до one-liners.

4. **🟡 LOW — `CaptchaRequiredException` dispatch-тест відсутній.** Asymmetry з `GroupAdminRequiredException` (positive + negative dispatch tests existing) — single Captcha positive-case test закриває gap.

5. **🟡 LOW — Version-bump checklist у [.claude/rules/signal-cli-protocol.md](.claude/rules/signal-cli-protocol.md) не згадує exception-substring stability.** `JsonRpcClient.cs:511-513` робить substring-match `Contains("admin", OrdinalIgnoreCase)` для `GroupAdminRequiredException` — якщо upstream змінить wording, typed exception silently degrade'не до base. Чек-лист `<SignalCliVersion>`-bump'у має нагадувати re-grep'ати upstream `Group*Command.java` на load-bearing substring.

Цей change оформляє всі 5 у одну minor release **4.10.0** (non-breaking) з one OpenSpec capability per finding.

## What Changes

5 capabilities, всі додаються до 4.10.0:

| # | Capability | LOC src | LOC test | Files | Risk |
|---|---|---|---|---|---|
| 1 | `identity-changed-deprecation` | ~15 | 0 | 2 (.cs + .md) | low (Obsolete-marker, не delete) |
| 2 | `json-payment-receipt-nullable` | ~3 | ~10 | 1 + baseline | low (Wave-7b shape, no prior users) |
| 3 | `event-dispatch-refactor` | −120/+30 | 0 | 1 | low (internal refactor) |
| 4 | `captcha-dispatch-test` | 0 | ~10 | 1 | none (test-only) |
| 5 | `protocol-checklist-amend` | 0 | 0 | 1 (.md) | none (doc) |

Жодних breaking changes. `IdentityChangedException` лишається у API до 5.0 per [.claude/rules/obsolete-shims.md](.claude/rules/obsolete-shims.md) one-major-grace convention.

## Capabilities

### Modified Capabilities

- `typed-rpc-errors` *(originally archived у `signal-cli-protocol-alignment`, розширено у `signal-cli-api-coverage`)*:
  - `IdentityChangedException` отримує `[Obsolete("...; will be removed in 5.0.")]` атрибут із чесним explanation'ом про upstream-level non-distinction; XMLDoc переписано.
  - `JsonRpcClient.InvokeMethodAsync` exception-mapping switch отримує positive-case тест для `CaptchaRequiredException` (code -6 → typed exception). Існуюча dispatch-логіка не змінюється.

- `event-decoding-expansion` *(archived у `signal-cli-api-coverage`)*:
  - `SignalEventService.OnNotificationReceived` 13 dispatch-branches уніфіковані через generic helper. Public behavior незмінне; existing dispatch tests + RG06 (`EventApiSymmetryTests`) гарантують no-regression.

- `messaging-interactive` *(archived у `signal-cli-api-coverage`)*:
  - `<exception cref="IdentityChangedException">` прибрано з ISignalMessage XMLDoc (cite-but-don't-throw fix).

### New Capabilities

- `json-payment-receipt-nullable`: `JsonPayment.Receipt` SHALL be `byte[]?` instead of `byte[]` to honor upstream's permissive serialization (signal-cli's Java does not enforce non-null on the `receipt` field). Serialization tests SHALL cover `receipt: null` AND missing-`receipt` envelope cases.

- `protocol-checklist-amend`: [.claude/rules/signal-cli-protocol.md](.claude/rules/signal-cli-protocol.md) SHALL include an 8th pinned fact: "No upstream distinction between first-contact-unknown and re-installed identity at JSON-RPC layer" + version-bump checklist SHALL include "re-grep upstream for load-bearing exception-substrings (`\"admin\"` for `GroupAdminRequired`)".

## Out of Scope

- **Adding `IdentityChangedException` dispatch via `listIdentities` cross-reference** — would require client-side caching of trust-store state + cross-call coordination. Consumer-side concern, not wrapper-layer concern. If a consumer asks, file separate `client-side-trust-cache` OpenSpec change.
- **Deleting `IdentityChangedException` immediately** — one-major-grace shim convention applies (`.claude/rules/obsolete-shims.md`). Removal у 5.0 за окремим `deprecated-shim-removal-5.0` change.
- **Making refactor (#3) breaking** — public API залишається ідентичним; внутрішня структура `OnNotificationReceived` — implementation detail.
- **Wholesale rewrite of `JsonRpcClient` exception-dispatch** to use Strategy/Map pattern замість switch-statement — premature; 4 typed exceptions ще не заслуговують це. Revisit якщо доростемо до 8+.
