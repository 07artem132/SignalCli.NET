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
/// deprecated-shim-removal §5 (v4.0): єдиний configuration surface — legacy <c>Config</c>-shim
/// видалено разом з його `AddSignalCli(Action&lt;Config&gt;?)` overload'ом.
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

    /// <summary>
    /// audit A3 / §1: ємність bounded-каналу між stdout-читачем і fan-out-споживачем
    /// JSON-RPC сповіщень у <c>JsonRpcClient</c>.
    /// </summary>
    /// <remarks>
    /// Якщо споживач (через <c>_notificationSubject.OnNext</c>) повільний, канал
    /// заповнюється — наступний <c>WriteAsync</c> чекає. Це back-pressure до самого
    /// stdout-reader-а: повільний підписник не дозволить нагромаджувати повідомлення
    /// у пам'яті. <see cref="System.Threading.Channels.BoundedChannelFullMode.Wait"/>.
    /// За замовчуванням 1024.
    /// </remarks>
    [Range(1, 1_000_000)]
    public int NotificationChannelCapacity { get; set; } = 1024;

    /// <summary>Змінні середовища, що передаються процесу signal-cli.</summary>
    /// <remarks>
    /// post-modernize-tuning §4.10 / §4.28 (audit D7/E2): на читання — <see cref="IReadOnlyDictionary{TKey,TValue}"/>.
    /// Викликач задає мапу через <c>opts.EnvironmentVariables = new Dictionary&lt;,&gt;{ … }</c> у Configure-делегаті,
    /// а downstream-сервіси читають read-only-вʼю — мутації після StartAsync виключені.
    /// </remarks>
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; set; } =
        new Dictionary<string, string>();

    // deprecated-shim-removal §5: ToConfig() shim видалено разом із Config-типом.
}
