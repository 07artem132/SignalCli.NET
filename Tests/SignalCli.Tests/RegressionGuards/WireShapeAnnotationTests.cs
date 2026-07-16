using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using SignalCli.Serialization;

namespace SignalCli.Tests.RegressionGuards;

/// <summary>
/// RG10 — wire-shape-annotation guard: кожна публічна властивість кожного DTO,
/// зареєстрованого у <see cref="SignalJsonContext"/>, МУСИТЬ нести явний
/// <c>[JsonPropertyName]</c> (або <c>[JsonIgnore]</c>).
/// </summary>
/// <remarks>
/// <para>
/// Контекст навмисно НЕ має <c>PropertyNamingPolicy</c> і працює case-sensitive:
/// signal-cli віддає camelCase (<c>deviceLinkUri</c>), а PascalCase-властивість без
/// атрибута тихо десеріалізується у <c>null</c> — без винятку, без логу. Саме так
/// зламався <c>StartLinkResponse.DeviceLinkUri</c> (виявлено 2026-07-16 e2e-пробою
/// startLink через SignalCliNet.WsRpcServer: RPC відповів <c>"result": {}</c>).
/// </para>
/// <para>
/// Wrapper-record'и зі своїм <c>[JsonConverter]</c> на типі (ListAccountsResponse
/// та схожі) звільнені: їхню wire-форму визначає конвертер, а не властивості.
/// Порожні record'и (без властивостей) проходять тривіально.
/// </para>
/// </remarks>
public class WireShapeAnnotationTests
{
    [Fact]
    public void EveryContextRegisteredDtoProperty_HasExplicitJsonPropertyNameOrIgnore()
    {
        var registeredTypes = typeof(SignalJsonContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsGenericType
                        && p.PropertyType.GetGenericTypeDefinition() == typeof(JsonTypeInfo<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .SelectMany(ExpandCollectionElementTypes)
            .Where(t => t.Namespace?.StartsWith("SignalCli.Models", StringComparison.Ordinal) == true)
            .Where(t => !t.IsEnum)
            .Distinct()
            .ToList();

        // Плита захисту від "guard нічого не сканує": контекст має ~99 реєстрацій,
        // після фільтрів має лишитись суттєва кількість DTO.
        Assert.True(registeredTypes.Count > 20,
            $"Очікували >20 DTO-типів із SignalJsonContext, знайшли {registeredTypes.Count} — " +
            "рефлексія по JsonTypeInfo<T>-властивостях контексту зламалась?");

        var violations = new List<string>();

        foreach (Type type in registeredTypes)
        {
            // Тип із власним конвертером сам визначає wire-форму — властивості не читаються.
            if (type.GetCustomAttribute<JsonConverterAttribute>() != null)
            {
                continue;
            }

            foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                bool hasName = prop.GetCustomAttribute<JsonPropertyNameAttribute>() != null;
                bool hasIgnore = prop.GetCustomAttribute<JsonIgnoreAttribute>() is { Condition: JsonIgnoreCondition.Always };

                if (!hasName && !hasIgnore)
                {
                    violations.Add($"{type.FullName}.{prop.Name}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "DTO-властивості без явного [JsonPropertyName] (контекст case-sensitive, " +
            "без naming policy — PascalCase мовчки дає null на camelCase wire):\n  " +
            string.Join("\n  ", violations));
    }

    /// <summary>
    /// Для реєстрацій колекцій (List&lt;T&gt;, T[] тощо) перевіряємо і сам тип, і елемент —
    /// wire-форму елемента визначають його властивості.
    /// </summary>
    private static IEnumerable<Type> ExpandCollectionElementTypes(Type type)
    {
        yield return type;

        if (type.IsArray && type.GetElementType() is { } elem)
        {
            yield return elem;
        }

        if (type.IsGenericType)
        {
            foreach (Type arg in type.GetGenericArguments())
            {
                yield return arg;
            }
        }
    }
}
