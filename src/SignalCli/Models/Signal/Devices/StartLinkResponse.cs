using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Devices;

/// <summary>
/// Відповідь на запит ініціалізації зв'язування пристрою.
/// </summary>
/// <remarks>
/// Містить URI для створення QR-коду, який сканується новим пристроєм
/// для початку процесу зв'язування.
/// fix-startlink-wire-shape (2026-07-16): wire-level JSON поле — camelCase `deviceLinkUri`;
/// без явного <c>[JsonPropertyName]</c> case-sensitive контекст <c>SignalJsonContext</c>
/// (без <c>PropertyNamingPolicy</c>) тихо десеріалізував PascalCase-властивість у <c>null</c>.
/// §0.5 cite-and-read: upstream `org.asamk.signal.commands/StartLinkCommand.java:42 @ v0.14.3`
/// (c554e5c) — <c>private record JsonLink(String deviceLinkUri) {}</c>, записується через
/// <c>jsonWriter.write(new JsonLink(deviceLinkUri.toString()))</c>. Пін: RG10
/// (<c>WireShapeAnnotationTests</c>) + <c>DeviceLinkingSerializationTests</c>.
/// </remarks>
/// <param name="DeviceLinkUri">URI для зв'язування пристрою, використовується для генерації QR-коду.</param>
[PublicAPI]
public sealed record StartLinkResponse([property: JsonPropertyName("deviceLinkUri")] string DeviceLinkUri);
