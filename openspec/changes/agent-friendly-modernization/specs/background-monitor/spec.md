## ADDED Requirements

### Requirement: Health monitor runs as BackgroundService
`SignalCliHealthMonitor` SHALL бути реалізованим через `Microsoft.Extensions.Hosting.BackgroundService` (не ручним `Task.Run`-циклом). Логіку очікування інтервалу SHALL виконувати `PeriodicTimer(interval, TimeProvider)`, а не `Task.Delay(interval, TimeProvider)` всередині `while`.

#### Scenario: Monitor cycles every interval under fake clock
- **GIVEN** `BackgroundService` запущено з `FakeTimeProvider` і `HealthCheckIntervalSeconds = 40`
- **WHEN** тест викликає `fakeTimeProvider.Advance(TimeSpan.FromSeconds(40))` 3 рази
- **THEN** `PingCliAsync` викликано рівно 3 рази

#### Scenario: One bad iteration does not stop the loop
- **GIVEN** `PingCliAsync` кидає `InvalidOperationException` у 2-й ітерації
- **WHEN** `fakeTimeProvider.Advance(TimeSpan.FromSeconds(40))` викликано ще раз
- **THEN** 3-тя ітерація відбувається; помилка 2-ї залогована
- **AND** жодних `UnobservedTaskException` не зафіксовано

#### Scenario: Stop cancels the next WaitForNextTickAsync
- **WHEN** `host.StopAsync()` викликано в момент між тіками
- **THEN** `BackgroundService.ExecuteAsync` завершується протягом ≤ 100 мс (без чекання повного інтервалу)

### Requirement: Restart-window timer goes through TimeProvider
`SignalCliHostedService.ScheduleRestartWindowReset` SHALL використовувати `TimeProvider.CreateTimer(...)` для відлічення вікна стабільності, а не `Task.Run(async () => await Task.Delay(window, token))`. `TimeProvider` SHALL бути inject-ovaним полем сервісу (за замовчуванням `TimeProvider.System`).

#### Scenario: Restart counter resets under fake clock
- **GIVEN** `_restartCount = 2`, процес у стані `Running`, `RestartWindowSeconds = 60`
- **WHEN** тест викликає `fakeTimeProvider.Advance(TimeSpan.FromSeconds(60))`
- **THEN** `_restartCount == 0`
- **AND** жоден `Task.Delay` із реальним wall-clock не задіяний

#### Scenario: Cancel on next restart
- **GIVEN** таймер вікна стабільності активний
- **WHEN** відбувається наступний рестарт до кінця вікна
- **THEN** старий таймер скасовано (новий callback не виконається)
- **AND** новий таймер стартує лише при наступному переході в `Running`

### Requirement: No wall-clock dependency in monitor/restart tests
Жоден тест із `Tests/SignalCli.Tests/SignalCliHealthMonitor/` або `Tests/SignalCli.Tests/SignalCliHostedService/` не SHALL чекати на реальний інтервал `Task.Delay(>10ms)`. Часом керує `FakeTimeProvider`.

#### Scenario: Test suite runs without wall-clock waits
- **WHEN** запускається `dotnet test` із Diagnostic logger
- **THEN** жоден тест із цих папок не має `Task.Delay` із інтервалом > 10 мс
