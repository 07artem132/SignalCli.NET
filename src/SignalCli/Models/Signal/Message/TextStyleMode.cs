namespace SignalCli.Models.Signal.Message;

/// <summary>
/// Режим обробки стилізації тексту у вихідному повідомленні Signal.
/// </summary>
/// <remarks>
/// A.10: внутрішній enum замість stringly-typed <c>"styled"</c>-прапорця в
/// <c>SignalMessage.SendUnifiedMessageAsync</c>. Зовнішній API лишається <see cref="bool"/>
/// (<c>*Options.UseStyle</c>); enum використовується лише як internal-перехідник.
/// </remarks>
internal enum TextStyleMode
{
    /// <summary>Текст передається як є, без markdown-подібної стилізації.</summary>
    None = 0,

    /// <summary>Текст обробляється через <c>TextStyleParser</c> (курсив/жирний/код тощо).</summary>
    Styled = 1,
}
