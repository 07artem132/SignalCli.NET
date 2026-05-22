using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Accounts;

/// <summary>
/// Параметри для запиту списку облікових записів Signal.
/// Цей запис використовується з методом listAccounts.
/// </summary>
[PublicAPI]
public sealed record ListAccountsParameters;