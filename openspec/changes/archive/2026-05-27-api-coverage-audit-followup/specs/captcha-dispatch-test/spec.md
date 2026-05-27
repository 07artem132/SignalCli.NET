## ADDED Requirements

### Requirement: `JsonRpcClient` dispatch of `CaptchaRequiredException` for code -6 SHALL be pinned by a positive-case test

`Tests/SignalCli.Tests/Exceptions/NewTypedRpcErrorsTests.cs` SHALL gain a `[Fact]` named `InvokeMethodAsync_Code_Minus6_ThrowsCaptchaRequired` that mirrors the existing `GroupAdminRequired` dispatch-test shape:

```csharp
[Fact]
public Task InvokeMethodAsync_Code_Minus6_ThrowsCaptchaRequired() =>
    AssertDispatchedException<CaptchaRequiredException>(
        """{"id":"1","error":{"code":-6,"message":"Captcha required"}}""",
        ex => Assert.Equal(JsonRpcErrorCode.CaptchaRejected, ex.KnownCode));
```

No production change. The test pins the existing dispatch arm at `src/SignalCli/Services/Rpc/JsonRpcClient.cs:510` so a future accidental removal of the `CaptchaRejected` switch-case fails immediately rather than silently downgrading callers to `catch (JsonRpcException)`.

#### Scenario: signal-cli emits -6 error → CaptchaRequiredException dispatched

- **GIVEN** an in-flight RPC call awaiting response
- **WHEN** signal-cli emits `{"id":"1","error":{"code":-6,"message":"Captcha required"}}`
- **THEN** the task faults with `CaptchaRequiredException`
- **AND** `ex.KnownCode == JsonRpcErrorCode.CaptchaRejected`
- **AND** `catch (JsonRpcException)` still catches it (backward compat preserved)

#### Scenario: Future removal of the -6 dispatch arm fails the test

- **GIVEN** a hypothetical PR that removes the `(int)JsonRpcErrorCode.CaptchaRejected => new CaptchaRequiredException(response.Error),` line from `JsonRpcClient.cs:510`
- **WHEN** CI runs `dotnet test`
- **THEN** `InvokeMethodAsync_Code_Minus6_ThrowsCaptchaRequired` fails because the dispatched exception type is `JsonRpcException` not `CaptchaRequiredException`
- **AND** the PR cannot merge until the dispatch arm is restored
