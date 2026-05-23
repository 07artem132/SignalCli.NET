using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalCli.Tests;

/// <summary>
/// post-modernize-tuning §6.10 (audit P6): test-local source-gen context для тестових DTO,
/// що НЕ повинні забруднювати production `SignalJsonContext`. Реєструємо тільки те, що
/// потрібно для unit-тестів JsonRpcClient'а (анонім-payload replacements).
/// </summary>
[JsonSerializable(typeof(TestProbeRequest))]
[JsonSerializable(typeof(TestProbeResponse))]
[JsonSerializable(typeof(JsonElement))]
internal partial class TestSerializationContext : JsonSerializerContext;

/// <summary>Test-only DTO для тестів JsonRpcClient: serialize-shape перевірки.</summary>
internal sealed record TestProbeRequest(
    [property: JsonPropertyName("hello")] string Hello = "");

/// <summary>Test-only DTO для тестів JsonRpcClient: deserialize-shape перевірки.</summary>
internal sealed record TestProbeResponse(
    [property: JsonPropertyName("foo")] string Foo = "");
