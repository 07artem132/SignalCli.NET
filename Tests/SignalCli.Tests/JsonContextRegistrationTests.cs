using System.Reflection;
using SignalCli.Serialization;

namespace SignalCli.Tests;

/// <summary>
/// post-modernize-tuning §6.12 (audit N8): захист від silent-empty-params регресії, коли
/// `JsonSerializer.SerializeToElement(payload, options)` всередині `JsonRpcClient.InvokeMethodAsync`
/// тихо повертає <c>"{}"</c> для типу, відсутнього в source-gen контексті
/// <see cref="SignalJsonContext"/>. До §6.4 (видалення reflection fallback) це не критично —
/// reflection-resolver рятує. Після §6.4 — silent-data-loss bug на production.
/// </summary>
/// <remarks>
/// Стратегія: рефлексивно знаходимо всі DTO-типи у <c>SignalCli.Models.Signal.*</c>-namespace,
/// що звичайно крос-уйдуть JSON-кордон (records з суфіксом <c>Parameters</c>, <c>Response</c>,
/// або <c>EventArgs</c>), і стверджуємо, що кожен зареєстрований у
/// <see cref="SignalJsonContext.Default"/>. Якщо новий PR додасть DTO, але забуде
/// <c>[JsonSerializable(typeof(NewThing))]</c> у контекст — тест валиться відразу.
///
/// Альтернатива (IL-level callsite-discovery через Mono.Cecil) overkill — модель-namespace
/// enumeration ловить той самий клас регресій без зайвої залежності.
/// </remarks>
public sealed class JsonContextRegistrationTests
{
    [Fact]
    public void AllParametersAndResponseDtos_AreRegisteredInSignalJsonContext()
    {
        // Завантажуємо assembly, що містить наші DTOs (через будь-який тип із SignalCli core).
        var coreAssembly = typeof(SignalJsonContext).Assembly;

        // Шукаємо концретні (не abstract, не interface, не generic-definition) public/internal
        // DTO-типи у Models-namespace, що ПРЯМО крос-уйдуть JSON-кордон як TRequest/TResponse
        // для InvokeMethodAsync. *EventArgs виключаємо — вони мапляться з JsonMessageEnvelope
        // програмно (через SignalEventService.OnNotificationReceived), а не через JsonSerializer.
        var dtoTypes = coreAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                        || (t.IsValueType && !t.IsEnum))
            .Where(t => t.Namespace?.StartsWith("SignalCli.Models.Signal", StringComparison.Ordinal) == true)
            .Where(t => t.Name.EndsWith("Parameters", StringComparison.Ordinal)
                        || t.Name.EndsWith("Response", StringComparison.Ordinal))
            .ToList();

        // Sanity: список має бути непорожній — інакше тест мовчазно проходить.
        Assert.NotEmpty(dtoTypes);

        var missing = new List<string>();
        foreach (var dto in dtoTypes)
        {
            var registered = SignalJsonContext.Default.GetTypeInfo(dto);
            if (registered is null)
            {
                missing.Add(dto.FullName ?? dto.Name);
            }
        }

        Assert.True(missing.Count == 0,
            $"Наступні DTO-типи не зареєстровані в SignalJsonContext (додай " +
            $"[JsonSerializable(typeof(...))]): {string.Join(", ", missing)}");
    }

    [Fact]
    public void KnownRpcTypes_AreRegisteredInSignalJsonContext()
    {
        // Окрема перевірка для типів, що НЕ підпадають під namespace-фільтр вище,
        // але активно використовуються в JsonRpcClient (root-types для serialize/deserialize).
        // Якщо хтось видалить [JsonSerializable] для одного з них — production'у настане лихо.
        Type[] knownRpcTypes =
        [
            typeof(SignalCli.Models.Rpc.JsonRpcRequest),
            typeof(SignalCli.Models.Rpc.JsonRpcResponse),
            typeof(SignalCli.Models.Rpc.JsonRpcError),
            typeof(SignalCli.Models.Rpc.JsonRpcNotificationRaw),
            typeof(SignalCli.Models.Rpc.JsonRpcNotification<SignalCli.Models.Signal.Events.SubscriptionEventArgs>),
        ];

        foreach (var t in knownRpcTypes)
        {
            Assert.NotNull(SignalJsonContext.Default.GetTypeInfo(t));
        }
    }
}
