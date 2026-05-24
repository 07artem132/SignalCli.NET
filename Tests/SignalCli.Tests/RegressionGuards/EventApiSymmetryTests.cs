using System.Reflection;
using SignalCli.Interfaces.Signal;

namespace SignalCli.Tests.RegressionGuards;

/// <summary>
/// RG06 — NF-002 (audit v2.1): кожна <see cref="IObservable{T}"/>-властивість на
/// <see cref="ISignalEventService"/> МУСИТЬ мати парний <see cref="IAsyncEnumerable{T}"/>-метод
/// з тим самим <c>TEventArgs</c> + <see cref="CancellationToken"/> з default-value.
/// </summary>
/// <remarks>
/// CLAUDE.md "Established patterns → Event streams: two surfaces" вимагає
/// "Each event kind in SignalEventService has BOTH an IObservable&lt;T&gt; and an
/// IAsyncEnumerable&lt;T&gt;". До цього патерн enforcъ'вся соціально — додавши новий
/// <c>IObservable&lt;NewEventArgs&gt; News { get; }</c> без парного <c>NewsAsync(ct)</c>,
/// код би скомпілився й шипнувся. Цей guard ловить таку ситуацію build-time.
/// </remarks>
public class EventApiSymmetryTests
{
    /// <summary>
    /// Виключення з конвенції <c>${PropertyName}Async</c> — поки що лише одне:
    /// <c>TypingNotifications</c> → <c>TypingAsync</c>. Якщо додаєте новий виняток —
    /// одразу запишіть тут і обґрунтуйте у CHANGELOG.
    /// </summary>
    private static readonly Dictionary<string, string> NonStandardPairings = new()
    {
        ["TypingNotifications"] = "TypingAsync",
    };

    [Fact]
    public void EveryObservableProperty_HasPairedAsyncEnumerableMethod()
    {
        var iface = typeof(ISignalEventService);
        var observableProps = iface.GetProperties()
            .Where(p => p.PropertyType.IsGenericType
                        && p.PropertyType.GetGenericTypeDefinition() == typeof(IObservable<>))
            .ToArray();

        // Sanity: не менш як 10 паттернів (TextMessages, Reaction, Attachments, Sticker,
        // TypingNotifications, Receipts, Syncs, Quotes, Edits, RemoteDeletes).
        Assert.True(observableProps.Length >= 10,
            $"Expected >= 10 IObservable<T> properties on ISignalEventService; got {observableProps.Length}.");

        var failures = new List<string>();
        foreach (var prop in observableProps)
        {
            var eventArgsType = prop.PropertyType.GetGenericArguments()[0];
            var expectedMethodName = NonStandardPairings.TryGetValue(prop.Name, out var alt)
                ? alt
                : prop.Name + "Async";

            var method = iface.GetMethod(
                expectedMethodName,
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: [typeof(CancellationToken)],
                modifiers: null);

            if (method is null)
            {
                failures.Add(
                    $"{prop.Name}: missing paired {expectedMethodName}(CancellationToken)");
                continue;
            }

            var expectedReturn = typeof(IAsyncEnumerable<>).MakeGenericType(eventArgsType);
            if (method.ReturnType != expectedReturn)
            {
                failures.Add(
                    $"{prop.Name}: {expectedMethodName} returns {method.ReturnType.Name}, " +
                    $"expected IAsyncEnumerable<{eventArgsType.Name}>");
            }

            var paramInfo = method.GetParameters().Single();
            if (!paramInfo.HasDefaultValue || paramInfo.DefaultValue is not null)
            {
                failures.Add(
                    $"{prop.Name}: {expectedMethodName}(CancellationToken) must declare `= default`");
            }
        }

        Assert.True(
            failures.Count == 0,
            "Event-API symmetry violations (CLAUDE.md \"Event streams: two surfaces\"):\n  "
            + string.Join("\n  ", failures));
    }
}
