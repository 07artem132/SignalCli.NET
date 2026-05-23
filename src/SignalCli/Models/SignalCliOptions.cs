using System.ComponentModel.DataAnnotations;
using SignalCli.Models.SignalCli;

namespace SignalCli.Models;

/// <summary>
/// Типізована, immutable конфігурація бібліотеки SignalCli.NET для реєстрації
/// через <c>IOptions&lt;SignalCliOptions&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// D.1: властивості — звичайні <c>set</c>-сетери, бо Microsoft.Extensions.Options
/// мутує об'єкт через <c>Action&lt;TOptions&gt;.Configure</c>-делегат і
/// <c>Bind(IConfiguration)</c> після його конструювання через
/// <see cref="Activator.CreateInstance{T}"/>. Init-only тут був би непрактичним.
/// </para>
/// <para>
/// DataAnnotations (<see cref="RequiredAttribute"/>/<see cref="RangeAttribute"/>)
/// перевіряються на старті хоста через <c>OptionsBuilder.ValidateDataAnnotations()</c>
/// + кастомне правило в <c>Validate(...)</c>, плюс compile-time-валідатор
/// <c>SignalCliOptionsValidator</c> (D.9). Помилки видно одразу на <c>host.StartAsync()</c>.
/// </para>
/// <para>
/// Для backward compat існує застарілий <see cref="Config"/>; він буде видалений у 3.0.
/// </para>
/// </remarks>
public sealed class SignalCliOptions
{
    /// <summary>Головна директорія програми (де лежать config/log/lib).</summary>
    [Required(AllowEmptyStrings = false)]
    public string AppHome { get; set; } = string.Empty;

    /// <summary>Піддиректорія з JAR-файлами Signal CLI (відносно <see cref="AppHome"/>).</summary>
    [Required(AllowEmptyStrings = false)]
    public string LibDirectory { get; set; } = string.Empty;

    /// <summary>Шлях до виконуваного файлу Java (для JVM-режиму). Опціональне — якщо вказано <see cref="SignalCliExecutable"/>.</summary>
    public string? JavaExecutable { get; set; }

    /// <summary>
    /// Шлях до нативного бінарника signal-cli. Якщо задано — Java не потрібна.
    /// </summary>
    public string? SignalCliExecutable { get; set; }

    /// <summary>Рівень логування signal-cli.</summary>
    public CliLogLevel CliLogLevelCli { get; set; } = CliLogLevel.Info;

    /// <summary>Шлях до файлу логу signal-cli; за замовчуванням обчислюється від <see cref="AppHome"/>.</summary>
    public string? LogFileCli { get; set; }

    /// <summary>Шлях до директорії зі сховищем даних signal-cli; за замовчуванням обчислюється від <see cref="AppHome"/>.</summary>
    public string? StoragePathCli { get; set; }

    /// <summary>Чи використовувати ручний режим отримання повідомлень (manual vs on-start).</summary>
    public bool UseManualReceiveMode { get; set; } = true;

    /// <summary>Максимальна кількість спроб перезапуску процесу (0 — вимкнено).</summary>
    [Range(0, 100)]
    public int MaxRestartAttempts { get; set; } = 3;

    /// <summary>Інтервал між перевірками здоров'я процесу (секунди).</summary>
    [Range(1, 3600)]
    public int HealthCheckIntervalSeconds { get; set; } = 40;

    /// <summary>Максимальний час очікування відповіді на ping під час health-check (секунди).</summary>
    [Range(1, 3600)]
    public int HealthCheckTimeoutSeconds { get; set; } = 10;

    /// <summary>Затримка перед перезапуском процесу (секунди).</summary>
    [Range(0, 3600)]
    public int RestartDelaySeconds { get; set; } = 5;

    /// <summary>Час очікування граційного завершення signal-cli після команди "exit" (секунди).</summary>
    [Range(0, 3600)]
    public int StopTimeoutSeconds { get; set; } = 2;

    /// <summary>Таймаут одного JSON-RPC запиту (секунди).</summary>
    [Range(1, 3600)]
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Вікно стабільності (секунди), після якого лічильник перезапусків скидається в 0.
    /// </summary>
    [Range(1, 86400)]
    public int RestartWindowSeconds { get; set; } = 60;

    /// <summary>Змінні середовища, що передаються процесу signal-cli.</summary>
    public IDictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Конвертує <see cref="SignalCliOptions"/> у legacy-<see cref="Config"/>
    /// (внутрішні сервіси досі споживають <see cref="Config"/> як singleton).
    /// </summary>
    internal Config ToConfig() => new()
    {
        AppHome = AppHome,
        LibDirectory = LibDirectory,
        JavaExecutable = JavaExecutable ?? string.Empty,
        SignalCliExecutable = SignalCliExecutable,
        CliLogLevelCli = CliLogLevelCli,
        LogFileCli = LogFileCli,
        StoragePathCli = StoragePathCli,
        UseManualReceiveMode = UseManualReceiveMode,
        MaxRestartAttempts = MaxRestartAttempts,
        HealthCheckIntervalSeconds = HealthCheckIntervalSeconds,
        HealthCheckTimeoutSeconds = HealthCheckTimeoutSeconds,
        RestartDelaySeconds = RestartDelaySeconds,
        StopTimeoutSeconds = StopTimeoutSeconds,
        RequestTimeoutSeconds = RequestTimeoutSeconds,
        RestartWindowSeconds = RestartWindowSeconds,
        EnvironmentVariables = EnvironmentVariables,
    };
}
