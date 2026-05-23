using System.ComponentModel.DataAnnotations;
using SignalCli.Models.SignalCli;

namespace SignalCli.Models;

/// <summary>
/// Типізована, immutable конфігурація бібліотеки SignalCli.NET для реєстрації
/// через <c>IOptions&lt;SignalCliOptions&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// D.1: усі властивості — <see langword="init"/>-only; об’єкт іммутабельний після
/// конструювання. DataAnnotations (<see cref="RequiredAttribute"/>/<see cref="RangeAttribute"/>)
/// перевіряються на старті хоста через <c>OptionsBuilder.ValidateDataAnnotations()</c>
/// + <c>ValidateOnStart()</c> — fail-fast замість сюрпризу під час `ToProcessConfig()`.
/// </para>
/// <para>
/// Для backward compat існує застарілий <see cref="Config"/> із <c>set</c>-сетерами;
/// він буде видалений у 3.0.
/// </para>
/// </remarks>
public sealed class SignalCliOptions
{
    /// <summary>Головна директорія програми (де лежать config/log/lib).</summary>
    /// <remarks>
    /// Не позначено <c>required</c>-модифікатором (C# 11), бо Options-фреймворк створює
    /// інстанс через <see cref="Activator.CreateInstance{T}"/> — це конфліктує з
    /// <c>required</c>. Валідація надається атрибутом <see cref="RequiredAttribute"/>.
    /// </remarks>
    [Required(AllowEmptyStrings = false)]
    public string AppHome { get; init; } = string.Empty;

    /// <summary>Піддиректорія з JAR-файлами Signal CLI (відносно <see cref="AppHome"/>).</summary>
    [Required(AllowEmptyStrings = false)]
    public string LibDirectory { get; init; } = string.Empty;

    /// <summary>Шлях до виконуваного файлу Java (для JVM-режиму). Опціональне — якщо вказано <see cref="SignalCliExecutable"/>.</summary>
    public string? JavaExecutable { get; init; }

    /// <summary>
    /// Шлях до нативного бінарника signal-cli. Якщо задано — Java не потрібна.
    /// </summary>
    public string? SignalCliExecutable { get; init; }

    /// <summary>Рівень логування signal-cli.</summary>
    public CliLogLevel CliLogLevelCli { get; init; } = CliLogLevel.Info;

    /// <summary>Шлях до файлу логу signal-cli; за замовчуванням обчислюється від <see cref="AppHome"/>.</summary>
    public string? LogFileCli { get; init; }

    /// <summary>Шлях до директорії зі сховищем даних signal-cli; за замовчуванням обчислюється від <see cref="AppHome"/>.</summary>
    public string? StoragePathCli { get; init; }

    /// <summary>Чи використовувати ручний режим отримання повідомлень (manual vs on-start).</summary>
    public bool UseManualReceiveMode { get; init; } = true;

    /// <summary>Максимальна кількість спроб перезапуску процесу (0 — вимкнено).</summary>
    [Range(0, 100)]
    public int MaxRestartAttempts { get; init; } = 3;

    /// <summary>Інтервал між перевірками здоров'я процесу (секунди).</summary>
    [Range(1, 3600)]
    public int HealthCheckIntervalSeconds { get; init; } = 40;

    /// <summary>Максимальний час очікування відповіді на ping під час health-check (секунди).</summary>
    [Range(1, 3600)]
    public int HealthCheckTimeoutSeconds { get; init; } = 10;

    /// <summary>Затримка перед перезапуском процесу (секунди).</summary>
    [Range(0, 3600)]
    public int RestartDelaySeconds { get; init; } = 5;

    /// <summary>Час очікування граційного завершення signal-cli після команди "exit" (секунди).</summary>
    [Range(0, 3600)]
    public int StopTimeoutSeconds { get; init; } = 2;

    /// <summary>Таймаут одного JSON-RPC запиту (секунди).</summary>
    [Range(1, 3600)]
    public int RequestTimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Вікно стабільності (секунди), після якого лічильник перезапусків скидається в 0.
    /// </summary>
    [Range(1, 86400)]
    public int RestartWindowSeconds { get; init; } = 60;

    /// <summary>Змінні середовища, що передаються процесу signal-cli.</summary>
    public IDictionary<string, string> EnvironmentVariables { get; init; } = new Dictionary<string, string>();

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
