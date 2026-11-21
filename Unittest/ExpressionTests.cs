using System.Numerics;
using SpreadsheetApp.Services;
using Xunit;

namespace SpreadsheetApp.Tests
{
    /// <summary>Вирази варіанта 58: + - * /, = &lt; &gt;, not, and, or, eqv, цілі числа довільної довжини.</summary>
    public class ExpressionTests
    {
        /// <summary>Обчислює вираз у клітинці A1 і повертає текст значення або "ERR: повідомлення".</summary>
        private static string Eval(string expression)
        {
            var sheet = new Spreadsheet(3, 3);
            sheet.GetCell("A1").Expression = "=" + expression;
            sheet.RecalculateAll();

            var cell = sheet.GetCell("A1");
            return cell.HasError ? "ERR: " + cell.ErrorMessage : cell.ValueText;
        }

        [Theory]
        [InlineData("5", "5")]
        [InlineData("5+5", "10")]
        [InlineData("10-2", "8")]
        [InlineData("3-5", "-2")]
        [InlineData("2+3*4", "14")]
        [InlineData("(2+3)*4", "20")]
        [InlineData("2*3+4*5", "26")]
        [InlineData("1+(2*(3+4))-5", "10")]
        [InlineData("(((1)))", "1")]
        public void Arithmetic(string expression, string expected)
        {
            Assert.Equal(expected, Eval(expression));
        }

        [Theory]
        [InlineData("7/2", "3")]
        [InlineData("10/3*3", "9")]
        [InlineData("100/7/2", "7")]
        [InlineData("0/5", "0")]
        public void IntegerDivision(string expression, string expected)
        {
            Assert.Equal(expected, Eval(expression));
        }

        [Theory]
        [InlineData("999999999999999999999+1", "1000000000000000000000")]
        [InlineData("99999999999999999999*99999999999999999999", "9999999999999999999800000000000000000001")]
        public void BigIntegers(string expression, string expected)
        {
            Assert.Equal(expected, Eval(expression));
        }

        [Fact]
        public void Engine_ReturnsBigInteger()
        {
            object result = new ExpressionEngine().Evaluate("2+3", new Spreadsheet(2, 2));
            Assert.Equal(new BigInteger(5), result);
        }

        [Theory]
        [InlineData("5>2", "true")]
        [InlineData("2<5", "true")]
        [InlineData("5<2", "false")]
        [InlineData("5=5", "true")]
        [InlineData("5=6", "false")]
        [InlineData("1+1=2", "true")]
        [InlineData("true=true", "true")]
        [InlineData("true=false", "false")]
        public void Comparison(string expression, string expected)
        {
            Assert.Equal(expected, Eval(expression));
        }

        [Theory]
        [InlineData("true", "true")]
        [InlineData("false", "false")]
        [InlineData("TRUE and FALSE", "false")]
        [InlineData("not true", "false")]
        [InlineData("not false", "true")]
        [InlineData("true and false", "false")]
        [InlineData("true or false", "true")]
        [InlineData("true eqv true", "true")]
        [InlineData("true eqv false", "false")]
        [InlineData("false eqv false", "true")]
        public void Logic(string expression, string expected)
        {
            Assert.Equal(expected, Eval(expression));
        }

        [Theory]
        [InlineData("true or false and false", "true")]      // and сильніше за or
        [InlineData("false eqv false or true", "false")]     // eqv слабше за or: false eqv (false or true)
        [InlineData("not 1<2", "false")]                     // not слабше за порівняння: not (1<2)
        [InlineData("not 5 > 3 eqv false", "true")]          // (not (5>3)) eqv false
        [InlineData("1<2 and 2<3", "true")]
        public void Precedence(string expression, string expected)
        {
            Assert.Equal(expected, Eval(expression));
        }

        [Theory]
        [InlineData("1 and true", "Очікувалось логічне значення")]
        [InlineData("true+1", "Очікувалось ціле число")]
        [InlineData("1=true", "Неможливо порівняти")]
        [InlineData("1/0", "Ділення на нуль")]
        [InlineData("7/(3-3)", "Ділення на нуль")]
        public void TypeAndRuntimeErrors(string expression, string expectedMessagePart)
        {
            string result = Eval(expression);
            Assert.StartsWith("ERR: ", result);
            Assert.Contains(expectedMessagePart, result);
        }

        [Theory]
        [InlineData("1+")]
        [InlineData("(1+2")]
        [InlineData("1+2)")]
        [InlineData("()")]
        [InlineData("1 2")]
        [InlineData("1<2<3")]
        [InlineData("not")]
        [InlineData("abc")]
        [InlineData("12a3")]
        [InlineData("-5")]   // унарного мінуса у варіанті 58 немає
        [InlineData("1 $ 2")]
        public void SyntaxErrors(string expression)
        {
            string result = Eval(expression);
            Assert.StartsWith("ERR: Синтаксична помилка", result);
        }

        [Fact]
        public void SyntaxError_ReportsPositionInCellText()
        {
            // у клітинці "=1+" помилка на 4-му символі (кінець виразу)
            Assert.Contains("(позиція 4)", Eval("1+"));
            // у клітинці "=1 $ 2" символ «$» — 4-й
            Assert.Contains("(позиція 4)", Eval("1 $ 2"));
            Assert.Contains("невідомий символ", Eval("1 $ 2"));
        }

        [Fact]
        public void EmptyFormula_IsError()
        {
            var sheet = new Spreadsheet(2, 2);
            sheet.GetCell("A1").Expression = "=";
            sheet.RecalculateAll();
            Assert.True(sheet.GetCell("A1").HasError);
        }
    }
}
