using System.Text.Json;
using SignalCli.Models.Signal.Devices;
using SignalCli.Serialization;

namespace SignalCli.Tests.Serialization;

/// <summary>
/// Wire-shape pinning для device-linking флоу (startLink/finishLink).
/// </summary>
/// <remarks>
/// Виявлено 2026-07-16 e2e-пробою через SignalCliNet.WsRpcServer: signal-cli 0.14.3
/// віддає <c>{"deviceLinkUri":"sgnl://..."}</c> (camelCase), а <c>StartLinkResponse</c>
/// без <c>[JsonPropertyName]</c> у case-sensitive контексті мовчки давав
/// <c>DeviceLinkUri == null</c> — RPC-відповідь <c>"result": {}</c>. Тести нижче
/// проганяють саме продакшн-шлях (<c>SignalJsonContext.Default</c>), не reflection-опції.
/// </remarks>
public class DeviceLinkingSerializationTests
{
    [Fact]
    public void StartLinkResponse_DeserializesCamelCaseDeviceLinkUri_FromContextPath()
    {
        // Приклад wire-відповіді signal-cli 0.14.3 (упс: StartLinkCommand → JsonLink).
        const string wire = """{"deviceLinkUri":"sgnl://linkdevice?uuid=abc&pub_key=def"}""";

        var response = JsonSerializer.Deserialize(
            wire, SignalJsonContext.Default.StartLinkResponse);

        Assert.NotNull(response);
        Assert.Equal("sgnl://linkdevice?uuid=abc&pub_key=def", response.DeviceLinkUri);
    }

    [Fact]
    public void FinishLinkResponse_DeserializesLowercaseNumber_FromContextPath()
    {
        // FinishLinkResponse вже має [JsonPropertyName("number")] — пін щоб не відкотили.
        const string wire = """{"number":"+380000000000"}""";

        var response = JsonSerializer.Deserialize(
            wire, SignalJsonContext.Default.FinishLinkResponse);

        Assert.NotNull(response);
        Assert.Equal("+380000000000", response.Number);
    }
}
