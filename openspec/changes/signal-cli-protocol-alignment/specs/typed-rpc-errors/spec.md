## ADDED Requirements

### Requirement: signal-cli error codes SHALL be representable as a typed enum

A new `public enum SignalCli.Exceptions.JsonRpcErrorCode` SHALL declare the ten error codes that signal-cli emits via JSON-RPC: the five JSON-RPC 2.0 standard codes (`ParseError = -32700`, `InvalidRequest = -32600`, `MethodNotFound = -32601`, `InvalidParams = -32602`, `InternalError = -32603`) and the five signal-cli-specific custom codes (`UserError = -1`, `IoError = -3`, `UntrustedIdentity = -4`, `RateLimit = -5`, `CaptchaRejected = -6`).

`JsonRpcException` SHALL expose a `public JsonRpcErrorCode? KnownCode { get; }` property that returns the enum value when the integer `Code` matches a defined enum member, or `null` for unknown codes (forward-compat for codes signal-cli might add in 0.15+).

#### Scenario: KnownCode resolves known codes
- **GIVEN** a `JsonRpcException` constructed from a `JsonRpcError` with `Code = -5`
- **WHEN** the consumer reads `ex.KnownCode`
- **THEN** the value is `JsonRpcErrorCode.RateLimit`

#### Scenario: KnownCode is null for unknown codes
- **GIVEN** a `JsonRpcException` constructed from a `JsonRpcError` with `Code = -9999`
- **WHEN** the consumer reads `ex.KnownCode`
- **THEN** the value is `null`

### Requirement: Two derived exception types SHALL surface high-leverage signal-cli error codes

Two `public sealed` exception types SHALL extend `JsonRpcException`:

- `RateLimitException` — thrown when the response error code equals `-5`. Consumer-actionable: retry with exponential backoff.
- `UntrustedIdentityException` — thrown when the response error code equals `-4`. Consumer-actionable: verify safety number, surface to user.

`JsonRpcClient.InvokeMethodAsync` SHALL emit the derived type when the wire `error.code` matches; for all other error codes the base `JsonRpcException` is thrown. Consumers using `catch (RateLimitException)` / `catch (UntrustedIdentityException)` SHALL receive the matching subset; consumers using `catch (JsonRpcException)` continue to catch every error code as before.

#### Scenario: Rate-limit response surfaces RateLimitException
- **GIVEN** an `InvokeMethodAsync` call in flight
- **WHEN** `ProcessMessageAsync` receives `{"id":"1","error":{"code":-5,"message":"Rate limit"}}`
- **THEN** the in-flight task faults with `RateLimitException` (which is also a `JsonRpcException`)
- **AND** `ex.KnownCode == JsonRpcErrorCode.RateLimit`

#### Scenario: Untrusted-identity response surfaces UntrustedIdentityException
- **GIVEN** an `InvokeMethodAsync` call in flight
- **WHEN** `ProcessMessageAsync` receives `{"id":"1","error":{"code":-4,"message":"Untrusted identity"}}`
- **THEN** the in-flight task faults with `UntrustedIdentityException`

#### Scenario: Unrelated error code keeps base JsonRpcException
- **GIVEN** an `InvokeMethodAsync` call in flight
- **WHEN** `ProcessMessageAsync` receives `{"id":"1","error":{"code":-3,"message":"IO failed"}}`
- **THEN** the in-flight task faults with plain `JsonRpcException` (NOT a derived type)
- **AND** `ex.KnownCode == JsonRpcErrorCode.IoError`
