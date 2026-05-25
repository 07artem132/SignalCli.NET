using System.Reflection;
using Microsoft.Extensions.Logging;
using SignalCli.Logging;

namespace SignalCli.Tests.RegressionGuards;

/// <summary>
/// audit-followup-2026 (regression-guards): пінує CLAUDE.md "Established patterns → Logging →
/// EventId blocks reserved per service". Якщо новий <c>[LoggerMessage(EventId = X, …)]</c>
/// landed з X поза блоком декларованим у CLAUDE.md, тест валиться з конкретним повідомленням
/// про який слот зайняти.
/// </summary>
public sealed class EventIdBlockTests
{
    public static IEnumerable<object[]> LogClassesAndBlocks() =>
    [
        [typeof(SignalCliHostedServiceLog), 100, 199],
        [typeof(SignalCliHealthMonitorLog), 200, 299],
        [typeof(JsonRpcClientLog), 300, 399],
        [typeof(JsonRpcClientHostedServiceLog), 400, 499],
        [typeof(SignalEventServiceLog), 500, 599],
        [typeof(SignalServiceLog), 600, 699],
        [typeof(SignalMessageLog), 700, 799],
        [typeof(SignalAccountsLog), 800, 899],
        [typeof(SignalDevicesLog), 800, 899],
        [typeof(SignalGroupsLog), 800, 899],
        // signal-cli-api-coverage Wave 3 (contacts-identity): new SignalContactsLog
        // shares the 800-899 block (Contacts uses 830-849 sub-range).
        [typeof(SignalContactsLog), 800, 899],
        [typeof(ProcessRunnerLog), 900, 999],
        [typeof(ProcessStateManagerLog), 900, 999],
    ];

    [Theory]
    [MemberData(nameof(LogClassesAndBlocks))]
    public void EventIds_InReservedBlock(Type logClass, int lo, int hi)
    {
        var offending = new List<string>();

        const BindingFlags MethodFlags = BindingFlags.Public | BindingFlags.NonPublic
                                        | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var method in logClass.GetMethods(MethodFlags))
        {
            var attr = method.GetCustomAttribute<LoggerMessageAttribute>();
            if (attr is null) continue;

            if (attr.EventId < lo || attr.EventId > hi)
            {
                offending.Add($"{logClass.Name}.{method.Name} → EventId={attr.EventId} ∉ [{lo}, {hi}]");
            }
        }

        Assert.True(
            offending.Count == 0,
            $"{logClass.Name} має [LoggerMessage]-методи з EventId поза блоком [{lo}, {hi}]:\n  " +
            string.Join("\n  ", offending) +
            "\nCLAUDE.md \"Established patterns → Logging\" резервує блок per-service; не змішуй.");
    }
}
