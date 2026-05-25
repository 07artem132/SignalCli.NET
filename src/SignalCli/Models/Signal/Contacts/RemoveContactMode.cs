using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Contacts;

/// <summary>
/// Режим видалення контакту. Три distinct behaviors, які upstream signal-cli виражає
/// через два булеані (<c>hide</c>/<c>forget</c>), а ми моделюємо як один enum-параметр
/// щоб уникнути client-side помилки "обидва прапори true одночасно".
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 3. Pinned до
/// <c>src/main/java/org/asamk/signal/commands/RemoveContactCommand.java @ bda4e7fc</c>.
/// <para>
/// <b>§F9 mutex:</b> upstream'ів argparse mutex group enforce'ить це на CLI-рівні,
/// але JSON-RPC шар приймає обидва прапори (тоді hide-first-wins). .NET service-метод
/// перекладає enum у відповідну пару флагів — на wire завжди корректний XOR.
/// </para>
/// </remarks>
[PublicAPI]
public enum RemoveContactMode
{
    /// <summary>Видалити контакт-запис (names/note/blocked), але зберегти identity-keys та sessions. Wire: hide=false, forget=false.</summary>
    DeleteContact,

    /// <summary>Сховати контакт у listings, зберегти всі дані. Wire: hide=true.</summary>
    Hide,

    /// <summary>Hard delete — identity keys, sessions, profile, contact record — все знищено. Wire: forget=true.</summary>
    Forget,
}
