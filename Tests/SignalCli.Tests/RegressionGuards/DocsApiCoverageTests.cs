using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using SignalCli.Extensions;
using SignalCli.Interfaces.Signal;
using SignalCli.Interfaces.SignalCli;

namespace SignalCli.Tests.RegressionGuards;

/// <summary>
/// RG09 — кожен публічний метод на 8 <c>ISignal*</c> facade-інтерфейсах + raw
/// <see cref="ISignalCliClient"/> + 4 extension-методи <see cref="ServiceCollectionExtensions"/>
/// МУСИТЬ бути згаданий хоча б у одному файлі під <c>docs/api/*.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// Анти-drift guard для документації. Старий аудит NF-006 виявив що CLAUDE.md "Future development
/// guardrails" сайт-bullet pointing на старе значення константи (15M замість 12M); тут же —
/// той самий клас регресій, але для docs/api/*.md.
/// </para>
/// <para>
/// При додаванні нового публічного RPC-методу або extension-у — обовʼязок розробника додати
/// згадку у відповідний docs/api/*.md (signature або принаймні method-name у backtick'ах). Якщо
/// тест fail'ить — fix the doc, не цей тест.
/// </para>
/// </remarks>
public class DocsApiCoverageTests
{
    [Fact]
    public void EveryPublicApiMethod_IsMentionedInDocsApi()
    {
        var docs = LoadAllDocsApiContent();
        var failures = new List<string>();

        foreach (var iface in InterfacesToCheck)
        {
            foreach (var m in iface.GetMethods().Where(IsNonInherited))
            {
                if (!IsMentionedInAnyDoc(m.Name, docs))
                {
                    failures.Add($"{iface.Name}.{m.Name}");
                }
            }
        }

        // ServiceCollectionExtensions — статичні extension'и (C# 14 extension block).
        // GetMethods() повертає сгенеровані extension-метод-decl'ації; фільтруємо за іменем.
        var sceMethods = new[]
        {
            nameof(ServiceCollectionExtensions.AddSignalCli),
            nameof(ServiceCollectionExtensions.AddSignalCliWithBundledRuntimeDefaults),
            nameof(ServiceCollectionExtensions.AddSignalEvents),
        };
        foreach (var name in sceMethods)
        {
            if (!IsMentionedInAnyDoc(name, docs))
            {
                failures.Add($"ServiceCollectionExtensions.{name}");
            }
        }

        Assert.True(
            failures.Count == 0,
            "Docs drift — public API methods not mentioned in any docs/api/*.md " +
            "(see docs/README.md for convention):\n  " +
            string.Join("\n  ", failures));
    }

    private static readonly Type[] InterfacesToCheck =
    [
        typeof(ISignalMessage),
        typeof(ISignalAccounts),
        typeof(ISignalDevices),
        typeof(ISignalGroups),
        typeof(ISignalContacts),
        typeof(ISignalStickers),
        typeof(ISignalResources),
        typeof(ISignalEventService),
        typeof(ISignalCliClient),
    ];

    private static bool IsNonInherited(MethodInfo m)
    {
        // Виключаємо успадковані з IHostedService (StartAsync/StopAsync) — це
        // інфраструктура hosting'у, а не наш RPC-surface. Аналогічно — property getter'и.
        if (m.IsSpecialName) return false;     // property accessors
        if (m.DeclaringType is null) return false;
        if (m.DeclaringType.Namespace?.StartsWith("Microsoft.", StringComparison.Ordinal) == true)
            return false;
        return true;
    }

    private static bool IsMentionedInAnyDoc(string methodName, Dictionary<string, string> docs)
    {
        // Word-boundary match: ім'я методу як ціле slово, обмежене не-identifier-char'ами
        // (лапки, дужки, крапка, пробіл, перенос рядка). Substring-match був би занадто
        // лояльним: `SendReactionAsync` matched би у `SendReactionAsyncRENAMED`.
        // `\b` anchored на `\w` (`[A-Za-z0-9_]`) — точно те, що нам потрібно для C# identifier'ів.
        var pattern = new Regex(@"\b" + Regex.Escape(methodName) + @"\b", RegexOptions.CultureInvariant);
        foreach (var content in docs.Values)
        {
            if (pattern.IsMatch(content))
                return true;
        }
        return false;
    }

    private static Dictionary<string, string> LoadAllDocsApiContent()
    {
        var apiDir = Path.Combine(LocateRepoRoot(), "docs", "api");
        Assert.True(Directory.Exists(apiDir), $"docs/api dir not found at {apiDir}");

        var files = Directory.GetFiles(apiDir, "*.md", SearchOption.TopDirectoryOnly);
        Assert.True(files.Length > 0, "docs/api/*.md is empty — нічого валідувати.");

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in files)
        {
            dict[Path.GetFileName(f)] = File.ReadAllText(f);
        }
        return dict;
    }

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(
            Path.GetDirectoryName(typeof(DocsApiCoverageTests).Assembly.Location)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir.FullName;
    }
}
