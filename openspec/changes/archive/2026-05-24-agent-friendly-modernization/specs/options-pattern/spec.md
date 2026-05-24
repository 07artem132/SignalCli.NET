## ADDED Requirements

### Requirement: Typed options with fail-fast validation
Конфігурація бібліотеки SHALL надаватися через `IOptions<SignalCliOptions>`, реєструватися з `AddOptions<SignalCliOptions>().ValidateDataAnnotations().Validate(...).ValidateOnStart()`, і SHALL валідуватися на старті хоста, а не на момент першого використання.

#### Scenario: Missing required AppHome surfaces on host start
- **GIVEN** споживач викликає `AddSignalCli(o => { /* AppHome не заданий */ })`
- **WHEN** хост запускається через `host.StartAsync()`
- **THEN** виклик фейлиться з `OptionsValidationException`, що містить ім’я властивості `AppHome` і причину `Required`
- **AND** жоден signal-cli процес не запускається

#### Scenario: Out-of-range MaxRestartAttempts is rejected
- **GIVEN** споживач задає `MaxRestartAttempts = -5`
- **WHEN** хост запускається
- **THEN** виклик фейлиться з `OptionsValidationException`, що згадує `[Range]` обмеження

#### Scenario: Either JavaExecutable or SignalCliExecutable is required
- **GIVEN** обидва `JavaExecutable` і `SignalCliExecutable` порожні
- **WHEN** хост запускається
- **THEN** виклик фейлиться з `OptionsValidationException` і повідомленням, що пояснює, який із двох параметрів треба задати

### Requirement: Immutable, init-only options surface
`SignalCliOptions` SHALL бути immutable після конструювання — усі властивості мають бути `init`-only, без публічних сетерів, щоб гарантувати, що сервіси читають той самий знімок упродовж життя процесу.

#### Scenario: Options cannot be mutated post-build
- **GIVEN** контейнер DI зрезолвив `IOptions<SignalCliOptions>`
- **WHEN** код спробує присвоїти значення будь-якій властивості
- **THEN** компіляція не пройде (init-only) ; runtime-mutation неможливий

### Requirement: Backward-compatible Config shim
Старий публічний `Config` SHALL лишитися як `[Obsolete]` адаптер у мажорному релізі `2.x`: `AddSignalCli(Action<Config>?)` мапить старий конфіг на новий `SignalCliOptions` без зміни поведінки. Шим SHALL бути видалений у `3.0`.

#### Scenario: Legacy AddSignalCli(Action<Config>) still works
- **GIVEN** існуючий споживацький код викликає `services.AddSignalCli(cfg => { cfg.AppHome = "/x"; cfg.LibDirectory = "lib"; cfg.JavaExecutable = "java"; })`
- **WHEN** хост запускається
- **THEN** він стартує успішно, з тими ж значеннями в `SignalCliOptions`
- **AND** компілятор показує `[Obsolete]` warning на цьому виклику

### Requirement: Single options snapshot per service
Внутрішні сервіси SHALL читати `IOptions<SignalCliOptions>.Value` один раз у конструкторі й кешувати знімок у `private readonly` полі. Це гарантує consistent view впродовж runtime сервісу і відповідає immutable-контракту.

#### Scenario: Sevices observe consistent options
- **GIVEN** `IOptions<SignalCliOptions>` зарезолвлено
- **WHEN** будь-який сервіс читає `Value` повторно
- **THEN** він отримує те саме посилання, що й при першому читанні (об’єкт immutable)
