namespace SignalCli.Tests.Integration;

/// <summary>
/// signal-cli-api-coverage Wave 3 (contacts-identity): env-gated fixture для E2E тестів
/// що потребують registered Signal account.
/// </summary>
/// <remarks>
/// Read-only methods (<c>listContacts</c>, <c>listIdentities</c>, Wave-5 <c>listDevices</c>,
/// Wave-8 <c>getUserStatus</c>) безпечно діляться account'ом між test-runs — НЕ мутують state.
/// <para>
/// Local-developer setup: <c>setx SIGNALCLI_TEST_ACCOUNT "+1234567890"</c> (Windows) або
/// <c>export SIGNALCLI_TEST_ACCOUNT="+1234567890"</c> (Linux/macOS). Number має бути
/// попередньо зареєстрований через <c>signal-cli register</c> + verify CLI flow.
/// </para>
/// <para>
/// CI runners без env var → skip clean (test passes without executing E2E body).
/// </para>
/// </remarks>
internal static class TestAccountFixture
{
    /// <summary>Назва env var, яка містить E.164 номер тестового акаунту.</summary>
    public const string EnvVar = "SIGNALCLI_TEST_ACCOUNT";

    /// <summary>Спробувати прочитати env var. <c>null</c> якщо не виставлено.</summary>
    public static string? TryGet() => Environment.GetEnvironmentVariable(EnvVar);

    /// <summary>
    /// Спробувати отримати account, або повернути reason для skip'а.
    /// </summary>
    /// <param name="account">Заповнюється номером якщо env var виставлено; інакше <c>""</c>.</param>
    /// <param name="skipReason">Заповнюється reason'ом якщо env var не виставлено; інакше <c>""</c>.</param>
    /// <returns><c>true</c> якщо account доступний для тесту; <c>false</c> якщо треба skip.</returns>
    public static bool TryGetOrSkip(out string account, out string skipReason)
    {
        var v = TryGet();
        if (string.IsNullOrEmpty(v))
        {
            account = string.Empty;
            skipReason = $"No {EnvVar} env var; integration test requires registered test account.";
            return false;
        }
        account = v;
        skipReason = string.Empty;
        return true;
    }
}
