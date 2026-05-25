using JetBrains.Annotations;

namespace SignalCli.Models.Signal.Message;

/// <summary>
/// Опції для відправки <b>receipt</b> (read/viewed acknowledgement) через Signal.
/// Створюється через <see cref="Builder"/>.
/// </summary>
/// <remarks>
/// signal-cli-api-coverage Wave 1 (`messaging-interactive`). Wire-shape pinned до
/// <c>src/main/java/org/asamk/signal/commands/SendReceiptCommand.java</c> @ <c>bda4e7fc</c>.
/// <para>
/// <b>§F7 quirk:</b> <see cref="Recipient"/> — <b>singular</b> string, не список. Унікально серед
/// 4-х Wave-1 send-методів (інші три приймають список). Upstream <c>argparse</c> декларує
/// recipient без <c>.nargs(...)</c>, тож canonical wire shape — single value.
/// </para>
/// <para>
/// <b>No group support.</b> Receipts ходять per-(sender,timestamp) тапл — групові receipts
/// emit'ять самі device'и одержувача при читанні. Тому ні <c>group-id</c>, ні <c>note-to-self</c>
/// у цього методу не існує.
/// </para>
/// </remarks>
[PublicAPI]
public sealed record ReceiptOptions
{
    /// <summary>Номер акаунту-відправника (E.164).</summary>
    public string Account { get; private set; } = string.Empty;

    /// <summary>§F7: singular recipient (NOT a list). Phone/UUID.</summary>
    public string Recipient { get; private set; } = string.Empty;

    /// <summary>Один або кілька send-timestamps повідомлень, що підтверджуються.</summary>
    public IReadOnlyList<long> TargetTimestamps { get; private set; } = [];

    /// <summary>Тип receipt'у (Read/Viewed). Default — <see cref="ReceiptType.Read"/>.</summary>
    public ReceiptType Type { get; private set; } = ReceiptType.Read;

    /// <summary>Будівельник <see cref="ReceiptOptions"/>.</summary>
    [PublicAPI]
    public sealed class Builder
    {
        private readonly ReceiptOptions _options;

        /// <summary>Створює будівельник з обов'язковими параметрами.</summary>
        /// <param name="account">E.164 номер акаунту-відправника.</param>
        /// <param name="recipient">Phone/UUID отримувача (singular).</param>
        /// <param name="targetTimestamps">Один або більше timestamp'ів повідомлень, що підтверджуються.</param>
        public Builder(string account, string recipient, IEnumerable<long> targetTimestamps)
        {
            ArgumentException.ThrowIfNullOrEmpty(account);
            ArgumentException.ThrowIfNullOrEmpty(recipient);
            ArgumentNullException.ThrowIfNull(targetTimestamps);
            var ts = targetTimestamps.ToList();
            if (ts.Count == 0)
                throw new ArgumentException("TargetTimestamps не може бути порожнім.", nameof(targetTimestamps));

            _options = new ReceiptOptions
            {
                Account = account,
                Recipient = recipient,
                TargetTimestamps = ts,
            };
        }

        /// <summary>Виставити тип receipt'у.</summary>
        public Builder WithType(ReceiptType type)
        {
            _options.Type = type;
            return this;
        }

        /// <summary>Будує <see cref="ReceiptOptions"/>.</summary>
        /// <exception cref="InvalidOperationException">Якщо обов'язкові поля скинуто після ctor.</exception>
        public ReceiptOptions Build()
        {
            if (string.IsNullOrEmpty(_options.Account))
                throw new InvalidOperationException("Account був скинутий після конструювання Builder.");
            if (string.IsNullOrEmpty(_options.Recipient))
                throw new InvalidOperationException("Recipient був скинутий після конструювання Builder.");
            if (_options.TargetTimestamps.Count == 0)
                throw new InvalidOperationException("TargetTimestamps було очищено після конструювання Builder.");
            return _options;
        }
    }
}
