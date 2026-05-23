using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using SignalCli.Exceptions;
using SignalCli.Interfaces.SignalCli;
using SignalCli.Models;
using SignalCli.Models.Rpc;
using SignalCli.Models.Signal.Events;
using SignalCli.Models.SignalCli;
using SignalCli.Services.Rpc;

namespace SignalCli.Tests;

public class JsonRpcClientTests
{
    private readonly Mock<ILogger<JsonRpcClient>> _loggerMock;
    private readonly Mock<IStreamPairProvider> _streamProviderMock;
    private readonly Subject<StreamPair?> _streamPairSubject;
    private StreamPair? _currentStreamPair { get; set; }

    public JsonRpcClientTests()
    {
        _loggerMock = new Mock<ILogger<JsonRpcClient>>();
        _loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _streamProviderMock = new Mock<IStreamPairProvider>();
        _streamPairSubject = new Subject<StreamPair?>();

        // По умолчанию CurrentStreamPair = null
        _streamProviderMock
            .SetupGet(sp => sp.CurrentStreamPair)
            .Returns(() => _currentStreamPair);

        _streamProviderMock
            .SetupGet(sp => sp.StreamPairChanged)
            .Returns(_streamPairSubject);
    }

    /// <summary>
    /// Створює клієнт JsonRpcClient з замоканим IStreamPairProvider.
    /// Дефолтний таймаут — великий, щоб тести з очікуванням відповіді не падали з TimeoutException.
    /// </summary>
    private JsonRpcClient CreateClient(int requestTimeoutSeconds = 60)
    {
        var config = new Config
        {
            AppHome = Path.GetTempPath(),
            JavaExecutable = string.Empty,
            LibDirectory = string.Empty,
            RequestTimeoutSeconds = requestTimeoutSeconds
        };
        // D.4: JsonRpcClient тепер приймає SignalCliOptions; конвертуємо legacy Config.
        return new JsonRpcClient(
            _loggerMock.Object,
            _streamProviderMock.Object,
            config.ToOptions()
        );
    }

    /// <summary>
    /// Генерує нову <see cref="StreamPair"/> через OnNext(...).
    /// Приймає на вхід 3 об'єкти: (inputWriter, outputReader, errorReader).
    /// </summary>
    private void PushStreamPair(StreamWriter input, StreamReader output, StreamReader error)
    {
        var pair = new StreamPair(input, output, error);
        _currentStreamPair = pair;
        _streamPairSubject.OnNext(pair);
    }

    /// <summary>
    /// Надсилає в OnNext(null) — імітуючи втрату поточної стрім-пари.
    /// </summary>
    private void PushNullStreamPair()
    {
        _streamPairSubject.OnNext(null);
    }


    [Fact]
    public void Ctor_ShouldSubscribeToStreamPairChanged()
    {
        // Arrange + Act
        var client = CreateClient();

        // Assert
        Assert.NotNull(client);
        _streamProviderMock.VerifyGet(sp => sp.StreamPairChanged, Times.AtLeastOnce);
    }

    [Fact]
    public async Task OnStreamPairChanged_Null_ShouldCancelAllPendingRequests()
    {
        // Arrange
        var client = CreateClient();

        // Створюємо MemoryStream для input, output, error,
        // щоб SendRequestAsync не падав на "No active stream pair".
        var inputStream = new MemoryStream();
        var inputWriter = new StreamWriter(inputStream);
        var outputReader = new StreamReader(new MemoryStream());
        var errorReader = new StreamReader(new MemoryStream());

        // «Включаем» стрим-пару
        PushStreamPair(inputWriter, outputReader, errorReader);

        // Запускаємо запит
        var invokeTask = client.InvokeMethodAsync<object, object>("testMethod", new { });

        // Act — імітуємо, що стрім-пара зникла
        PushNullStreamPair();

        // Assert
        // Должен прилететь TaskCanceledException для invokeTask
        await Assert.ThrowsAsync<TaskCanceledException>(() => invokeTask);
    }

    [Fact]
    public async Task Dispose_ShouldCancelPendingRequests()
    {
        // Arrange
        var client = CreateClient();

        // Аналогічно створюємо MemoryStream для input, output, error
        var inputWriter = new StreamWriter(new MemoryStream());
        var outputReader = new StreamReader(new MemoryStream());
        var errorReader = new StreamReader(new MemoryStream());
        PushStreamPair(inputWriter, outputReader, errorReader);

        var invokeTask = client.InvokeMethodAsync<object, object>("test", new { });

        // Act
        // A.6: IJsonRpcClient тепер IAsyncDisposable-only.
        await client.DisposeAsync();

        // Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => invokeTask);
    }

    [Fact]
    public async Task InvokeMethodAsync_WhenResponseArrives_ShouldCompleteTask()
    {
        // Arrange
        var client = CreateClient();

        // Створюємо MemoryStream'и
        var inputWriter = new StreamWriter(new MemoryStream());
        var outputReader = new StreamReader(new MemoryStream());
        var errorReader = new StreamReader(new MemoryStream());
        PushStreamPair(inputWriter, outputReader, errorReader);

        // Запускаємо запит
        var invokeTask = client.InvokeMethodAsync<TestResponse, object>("someMethod", new { param = 123 });

        // Імітація «відповіді» з id=1 (якщо лічильник 0 => перший запит → "1")
        var json = @"{ ""id"": ""1"", ""result"": { ""Foo"": ""bar"" } }";

        // Викликаємо ProcessMessage(...) через рефлексію, бо він приватний
        CallProcessMessage(client, json);

        // Assert
        var result = await invokeTask;
        Assert.Equal("bar", result.Foo);
    }

    [Fact]
    public async Task InvokeMethodAsync_WhenErrorComes_ShouldThrowJsonRpcException()
    {
        // Arrange
        var client = CreateClient();

        var inputWriter = new StreamWriter(new MemoryStream());
        var outputReader = new StreamReader(new MemoryStream());
        var errorReader = new StreamReader(new MemoryStream());
        PushStreamPair(inputWriter, outputReader, errorReader);

        var invokeTask = client.InvokeMethodAsync<object, object>("test", new { });

        // Ответ с error
        var response = @"{
            ""id"": ""1"",
            ""error"": { ""code"": -123, ""message"": ""Something went wrong"" }
        }";
        CallProcessMessage(client, response);

        // Assert
        var ex = await Assert.ThrowsAsync<JsonRpcException>(() => invokeTask);
        Assert.Equal("Something went wrong", ex.Message);
        //Assert.Equal(-123, ex.Code);
    }

    [Fact]
    public void ProcessMessage_WhenNotification_ShouldPushToNotifications()
    {
        // Arrange
        var client = CreateClient();
        var received = new List<JsonRpcNotification<SubscriptionEventArgs>>();

        using var sub = client.Notifications.Subscribe(n => received.Add(n));

        var notificationJson = @"{
            ""jsonrpc"": ""2.0"",
            ""method"": ""subscribe"",
            ""params"": {
                ""subscription"": 0,
            }
        }";

        // Act
        CallProcessMessage(client, notificationJson);

        // Assert
        Assert.Single(received);
        Assert.Equal("subscribe", received[0].Method);
        Assert.Equal(0, received[0].Params.Subscription);
    }

    [Fact]
    public async Task SendRequestAsync_ShouldWriteJsonToInputStream()
    {
        // Arrange
        var client = CreateClient();

        // Перевірятимемо, що в inputWriter «записався» JSON
        var inputStream = new MemoryStream();
        var inputWriter = new StreamWriter(inputStream);
        var outputReader = new StreamReader(new MemoryStream());
        var errorReader = new StreamReader(new MemoryStream());
        PushStreamPair(inputWriter, outputReader, errorReader);

        // Act — отправляем запрос
        _ = client.InvokeMethodAsync<JsonElement, object>("myMethod", new { Hello = "world" });

        // Даємо трохи часу, щоб SendRequestAsync встиг записати
        await Task.Delay(50);

        // Assert: читаємо з inputStream, дивимось, що там
        inputWriter.Flush(); // не забудьте сбросить буфер
        inputStream.Seek(0, SeekOrigin.Begin);

        using var reader = new StreamReader(inputStream);
        var written = await reader.ReadToEndAsync();
        Assert.Contains(@"""method"":""myMethod""", written);
        Assert.Contains(@"""Hello"":""world""", written);

        // Сирий JSON-запит логуємо лише на Trace (приватність: містить тіло повідомлення).
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Відправлено JSON-RPC запит")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task A3_InvokeMethodAsync_SilentProcess_FaultsWithTimeoutException()
    {
        // A.3 (F1): процес живий, але мовчить — виклик має зафейлитись TimeoutException,
        // а не висіти вічно. Користуємось дуже коротким таймаутом.
        var client = CreateClient(requestTimeoutSeconds: 1);

        var inputWriter = new StreamWriter(new MemoryStream());
        var outputReader = new StreamReader(new MemoryStream());
        var errorReader = new StreamReader(new MemoryStream());
        PushStreamPair(inputWriter, outputReader, errorReader);

        var invokeTask = client.InvokeMethodAsync<TestResponse, object>("silent", new { });

        // Очікуємо точно TimeoutException у межах кількох таймаутів (бо є тонкі race-вікна).
        var ex = await Assert.ThrowsAsync<TimeoutException>(async () =>
            await invokeTask.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains("silent", ex.Message);
    }

    [Fact]
    public async Task A3_InvokeMethodAsync_CallerCancel_DoesNotMaskAsTimeoutException()
    {
        // A.3 (F1) часткова: якщо callerCancel спрацював раніше за timeout,
        // має вилетіти OperationCanceledException, а НЕ TimeoutException.
        var client = CreateClient(requestTimeoutSeconds: 10);

        var inputWriter = new StreamWriter(new MemoryStream());
        var outputReader = new StreamReader(new MemoryStream());
        var errorReader = new StreamReader(new MemoryStream());
        PushStreamPair(inputWriter, outputReader, errorReader);

        using var cts = new CancellationTokenSource();
        var invokeTask = client.InvokeMethodAsync<TestResponse, object>("cancel-me", new { }, cts.Token);
        await Task.Delay(50);
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() => invokeTask);
    }

    [Fact]
    public async Task A8_OnStreamPairChanged_StopsPriorReaderBeforeStartingNew()
    {
        // A.8 (F4): після push другої пари, читач першої має бути зупинений
        // (а не залишений із попереднім токеном). Перевіряємо через те, що
        // після другого PushStreamPair виклик на «старій» парі (відповідь з id=2)
        // не доходить, бо pending-запити чистяться, а нові читачі читають з нової пари.
        var client = CreateClient();

        // Перша пара
        var inputA = new StreamWriter(new MemoryStream());
        var outA = new StreamReader(new MemoryStream());
        var errA = new StreamReader(new MemoryStream());
        PushStreamPair(inputA, outA, errA);

        // Pending запит на першій парі.
        var first = client.InvokeMethodAsync<TestResponse, object>("first", new { });

        // Друга пара — push має скасувати pending.
        var inputB = new StreamWriter(new MemoryStream());
        var outB = new StreamReader(new MemoryStream());
        var errB = new StreamReader(new MemoryStream());
        PushStreamPair(inputB, outB, errB);

        await Assert.ThrowsAsync<TaskCanceledException>(() => first);
    }

    // Допоміжний метод для приватного ProcessMessage(...)
    private static void CallProcessMessage(JsonRpcClient client, string json)
    {
        var methodInfo = typeof(JsonRpcClient)
            .GetMethod("ProcessMessage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        methodInfo!.Invoke(client, new object[] { json });
    }

    private class TestResponse
    {
        public string Foo { get; set; }
    }
}