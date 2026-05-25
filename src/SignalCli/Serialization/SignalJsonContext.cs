using System.Text.Json;
using System.Text.Json.Serialization;
using SignalCli.Models.Rpc;
using SignalCli.Models.Signal;
using SignalCli.Models.Signal.Accounts;
using SignalCli.Models.Signal.Contacts;
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
//
// json-hardening-source-gen-attribute (audit v2.1 follow-up): AllowDuplicateProperties = false
// — генератор вшиває duplicate-key check у згенерований Utf8JsonReader-loop. CLAUDE.md
// rule #18 раніше виставляв цей флаг тільки на runtime-options (SignalJson.Options), але
// production-шлях через SignalJsonContext.Default.X обходив його (fast-path читає JSON напряму).
// Тепер захист fail-loud діє на ОБОХ шляхах — runtime + source-gen. Pinned RG05 ×3 facts у
// JsonSerializationTests.cs.
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Default,
    AllowDuplicateProperties = false)]
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
// Groups — Wave 2 groups-crud (signal-cli-api-coverage):
// 3 нові методи (joinGroup/updateGroup/quitGroup) кожен з парою Parameters/Response.
// Response типи окремі (а НЕ reuse SendMessageResponse) бо wire shapes різні:
// JoinGroupResponse додає {groupId, onlyRequested?}; UpdateGroupResponse все-nullable
// + dimorphic groupId; QuitGroupResponse теж все-nullable бо §F8 NotAGroupMember → {}.
[JsonSerializable(typeof(JoinGroupParameters))]
[JsonSerializable(typeof(JoinGroupResponse))]
[JsonSerializable(typeof(UpdateGroupParameters))]
[JsonSerializable(typeof(UpdateGroupResponse))]
[JsonSerializable(typeof(QuitGroupParameters))]
[JsonSerializable(typeof(QuitGroupResponse))]
// Contacts — Wave 3 contacts-identity (signal-cli-api-coverage):
// 8 нових RPC методів. ListContacts/Identities — flat-array wrapper-record pattern
// (як ListAccountsResponse). 6 void-методів (Trust*/UpdateContact/RemoveContact/
// UpdateProfile/Block/Unblock) використовують JsonElement як response-тип
// (signal-cli emit'ить `"result": null` для void).
[JsonSerializable(typeof(ListContactsParameters))]
[JsonSerializable(typeof(ListContactsResponse))]
[JsonSerializable(typeof(List<JsonContact>))]
[JsonSerializable(typeof(JsonContact))]
[JsonSerializable(typeof(JsonContactProfile))]
[JsonSerializable(typeof(JsonContactInternal))]
[JsonSerializable(typeof(ListIdentitiesParameters))]
[JsonSerializable(typeof(ListIdentitiesResponse))]
[JsonSerializable(typeof(List<JsonIdentity>))]
[JsonSerializable(typeof(JsonIdentity))]
[JsonSerializable(typeof(TrustParameters))]
[JsonSerializable(typeof(UpdateContactParameters))]
[JsonSerializable(typeof(RemoveContactParameters))]
[JsonSerializable(typeof(UpdateProfileParameters))]
[JsonSerializable(typeof(BlockParameters))]
[JsonSerializable(typeof(UnblockParameters))]
// Message
[JsonSerializable(typeof(SendMessageFullParameters))]
[JsonSerializable(typeof(SendMessageResponse))]
// Message — Wave 1 messaging-interactive (signal-cli-api-coverage):
// 4 send-методи reuse'ять існуючий SendMessageResponse (research §Cross-method §4
// confirms identical wire shape `{ timestamp, results }` для всіх sendReaction/Receipt/
// Typing/RemoteDelete). Окремі Response-типи створювати не потрібно.
[JsonSerializable(typeof(SendReactionParameters))]
[JsonSerializable(typeof(SendReceiptParameters))]
[JsonSerializable(typeof(SendTypingParameters))]
[JsonSerializable(typeof(RemoteDeleteParameters))]
// post-modernize-tuning §6.7 (audit P6): JsonElement використовується як TResponse
// у SignalEventService.SubscribeAsync (signal-cli повертає raw id або помилку); після
// AOT-міграції потрібен JsonTypeInfo<JsonElement>. Source-gen надає pass-through-typeinfo.
[JsonSerializable(typeof(JsonElement))]
internal partial class SignalJsonContext : JsonSerializerContext;
