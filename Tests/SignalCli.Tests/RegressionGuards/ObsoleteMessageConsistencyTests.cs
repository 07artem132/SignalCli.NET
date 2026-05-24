using System.Reflection;
using System.Text.RegularExpressions;
using SignalCli.Models;

namespace SignalCli.Tests.RegressionGuards;

/// <summary>
/// audit-followup-2026 (regression-guards): пінує CLAUDE.md "Backward compatibility convention →
/// Doc-sync invariant" — кожне <c>[Obsolete("…will be removed in N.0")]</c>-повідомлення SHALL
/// мати N строго більше за поточний major. Drift тут (як знайдено в аудиті 2026-05-24 — 6 сайтів
/// казали "3.0" при 3.0.0-релізі) робить deprecation-timeline ненадійним для consumer'ів.
/// </summary>
public sealed partial class ObsoleteMessageConsistencyTests
{
    [GeneratedRegex(@"will be removed in (\d+)\.0", RegexOptions.IgnoreCase)]
    private static partial Regex RemovalVersionRegex();

    [Fact]
    public void AllObsoleteAttributes_HaveValidRemovalVersion()
    {
        var assembly = typeof(SignalCliOptions).Assembly;
        var currentMajor = assembly.GetName().Version?.Major
            ?? throw new InvalidOperationException("Assembly version not available");

        var offending = new List<string>();

        // Реф­лективно проходимо всі члени всіх типів — public + non-public, instance + static.
        const BindingFlags AllMembers = BindingFlags.Public | BindingFlags.NonPublic
                                       | BindingFlags.Instance | BindingFlags.Static
                                       | BindingFlags.DeclaredOnly;

        foreach (var type in assembly.GetTypes())
        {
            CheckMember(type, type, currentMajor, offending);
            foreach (var member in type.GetMembers(AllMembers))
            {
                CheckMember(member, type, currentMajor, offending);
            }
        }

        Assert.True(
            offending.Count == 0,
            $"[Obsolete] messages з removal-version <= current major ({currentMajor}) — drift, який " +
            $"треба полагодити. Бажано підняти version до {currentMajor + 1}.0+:\n  " +
            string.Join("\n  ", offending));
    }

    private static void CheckMember(MemberInfo member, Type declaringType, int currentMajor, List<string> offending)
    {
        foreach (var attr in member.GetCustomAttributes<ObsoleteAttribute>(inherit: false))
        {
            var message = attr.Message ?? string.Empty;
            var match = RemovalVersionRegex().Match(message);
            if (!match.Success) continue;

            if (!int.TryParse(match.Groups[1].Value, out var removalMajor)) continue;

            if (removalMajor <= currentMajor)
            {
                var memberName = member is Type t ? t.FullName : $"{declaringType.FullName}.{member.Name}";
                offending.Add($"{memberName} → \"{message}\" (removalMajor={removalMajor}, currentMajor={currentMajor})");
            }
        }
    }
}
