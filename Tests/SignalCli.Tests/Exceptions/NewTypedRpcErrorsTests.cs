using SignalCli.Exceptions;
using SignalCli.Models.Rpc;

namespace SignalCli.Tests.Exceptions;

/// <summary>
/// signal-cli-api-coverage Wave 1: 3 нові typed RPC-помилки
/// (<see cref="IdentityChangedException"/>, <see cref="GroupAdminRequiredException"/>,
/// <see cref="CaptchaRequiredException"/>) + перевірка inheritance-контракту
/// IdentityChangedException : UntrustedIdentityException : JsonRpcException.
/// </summary>
public class NewTypedRpcErrorsTests
{
    [Fact]
    public void IdentityChangedException_IsSubtypeOfUntrustedIdentity()
    {
        // Key контракт: `catch (UntrustedIdentityException)` ловить також IdentityChangedException.
        // api-coverage-audit-followup §1: тип deprecated (never dispatched, removed 5.0); retained
        // для hierarchy regression-guard during 4.x deprecation period.
#pragma warning disable SIGNALCLI001 // Obsolete IdentityChangedException usage — retained для hierarchy guard
        var ex = new IdentityChangedException(new JsonRpcError { Code = -4, Message = "Untrusted (re-install)" });
#pragma warning restore SIGNALCLI001
        Assert.IsAssignableFrom<UntrustedIdentityException>(ex);
        Assert.IsAssignableFrom<JsonRpcException>(ex);
        Assert.Equal(-4, ex.Error.Code);
        Assert.Equal(JsonRpcErrorCode.UntrustedIdentity, ex.KnownCode);
    }

    [Fact]
    public void GroupAdminRequiredException_HasCodeMinusOne()
    {
        var ex = new GroupAdminRequiredException(new JsonRpcError { Code = -1, Message = "User is not an admin of the group" });
        Assert.IsAssignableFrom<JsonRpcException>(ex);
        Assert.Equal(-1, ex.Error.Code);
        Assert.Equal(JsonRpcErrorCode.UserError, ex.KnownCode);
        Assert.Contains("admin", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CaptchaRequiredException_HasCodeMinusSix()
    {
        var ex = new CaptchaRequiredException(new JsonRpcError { Code = -6, Message = "Captcha required" });
        Assert.IsAssignableFrom<JsonRpcException>(ex);
        Assert.Equal(-6, ex.Error.Code);
        Assert.Equal(JsonRpcErrorCode.CaptchaRejected, ex.KnownCode);
    }

    [Fact]
    public void NewExceptions_AreSealed()
    {
        // Sealed-контракт: ці типи — листя hierarchy; consumer не повинен далі subclass'ити.
#pragma warning disable SIGNALCLI001 // Obsolete IdentityChangedException — retained для hierarchy guard
        Assert.True(typeof(IdentityChangedException).IsSealed);
#pragma warning restore SIGNALCLI001
        Assert.True(typeof(GroupAdminRequiredException).IsSealed);
        Assert.True(typeof(CaptchaRequiredException).IsSealed);
    }

    [Fact]
    public void UntrustedIdentityException_IsNoLongerSealed()
    {
        // Wave 1 розпинила тип щоб дозволити IdentityChangedException-derivation.
        // Регресія: повторне sealing зламає IdentityChangedException.
        Assert.False(typeof(UntrustedIdentityException).IsSealed);
    }

    // ---- JsonRpcClient dispatch (using same ProcessMessageAsync reflection trick як T01) ----

    [Fact]
    public Task InvokeMethodAsync_Code_Minus6_ThrowsCaptchaRequired() =>
        AssertDispatchedException<CaptchaRequiredException>(
            """{"id":"1","error":{"code":-6,"message":"Captcha rejected"}}""",
            ex => Assert.Equal(JsonRpcErrorCode.CaptchaRejected, ex.KnownCode));

    [Fact]
    public Task InvokeMethodAsync_Code_Minus1_WithAdminMessage_ThrowsGroupAdminRequired() =>
        AssertDispatchedException<GroupAdminRequiredException>(
            """{"id":"1","error":{"code":-1,"message":"User is not an admin of the group"}}""",
            ex => Assert.Contains("admin", ex.Message, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public Task InvokeMethodAsync_Code_Minus1_WithoutAdminToken_ThrowsBaseJsonRpcException() =>
        // -1 без токену "admin" → generic JsonRpcException (НЕ GroupAdminRequired)
        AssertExactExceptionType<JsonRpcException>(
            """{"id":"1","error":{"code":-1,"message":"Invalid phone number"}}""");

    // Allow GroupAdminRequiredException-derived bases not to upcast to base.
    [Fact]
    public Task InvokeMethodAsync_Code_Minus1_WithAdminCaseInsensitive_StillDispatchesGroupAdmin() =>
        AssertDispatchedException<GroupAdminRequiredException>(
            """{"id":"1","error":{"code":-1,"message":"Only ADMINS can perform this action"}}""",
            _ => { /* лише тип перевіряємо */ });

    private static Task AssertDispatchedException<TException>(string errorJson, Action<TException> verify)
        where TException : Exception
        => RunDispatchAndAssert(errorJson, async invokeTask =>
        {
            var ex = await Assert.ThrowsAsync<TException>(() => invokeTask);
            verify(ex);
        });

    private static Task AssertExactExceptionType<TException>(string errorJson)
        where TException : Exception
        => RunDispatchAndAssert(errorJson, async invokeTask =>
        {
            var ex = await Assert.ThrowsAnyAsync<JsonRpcException>(() => invokeTask);
            // Перевіряємо EXACT type — інакше -1 без "admin" впав би на subtype GroupAdminRequired
            // (false-positive). GetType() — exact runtime type, не IsAssignableFrom.
            Assert.Equal(typeof(TException), ex.GetType());
        });

    private static async Task RunDispatchAndAssert(string errorJson, Func<Task, Task> assertion)
    {
        var logger = new Moq.Mock<Microsoft.Extensions.Logging.ILogger<SignalCli.Services.Rpc.JsonRpcClient>>();
        logger.Setup(l => l.IsEnabled(Moq.It.IsAny<Microsoft.Extensions.Logging.LogLevel>())).Returns(true);

        var streamProvider = new Moq.Mock<SignalCli.Interfaces.SignalCli.IStreamPairProvider>();
        var subject = new System.Reactive.Subjects.Subject<SignalCli.Models.SignalCli.StreamPair?>();
        SignalCli.Models.SignalCli.StreamPair? current = null;
        streamProvider.SetupGet(sp => sp.CurrentStreamPair).Returns(() => current);
        streamProvider.SetupGet(sp => sp.StreamPairChanged).Returns(subject);

        var options = new SignalCli.Models.SignalCliOptions
        {
            AppHome = Path.GetTempPath(),
            JavaExecutable = string.Empty,
            LibDirectory = string.Empty,
            RequestTimeoutSeconds = 60,
        };
        await using var client = new SignalCli.Services.Rpc.JsonRpcClient(
            logger.Object, streamProvider.Object, options, null);

        current = new SignalCli.Models.SignalCli.StreamPair(
            new StreamWriter(new MemoryStream()),
            new StreamReader(new MemoryStream()),
            new StreamReader(new MemoryStream()));
        subject.OnNext(current);

        var invokeTask = client.InvokeMethodAsync(
            "test",
            new TestProbeRequest(),
            TestSerializationContext.Default.TestProbeRequest,
            TestSerializationContext.Default.TestProbeResponse);

        var mi = typeof(SignalCli.Services.Rpc.JsonRpcClient).GetMethod(
            "ProcessMessageAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        await (Task)mi!.Invoke(client, [errorJson, CancellationToken.None])!;

        await assertion(invokeTask);
    }
}
