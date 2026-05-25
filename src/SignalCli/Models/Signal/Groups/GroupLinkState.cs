using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Groups;

/// <summary>
/// Стан публічного посилання-запрошення групи. Wire-shape — kebab-case рядки:
/// <c>"enabled"</c> / <c>"enabled-with-approval"</c> / <c>"disabled"</c>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 2 (`groups-crud`). Pinned до
/// <c>lib/src/main/java/org/asamk/signal/manager/api/GroupLinkState.java @ bda4e7fc</c>.
/// <para>
/// Upstream приймає і kebab-case (argparse <c>.choices</c>) і camelCase
/// (<c>enabledWithApproval</c>) як alias — server-side fallback у
/// <c>UpdateGroupCommand.java:82-92</c>. Canonical вихідний wire-формат — kebab-case.
/// Service-метод мапить .NET PascalCase → kebab-string перед серіалізацією.
/// </para>
/// </remarks>
[PublicAPI]
public enum GroupLinkState
{
    /// <summary>Посилання-запрошення активне; будь-хто з посиланням стає member'ом одразу.</summary>
    Enabled,

    /// <summary>Посилання активне, але join потребує admin-approval. Wire: <c>"enabled-with-approval"</c>.</summary>
    EnabledWithApproval,

    /// <summary>Посилання вимкнено; новий join можливий лише через прямі add-member виклики admin'ів.</summary>
    Disabled,
}
