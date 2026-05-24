## ADDED Requirements

### Requirement: All library logs go through [LoggerMessage] source generation
Усі виклики `ILogger` у бібліотеці (`src/SignalCli/`) SHALL виконуватися через `partial`-методи, згенеровані `[LoggerMessage]`. Прямі виклики `_logger.LogInformation/...("string template", args)` SHALL бути усунуті, окрім явно задокументованих винятків (наприклад, динамічно сформовані повідомлення в одному-двох діагностичних місцях).

#### Scenario: No raw LoggerExtensions calls in library code
- **WHEN** на коді `src/SignalCli/` запускається аналізатор CA1848
- **THEN** він не повідомляє про порушення

#### Scenario: Generated logging methods use stable EventIds
- **GIVEN** структура EventId за сервісами зафіксована у `design.md`
- **WHEN** перевіряється, що жоден EventId не дублюється між класами
- **THEN** перевірка проходить (кожен метод має унікальний EventId у своєму діапазоні)

### Requirement: Log scopes for subscription-bound notifications
`SignalEventService.OnNotificationReceived` SHALL обгортати обробку нотифікації в `ILogger.BeginScope` зі структурованими полями `SubscriptionId` і `Account`, щоб усі логи в межах однієї нотифікації успадковували контекст.

#### Scenario: Log entries inherit SubscriptionId scope
- **WHEN** нотифікація для `SubscriptionId=42, Account="+380…"` обробляється
- **THEN** усі лог-записи, що виникають усередині обробки, мають у structured properties `SubscriptionId=42` і `Account="+380…"`

### Requirement: Privacy guarantees preserved by generated logs
Source-generated логи SHALL зберігати існуючі privacy-вимоги з `address-audit-findings/logging-privacy`: тіла повідомлень, номери телефонів, вкладення йдуть тільки на `Trace`; `Information` логи фасадів містять лише лічильники.

#### Scenario: No PII above Trace
- **WHEN** запускається `PrivacyLoggingTests`
- **THEN** жоден `[LoggerMessage]` метод із `Level >= LogLevel.Debug` не містить у шаблоні плейсхолдерів для `Body`, `Phone`, `Attachment`, `Account` (на рівні `Information`+) або еквівалентних PII-полів

### Requirement: Privacy tests assert on EventIds, not text substrings
`PrivacyLoggingTests` SHALL перевіряти присутність/відсутність логів за конкретними `EventId`-ами, а не за підрядками шаблону повідомлення — це робить тести стійкими до невеликих змін текстів.

#### Scenario: EventId-based assertion survives wording changes
- **GIVEN** тест перевіряє, що `JsonRpcClientLog.SentRequest` (EventId=320) не з’являється вище `Trace`
- **WHEN** текст повідомлення цього методу змінюється
- **THEN** тест продовжує проходити (бо орієнтується на EventId)
