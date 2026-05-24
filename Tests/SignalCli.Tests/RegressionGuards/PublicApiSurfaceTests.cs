using System.Globalization;
using System.Reflection;
using System.Text;
using SignalCli.Models;

namespace SignalCli.Tests.RegressionGuards;

/// <summary>
/// audit-followup-2026 (regression-guards): пінує public API surface SignalCli.dll проти
/// baseline-файлу. Reflective walker emit'ить канонічну форму per public type/member, sorts,
/// порівнює із зеленим baseline. Будь-яке випадкове додавання/видалення public-члена → тест
/// валиться з unified-diff'ом.
/// </summary>
/// <remarks>
/// <b>Як регенерувати baseline</b> після свідомої public-API зміни:
/// <list type="number">
/// <item>Запустити тест — він fail'не з diff'ом.</item>
/// <item>Скопіювати ACTUAL рядки з output'у тесту в файл <c>SignalCli.public-api.txt</c>
///       у цій директорії (replace contents).</item>
/// <item>Reviewer перевіряє baseline-diff у PR як свідому architectural-decision.</item>
/// </list>
/// </remarks>
public sealed class PublicApiSurfaceTests
{
    private const string BaselineFileName = "SignalCli.public-api.txt";

    [Fact]
    public void PublicSurface_MatchesBaseline()
    {
        var actual = GenerateSurface(typeof(SignalCliOptions).Assembly);
        var baselinePath = ResolveBaselinePath();

        if (!File.Exists(baselinePath))
        {
            // Перший запуск — записуємо baseline і fail'имо з підказкою. Це раз-у-житті
            // sequence; reviewer перевіряє, що baseline.txt у PR — це той, що ми очікуємо.
            File.WriteAllText(baselinePath, actual, Encoding.UTF8);
            Assert.Fail($"Baseline file створено в {baselinePath}. Перевірте контент і commit. Наступні запуски будуть pin'ити public API.");
        }

        var expected = File.ReadAllText(baselinePath, Encoding.UTF8).Replace("\r\n", "\n");
        actual = actual.Replace("\r\n", "\n");

        if (expected != actual)
        {
            var diff = BuildDiff(expected, actual);
            Assert.Fail(
                "Public API surface drifted from baseline. " +
                "Якщо це навмисна зміна — оновіть baseline-файл " +
                $"({baselinePath}) контентом ACTUAL нижче.\n\n" +
                $"DIFF (- baseline, + actual):\n{diff}");
        }
    }

    private static string GenerateSurface(Assembly assembly)
    {
        var lines = new List<string>();

        foreach (var type in assembly.GetTypes().Where(t => t.IsPublic || t.IsNestedPublic))
        {
            if (type.IsNested && !type.IsNestedPublic) continue;
            lines.Add($"T:{FormatTypeName(type)}");

            const BindingFlags MemberFlags = BindingFlags.Public
                                           | BindingFlags.Instance
                                           | BindingFlags.Static
                                           | BindingFlags.DeclaredOnly;

            // Constructors
            foreach (var ctor in type.GetConstructors(MemberFlags).OrderBy(m => FormatMethodName(m)))
                lines.Add($"M:{FormatTypeName(type)}.{FormatMethodName(ctor)}");

            // Methods (skip property/event accessors — get_*, set_*, add_*, remove_*)
            foreach (var method in type.GetMethods(MemberFlags)
                         .Where(m => !m.IsSpecialName)
                         .OrderBy(m => FormatMethodName(m)))
                lines.Add($"M:{FormatTypeName(type)}.{FormatMethodName(method)}");

            // Properties
            foreach (var prop in type.GetProperties(MemberFlags).OrderBy(p => p.Name))
                lines.Add($"P:{FormatTypeName(type)}.{prop.Name}:{FormatTypeName(prop.PropertyType)}");

            // Events
            foreach (var evt in type.GetEvents(MemberFlags).OrderBy(e => e.Name))
                lines.Add($"E:{FormatTypeName(type)}.{evt.Name}");

            // Fields (public — rare for our codebase)
            foreach (var field in type.GetFields(MemberFlags).OrderBy(f => f.Name))
                lines.Add($"F:{FormatTypeName(type)}.{field.Name}:{FormatTypeName(field.FieldType)}");
        }

        lines.Sort(StringComparer.Ordinal);
        return string.Join("\n", lines) + "\n";
    }

    private static string FormatTypeName(Type type)
    {
        if (type.IsGenericParameter) return type.Name;
        if (!type.IsGenericType) return StripAssemblyMetadata(type.FullName ?? type.Name);
        var genericName = type.GetGenericTypeDefinition().FullName ?? type.Name;
        var stripped = genericName.Contains('`') ? genericName[..genericName.IndexOf('`')] : genericName;
        var args = string.Join(",", type.GetGenericArguments().Select(FormatTypeName));
        return $"{stripped}<{args}>";
    }

    /// <summary>
    /// version-CHANGELOG-lockstep fix (audit v2.1 +1 follow-up): для by-ref/array/pointer типів,
    /// які проходять через <c>type.FullName</c>-гілку, FullName для byref-to-generic повертає
    /// assembly-qualified inner з вшитим <c>Version=X.Y.Z</c>. Кожен bump <c>&lt;SignalCliPackageVersion&gt;</c>
    /// форсував би регенерацію baseline'у — exactly the friction that <c>Directory.Build.props</c>
    /// version-centralization мала усунути. Регулярка стрипає тільки assembly-metadata
    /// (Version/Culture/PublicKeyToken); решта структури типу збережена.
    /// </summary>
    private static string StripAssemblyMetadata(string typeName) =>
        System.Text.RegularExpressions.Regex.Replace(
            typeName,
            @", Version=\d+\.\d+\.\d+\.\d+, Culture=[^,\]]+, PublicKeyToken=[^,\]]+",
            string.Empty);

    private static string FormatMethodName(MethodBase method)
    {
        var sb = new StringBuilder();
        sb.Append(method is ConstructorInfo ? ".ctor" : method.Name);
        if (method.IsGenericMethod || (method is MethodInfo mi && mi.IsGenericMethodDefinition))
        {
            var args = method.GetGenericArguments().Length;
            sb.Append(CultureInfo.InvariantCulture, $"``{args}");
        }
        sb.Append('(');
        sb.Append(string.Join(",", method.GetParameters().Select(p => FormatTypeName(p.ParameterType))));
        sb.Append(')');
        return sb.ToString();
    }

    private static string ResolveBaselinePath()
    {
        // Шукаємо baseline відносно source-файлу тесту, не bin/Debug. Тест-runner запускається
        // з bin/Debug/net10.0; підіймаємось до Tests/SignalCli.Tests/RegressionGuards/.
        var asmDir = Path.GetDirectoryName(typeof(PublicApiSurfaceTests).Assembly.Location)!;
        var candidate = Path.GetFullPath(Path.Combine(asmDir, "..", "..", "..", "RegressionGuards", BaselineFileName));
        return candidate;
    }

    private static string BuildDiff(string expected, string actual)
    {
        var exp = expected.Split('\n');
        var act = actual.Split('\n');
        var expSet = new HashSet<string>(exp);
        var actSet = new HashSet<string>(act);

        var sb = new StringBuilder();
        foreach (var removed in exp.Where(l => !actSet.Contains(l)).Take(50))
            sb.AppendLine(CultureInfo.InvariantCulture, $"- {removed}");
        foreach (var added in act.Where(l => !expSet.Contains(l)).Take(50))
            sb.AppendLine(CultureInfo.InvariantCulture, $"+ {added}");
        return sb.ToString();
    }
}
