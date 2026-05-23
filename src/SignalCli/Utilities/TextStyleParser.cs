namespace SignalCli.Utilities;

/// <summary>
/// Парсер для обробки стилізації тексту в Signal.
/// Підтримує розбір маркувань тексту (жирний, курсив, моноширинний, закреслений, спойлер)
/// та створення відповідних інструкцій форматування для API Signal.
/// </summary>
internal class TextStyleParser
{
    private readonly string _input;
    private int _pos;
    private readonly System.Text.StringBuilder _output;
    private readonly List<string> _formatStrings;
    private readonly Stack<TokenState> _tokens;
    private const char EscapeChar = '\\';

    /// <summary>
    /// Створює новий екземпляр парсера для обробки стилізованого тексту.
    /// </summary>
    /// <param name="input">Вхідний текст із маркуванням стилів (наприклад, *курсив*, **жирний**, `код`).</param>
    public TextStyleParser(string input)
    {
        _input = input;
        _pos = 0;
        _output = new System.Text.StringBuilder();
        _formatStrings = new List<string>();
        _tokens = new Stack<TokenState>();
    }

    /// <summary>
    /// Розбирає вхідний текст та створює інструкції форматування.
    /// </summary>
    /// <returns>
    /// Кортеж, що містить очищений текст (без маркерів стилів) та список інструкцій форматування
    /// у форматі "start:length:STYLE".
    /// </returns>
    public (string ParsedText, List<string> FormatStrings) Parse()
    {
        while (_pos < _input.Length)
        {
            char c = _input[_pos];
            char next = _pos + 1 < _input.Length ? _input[_pos + 1] : '\0';

            // Екранування: \\, \*, \`, \|, \~ → виводимо літеральний символ.
            // Обробляємо тут єдиним блоком, тож маркери, що йдуть за escape,
            // ніколи не трактуються як форматування.
            if (c == EscapeChar &&
                (next == '*' || next == '`' || next == '|' || next == '~' || next == EscapeChar))
            {
                _output.Append(next);
                _pos += 2;
                continue;
            }

            if (c == '*')
            {
                if (next == '*')
                {
                    HandleToken(TokenType.Bold);
                    _pos += 2;
                }
                else
                {
                    HandleToken(TokenType.Italic);
                    _pos++;
                }
                continue;
            }

            if (c == '|' && next == '|')
            {
                HandleToken(TokenType.Spoiler);
                _pos += 2;
                continue;
            }

            if (c == '~')
            {
                HandleToken(TokenType.Strikethrough);
                _pos++;
                continue;
            }

            if (c == '`')
            {
                HandleToken(TokenType.Monospace);
                _pos++;
                continue;
            }

            _output.Append(c);
            _pos++;
        }

        // F18: маркери, що залишилися на стеку незакритими, повертаємо в текст як літерали,
        // щоб користувач бачив свій ввід без мовчазного «з'їдання» символу. Обробляємо у
        // порядку спадання BeginPos — інакше попередні вставки зсувають пізніші позиції.
        if (_tokens.Count > 0)
        {
            var unclosed = _tokens.ToArray(); // Stack.ToArray() — від top до bottom (LIFO).
            // Сортуємо за BeginPos спаданням — спершу найправіша, потім лівіша.
            Array.Sort(unclosed, (a, b) => b.BeginPos.CompareTo(a.BeginPos));
            foreach (var state in unclosed)
            {
                _output.Insert(state.BeginPos, MarkerForToken(state.Token));
            }
        }

        return (_output.ToString(), _formatStrings);
    }

    /// <summary>
    /// Літеральний маркер, який відповідає типу токена (для повернення в текст
    /// при незакритому маркері — F18).
    /// </summary>
    private static string MarkerForToken(TokenType token) => token switch
    {
        TokenType.Italic => "*",
        TokenType.Bold => "**",
        TokenType.Monospace => "`",
        TokenType.Strikethrough => "~",
        TokenType.Spoiler => "||",
        _ => string.Empty
    };

    /// <summary>
    /// Обробляє маркер стилю, відкриваючи або закриваючи форматування.
    /// </summary>
    /// <param name="tokenType">Тип маркера стилю.</param>
    private void HandleToken(TokenType tokenType)
    {
        int currentPos = _output.Length;
        if (_tokens.Count > 0 && _tokens.Peek().Token == tokenType)
        {
            var startState = _tokens.Pop();
            int length = currentPos - startState.BeginPos;
            _formatStrings.Add($"{startState.BeginPos}:{length}:{tokenType.ToString().ToUpperInvariant()}");
        }
        else
        {
            _tokens.Push(new TokenState { BeginPos = currentPos, Token = tokenType });
        }
    }

    /// <summary>
    /// Клас для зберігання стану маркера стилю.
    /// </summary>
    private class TokenState
    {
        /// <summary>
        /// Позиція початку дії стилю в тексті.
        /// </summary>
        public int BeginPos { get; set; }
        
        /// <summary>
        /// Тип стилю.
        /// </summary>
        public TokenType Token { get; set; }
    }

    /// <summary>
    /// Перелік типів стилів тексту.
    /// </summary>
    private enum TokenType
    {
        /// <summary>
        /// Курсив (*текст*).
        /// </summary>
        Italic,
        
        /// <summary>
        /// Жирний шрифт (**текст**).
        /// </summary>
        Bold,
        
        /// <summary>
        /// Моноширинний шрифт (`текст`).
        /// </summary>
        Monospace,
        
        /// <summary>
        /// Закреслений текст (~текст~).
        /// </summary>
        Strikethrough,
        
        /// <summary>
        /// Спойлер (||текст||).
        /// </summary>
        Spoiler
    }
}