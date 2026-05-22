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
        char prevChar = '\0';
        while (_pos < _input.Length)
        {
            char c = _input[_pos];
            char next = _pos + 1 < _input.Length ? _input[_pos + 1] : '\0';
            bool consumed = false;

            if (c == '*')
            {
                if (next == '*')
                {
                    _pos += 2;
                    if (prevChar == EscapeChar)
                    {
                        _output.Append("**");
                    }
                    else
                    {
                        HandleToken(TokenType.Bold);
                    }
                    consumed = true;
                }
                else
                {
                    _pos++;
                    if (prevChar == EscapeChar)
                    {
                        _output.Append("*");
                    }
                    else
                    {
                        HandleToken(TokenType.Italic);
                    }
                    consumed = true;
                }
            }
            else if (c == '|' && next == '|')
            {
                _pos += 2;
                if (prevChar == EscapeChar)
                {
                    _output.Append("||");
                }
                else
                {
                    HandleToken(TokenType.Spoiler);
                }
                consumed = true;
            }
            else if (c == '~')
            {
                _pos++;
                if (prevChar == EscapeChar)
                {
                    _output.Append("~");
                }
                else
                {
                    HandleToken(TokenType.Strikethrough);
                }
                consumed = true;
            }
            else if (c == '`')
            {
                _pos++;
                if (prevChar == EscapeChar)
                {
                    _output.Append("`");
                }
                else
                {
                    HandleToken(TokenType.Monospace);
                }
                consumed = true;
            }
            else if (c == EscapeChar && (next == '*' || next == '`' || next == '|' || next == '~'))
            {
                _pos += 2;
                _output.Append(next);
                consumed = true;
            }

            if (!consumed)
            {
                _output.Append(c);
                _pos++;
            }
            prevChar = c;
        }
        return (_output.ToString(), _formatStrings);
    }

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
            _formatStrings.Add($"{startState.BeginPos}:{length}:{tokenType.ToString().ToUpper()}");
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