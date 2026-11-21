using Antlr4.Runtime;
using SpreadsheetApp.Service;

namespace SpreadsheetApp.Services
{
    /// <summary>
    /// Перетворює помилки парсера ANTLR на ExpressionException з українським текстом і позицією.
    /// Нерозпізнані символи лексер віддає як токен UNKNOWN, тому вони теж потрапляють сюди.
    /// </summary>
    public class ThrowExceptionErrorListener : BaseErrorListener
    {
        public override void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            string text = offendingSymbol?.Text;
            string what;

            if (offendingSymbol == null || text == "<EOF>")
                what = "несподіваний кінець виразу";
            else if (offendingSymbol.Type == LabCalculatorLexer.UNKNOWN)
                what = $"невідомий символ або ім'я «{text}»";
            else
                what = $"несподіваний фрагмент «{text}»";

            throw new ExpressionException($"Синтаксична помилка: {what}", charPositionInLine + 1);
        }
    }
}
