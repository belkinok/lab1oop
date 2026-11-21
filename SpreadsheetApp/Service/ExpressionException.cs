namespace SpreadsheetApp.Services
{
    /// <summary>
    /// Помилка в виразі: синтаксична, типів, ділення на нуль, некоректне посилання тощо.
    /// Position — позиція (з 1) у виразі, де сталася помилка; 0 — позиція невідома.
    /// </summary>
    public class ExpressionException : Exception
    {
        public int Position { get; }

        public ExpressionException(string message, int position = 0) : base(message)
        {
            Position = position;
        }
    }
}
