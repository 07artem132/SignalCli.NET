using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using SignalCli.Exceptions;
using SignalCli.Interfaces.SignalCli;
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
    /// Создаёт тестируемый JsonRpcClient с замоканным IStreamPairProvider.
    /// </summary>
    private JsonRpcClient CreateClient()
    {
        return new JsonRpcClient(
            _loggerMock.Object,
            _streamProviderMock.Object
        );
    }

    /// <summary>
    /// Генерирует новый <see cref="StreamPair"/> через OnNext(...).
    /// Принимает на вход 3 объекта: (inputWriter, outputReader, errorReader).
    /// </summary>
    private void PushStreamPair(StreamWriter input, StreamReader output, StreamReader error)
    {
        var pair = new StreamPair(input, output, error);
        _currentStreamPair = pair;
        _streamPairSubject.OnNext(pair);
    }

    /// <summary>
    /// Посылает в OnNext(null) - имитируя потерю текущего стрим-пара.
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

        // Создаём некий MemoryStream для input, output, error
        //чтобы SendRequestAsync не падал на "No active stream pair".
        var inputStream = new MemoryStream();
        var inputWriter = new StreamWriter(inputStream);
        var outputReader = new StreamReader(new MemoryStream());
        var errorReader = new StreamReader(new MemoryStream());

        // «Включаем» стрим-пару
        PushStreamPair(inputWriter, outputReader, errorReader);

        // Запускаем запрос
        var invokeTask = client.InvokeMethodAsync<object, object>("testMethod", new { });

        // Act — имитируем, что стрим-пара пропала
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

        // Аналогично создаём MemoryStream для input, output, error
        var inputWriter = new StreamWriter(new MemoryStream());
        var outputReader = new StreamReader(new MemoryStream());
        var errorReader = new StreamReader(new MemoryStream());
        PushStreamPair(inputWriter, outputReader, errorReader);

        var invokeTask = client.InvokeMethodAsync<object, object>("test", new { });

        // Act
        client.Dispose(); // Диспоз → отмена всех pending-запросов

        // Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => invokeTask);
    }

    [Fact]
    public async Task InvokeMethodAsync_WhenResponseArrives_ShouldCompleteTask()
    {
        // Arrange
        var client = CreateClient();

        // Создаём MemoryStream'ы
        var inputWriter = new StreamWriter(new MemoryStream());
        var outputReader = new StreamReader(new MemoryStream());
        var errorReader = new StreamReader(new MemoryStream());
        PushStreamPair(inputWriter, outputReader, errorReader);

        // Запускаем запрос
        var invokeTask = client.InvokeMethodAsync<TestResponse, object>("someMethod", new { param = 123 });

        // Имитация «ответа» с id=1 (если счётчик 0 => первый запрос → "1")
        var json = @"{ ""id"": ""1"", ""result"": { ""Foo"": ""bar"" } }";

        // Вызываем ProcessMessage(...) рефлексией, т.к. он приватный
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

        // Будем проверять, что в inputWriter «записался» JSON
        var inputStream = new MemoryStream();
        var inputWriter = new StreamWriter(inputStream);
        var outputReader = new StreamReader(new MemoryStream());
        var errorReader = new StreamReader(new MemoryStream());
        PushStreamPair(inputWriter, outputReader, errorReader);

        // Act — отправляем запрос
        _ = client.InvokeMethodAsync<JToken, object>("myMethod", new { Hello = "world" });

        // Дадим чуть времени, чтобы SendRequestAsync успел записать
        await Task.Delay(50);

        // Assert: читаем из inputStream, смотрим что там
        inputWriter.Flush(); // не забудьте сбросить буфер
        inputStream.Seek(0, SeekOrigin.Begin);

        using var reader = new StreamReader(inputStream);
        var written = await reader.ReadToEndAsync();
        Assert.Contains(@"""method"":""myMethod""", written);
        Assert.Contains(@"""Hello"":""world""", written);

        // Проверим, что в логах есть "Sent JSON-RPC request"
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Відправлено JSON-RPC запит")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    // Вспомогательный метод для приватного ProcessMessage(...)
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