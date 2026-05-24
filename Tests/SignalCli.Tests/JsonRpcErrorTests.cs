using System.Text.Json;
using SignalCli.Exceptions;
using SignalCli.Models.Rpc;
using SignalCli.Serialization;

namespace SignalCli.Tests;

/// <summary>
/// signal-cli-protocol-alignment (typed-rpc-errors) + audit-followup-2026 §6.a:
/// JSON-RPC error code contract — deserialization, KnownCode mapping, derived exceptions.
/// </summary>
public class JsonRpcErrorTests
{
    private static JsonRpcResponse DeserializeError(string json)
        => JsonSerializer.Deserialize(json, SignalJsonContext.Default.JsonRpcResponse)
           ?? throw new InvalidOperationException("null response");

    // ---- Standard JSON-RPC 2.0 codes ----

    [Fact]
    public void Error_Minus32601_DeserializesAsMethodNotFound()
    {
        const string json = """{"jsonrpc":"2.0","error":{"code":-32601,"message":"Method not found"},"id":"1"}""";
        var resp = DeserializeError(json);
        Assert.NotNull(resp.Error);
        Assert.Equal(-32601, resp.Error!.Code);
        Assert.Equal("Method not found", resp.Error.Message);
    }

    [Fact]
    public void Error_Minus32700_DeserializesAsParseError()
    {
        const string json = """{"jsonrpc":"2.0","error":{"code":-32700,"message":"Parse error"},"id":null}""";
        var resp = DeserializeError(json);
        Assert.Equal(-32700, resp.Error!.Code);
    }

    // ---- KnownCode enum mapping ----

    [Theory]
    [InlineData(-32700, JsonRpcErrorCode.ParseError)]
    [InlineData(-32600, JsonRpcErrorCode.InvalidRequest)]
    [InlineData(-32601, JsonRpcErrorCode.MethodNotFound)]
    [InlineData(-32602, JsonRpcErrorCode.InvalidParams)]
    [InlineData(-32603, JsonRpcErrorCode.InternalError)]
    [InlineData(-1, JsonRpcErrorCode.UserError)]
    [InlineData(-3, JsonRpcErrorCode.IoError)]
    [InlineData(-4, JsonRpcErrorCode.UntrustedIdentity)]
    [InlineData(-5, JsonRpcErrorCode.RateLimit)]
    [InlineData(-6, JsonRpcErrorCode.CaptchaRejected)]
    public void KnownCode_ResolvesAllSignalCliCodes(int wireCode, JsonRpcErrorCode expected)
    {
        var ex = new JsonRpcException(new JsonRpcError { Code = wireCode, Message = "test" });
        Assert.Equal(expected, ex.KnownCode);
    }

    [Fact]
    public void KnownCode_UnknownCode_ReturnsNull()
    {
        // -9999 не визначений у enum — KnownCode має повернути null (forward-compat).
        var ex = new JsonRpcException(new JsonRpcError { Code = -9999, Message = "future" });
        Assert.Null(ex.KnownCode);
    }

    // ---- Derived exception types ----

    [Fact]
    public void RateLimitException_HasCorrectCode()
    {
        var ex = new RateLimitException(new JsonRpcError { Code = -5, Message = "Rate limit" });
        Assert.IsAssignableFrom<JsonRpcException>(ex);
        Assert.Equal(-5, ex.Error.Code);
        Assert.Equal(JsonRpcErrorCode.RateLimit, ex.KnownCode);
    }

    [Fact]
    public void UntrustedIdentityException_HasCorrectCode()
    {
        var ex = new UntrustedIdentityException(new JsonRpcError { Code = -4, Message = "Untrusted" });
        Assert.IsAssignableFrom<JsonRpcException>(ex);
        Assert.Equal(-4, ex.Error.Code);
        Assert.Equal(JsonRpcErrorCode.UntrustedIdentity, ex.KnownCode);
    }

    // ---- audit-followup-2026 §6.a: error.data payload preservation ----

    /// <summary>
    /// JSON-RPC 2.0 спецификация дозволяє довільний <c>error.data</c> об'єкт. signal-cli
    /// може ним передавати додаткову діагностику. Наш JsonRpcError.Data зберігає його як
    /// JsonElement; цей тест пінує що поле дійсно desеріалізується.
    /// </summary>
    [Fact]
    public void Error_WithDataField_PreservesPayload()
    {
        const string json = """{"jsonrpc":"2.0","error":{"code":-32603,"message":"E","data":{"foo":42,"bar":"baz"}},"id":"1"}""";
        var resp = DeserializeError(json);
        Assert.NotNull(resp.Error);
        // Data — JsonElement? (опціональне поле); має бути не-null + містити foo=42.
        Assert.NotNull(resp.Error!.Data);
        var data = resp.Error.Data!.Value;
        Assert.Equal(System.Text.Json.JsonValueKind.Object, data.ValueKind);
        Assert.Equal(42, data.GetProperty("foo").GetInt32());
        Assert.Equal("baz", data.GetProperty("bar").GetString());
    }
}
