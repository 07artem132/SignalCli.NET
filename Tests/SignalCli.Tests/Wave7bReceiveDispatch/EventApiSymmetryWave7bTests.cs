using System.Reflection;
using SignalCli.Interfaces.Signal;

namespace SignalCli.Tests.Wave7bReceiveDispatch;

/// <summary>
/// signal-cli-api-coverage Wave 7b: pin'ить що 7 нових event-streams мають парні
/// IObservable + IAsyncEnumerable (RG06 compliance — кожен Foo має FoosAsync).
/// Total event-pair-count: 10 (pre-Wave-7b) + 7 (Wave 7b) = 17.
/// </summary>
public class EventApiSymmetryWave7bTests
{
    [Theory]
    [InlineData("PollCreates", "PollCreatesAsync")]
    [InlineData("PollVotes", "PollVotesAsync")]
    [InlineData("PollTerminates", "PollTerminatesAsync")]
    [InlineData("Payments", "PaymentsAsync")]
    [InlineData("PinMessages", "PinMessagesAsync")]
    [InlineData("UnpinMessages", "UnpinMessagesAsync")]
    [InlineData("AdminDeletes", "AdminDeletesAsync")]
    public void Wave7b_EventPair_HasBothObservableAndAsyncEnumerable(string observableName, string asyncMethodName)
    {
        var type = typeof(ISignalEventService);

        var prop = type.GetProperty(observableName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        // Property type — IObservable<T>
        Assert.True(prop.PropertyType.IsGenericType);
        Assert.Equal(typeof(IObservable<>), prop.PropertyType.GetGenericTypeDefinition());

        var asyncMethod = type.GetMethod(asyncMethodName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(asyncMethod);
        // Returns IAsyncEnumerable<T>
        Assert.True(asyncMethod.ReturnType.IsGenericType);
        Assert.Equal(typeof(IAsyncEnumerable<>), asyncMethod.ReturnType.GetGenericTypeDefinition());

        // Element types match (T у IObservable<T> == T у IAsyncEnumerable<T>).
        var observableT = prop.PropertyType.GetGenericArguments()[0];
        var asyncT = asyncMethod.ReturnType.GetGenericArguments()[0];
        Assert.Equal(observableT, asyncT);
    }

    [Fact]
    public void Wave7b_AllSevenStreamsDeclaredOnInterface()
    {
        // Sanity: 7 нових IObservable properties на ISignalEventService.
        var type = typeof(ISignalEventService);
        var wave7bProperties = new[] { "PollCreates", "PollVotes", "PollTerminates", "Payments", "PinMessages", "UnpinMessages", "AdminDeletes" };
        foreach (var name in wave7bProperties)
        {
            Assert.NotNull(type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance));
        }
    }
}
