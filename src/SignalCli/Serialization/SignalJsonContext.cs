using System.Text.Json;
using System.Text.Json.Serialization;
using SignalCli.Models.Rpc;
using SignalCli.Models.Signal;
using SignalCli.Models.Signal.Accounts;
using SignalCli.Models.Signal.Devices;
using SignalCli.Models.Signal.Events;
using SignalCli.Models.Signal.Groups;
using SignalCli.Models.Signal.Message;
using SignalCli.Models.SignalCli;

namespace SignalCli.Serialization;

/// <summary>
/// Source-generated контекст серіалізації для всіх типів протоколу signal-cli.
/// Метадані генеруються на етапі компіляції (швидший старт, менше памʼяті, trim-safe).
/// Вкладені типи (наприклад усі Json*-типи з Envelope) генератор підхоплює автоматично.
/// </summary>
// post-modernize-tuning §6.6 (audit B6): GenerationMode = Default дає і metadata, і fast-path
// серіалізацію — згенерований код може писати JSON напряму через Utf8JsonWriter без обходу
// JsonTypeInfo, що дає помітний перфоманс-приріст для гарячих write-шляхів (Microsoft
// "Reflection vs source generation" — Default is faster than Metadata-only).
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]
// JSON-RPC
[JsonSerializable(typeof(JsonRpcRequest))]
[JsonSerializable(typeof(JsonRpcResponse))]
[JsonSerializable(typeof(JsonRpcError))]
[JsonSerializable(typeof(JsonRpcNotificationRaw))]
[JsonSerializable(typeof(JsonRpcNotification<SubscriptionEventArgs>))]
// Events / receive
[JsonSerializable(typeof(SubscriptionEventArgs))]
[JsonSerializable(typeof(SignalEventArgs))]
[JsonSerializable(typeof(JsonMessageEnvelope))]
[JsonSerializable(typeof(SubscribeReceiveParameters))]
[JsonSerializable(typeof(SubscribeReceiveResponse))]
[JsonSerializable(typeof(UnsubscribeReceiveParameters))]
[JsonSerializable(typeof(UnsubscribeReceiveResponse))]
// System
[JsonSerializable(typeof(VersionParameters))]
[JsonSerializable(typeof(VersionResponse))]
// Accounts
[JsonSerializable(typeof(ListAccountsParameters))]
[JsonSerializable(typeof(ListAccountsResponse))]
// §4.20 (N10): wrapper-record's converter delegates to List<Account>. Source-gen
// (як of .NET 7+) не має reflection-fallback, тож inner-collection треба реєструвати окремо.
[JsonSerializable(typeof(List<Account>))]
[JsonSerializable(typeof(Account))]
[JsonSerializable(typeof(SyncAccountsParameters))]
[JsonSerializable(typeof(SyncAccountsResponse))]
// Devices
[JsonSerializable(typeof(StartLinkParameters))]
[JsonSerializable(typeof(StartLinkResponse))]
[JsonSerializable(typeof(FinishLinkParameters))]
[JsonSerializable(typeof(FinishLinkResponse))]
// Groups
[JsonSerializable(typeof(ListGroupsParameters))]
[JsonSerializable(typeof(ListGroupsResponse))]
// §4.20 (N10): inner-collection для wrapper-converter'а.
[JsonSerializable(typeof(List<Group>))]
[JsonSerializable(typeof(Group))]
// Message
[JsonSerializable(typeof(SendMessageFullParameters))]
[JsonSerializable(typeof(SendMessageResponse))]
// post-modernize-tuning §6.7 (audit P6): JsonElement використовується як TResponse
// у SignalEventService.SubscribeAsync (signal-cli повертає raw id або помилку); після
// AOT-міграції потрібен JsonTypeInfo<JsonElement>. Source-gen надає pass-through-typeinfo.
[JsonSerializable(typeof(JsonElement))]
internal partial class SignalJsonContext : JsonSerializerContext;
