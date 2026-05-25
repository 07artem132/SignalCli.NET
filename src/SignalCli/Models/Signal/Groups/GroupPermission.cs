using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Groups;

/// <summary>
/// Хто має право на конкретну group-action (add-member / edit-details / send-messages).
/// Wire-shape — kebab-case: <c>"every-member"</c> / <c>"only-admins"</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 2. Pinned до
/// <c>lib/src/main/java/org/asamk/signal/manager/api/GroupPermission.java @ bda4e7fc</c>.
/// camelCase (<c>everyMember</c>/<c>onlyAdmins</c>) теж приймається upstream'ом як alias.
/// <para>
/// Для <c>send-messages</c> permission upstream нормалізує значення у boolean
/// <c>isAnnouncementGroup</c> (<c>OnlyAdmins → true</c>, <c>EveryMember → false</c>) —
/// але це internal-translation у command-handler'і; wire-shape тут стандартний.
/// </para>
/// </remarks>
[PublicAPI]
public enum GroupPermission
{
    /// <summary>Будь-який member може виконати дію. Wire: <c>"every-member"</c>.</summary>
    EveryMember,

    /// <summary>Тільки admin'и можуть виконати дію. Wire: <c>"only-admins"</c>.</summary>
    OnlyAdmins,
}
