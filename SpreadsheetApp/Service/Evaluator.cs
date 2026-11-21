using Antlr4.Runtime;
using SpreadsheetApp.Service;
using System.Numerics;

namespace SpreadsheetApp.Services
{
    /// <summary>
    /// Обчислює дерево розбору. Типи значень: BigInteger (цілі числа довільної довжини) і bool.
    /// Типізація сувора: арифметика й &lt; &gt; — лише для цілих, and/or/not/eqv — лише для логічних,
    /// «=» порівнює два значення одного типу.
    /// </summary>
    public class Evaluator : LabCalculatorBaseVisitor<object>
    {
        private readonly Spreadsheet _sheet;

        public Evaluator(Spreadsheet sheet)
        {
            _sheet = sheet;
        }

        public override object VisitCompileUnit(LabCalculatorParser.CompileUnitContext context)
        {
            return Visit(context.expression());
        }

        public override object VisitExpression(LabCalculatorParser.ExpressionContext context)
        {
            return Visit(context.logicEqv());
        }

        public override object VisitLogicEqv(LabCalculatorParser.LogicEqvContext context)
        {
            object first = Visit(context.logicOr(0));
            int count = context.logicOr().Length;
            if (count == 1) return first;

            bool result = AsBool(first, context.logicOr(0));
            for (int i = 1; i < count; i++)
            {
                bool operand = AsBool(Visit(context.logicOr(i)), context.logicOr(i));
                result = result == operand;
            }
            return result;
        }

        public override object VisitLogicOr(LabCalculatorParser.LogicOrContext context)
        {
            object first = Visit(context.logicAnd(0));
            int count = context.logicAnd().Length;
            if (count == 1) return first;

            bool result = AsBool(first, context.logicAnd(0));
            for (int i = 1; i < count; i++)
            {
                bool operand = AsBool(Visit(context.logicAnd(i)), context.logicAnd(i));
                result = result || operand;
            }
            return result;
        }

        public override object VisitLogicAnd(LabCalculatorParser.LogicAndContext context)
        {
            object first = Visit(context.logicNot(0));
            int count = context.logicNot().Length;
            if (count == 1) return first;

            bool result = AsBool(first, context.logicNot(0));
            for (int i = 1; i < count; i++)
            {
                bool operand = AsBool(Visit(context.logicNot(i)), context.logicNot(i));
                result = result && operand;
            }
            return result;
        }

        public override object VisitLogicNot(LabCalculatorParser.LogicNotContext context)
        {
            if (context.NOT() != null)
                return !AsBool(Visit(context.logicNot()), context.logicNot());

            return Visit(context.comparison());
        }

        public override object VisitComparison(LabCalculatorParser.ComparisonContext context)
        {
            object left = Visit(context.additive(0));
            if (context.additive().Length == 1) return left;

            var rightContext = context.additive(1);
            object right = Visit(rightContext);
            string op = context.GetChild(1).GetText();

            if (op == "=")
            {
                if (left is BigInteger li && right is BigInteger ri) return li == ri;
                if (left is bool lb && right is bool rb) return lb == rb;
                throw new ExpressionException("Неможливо порівняти ціле число з логічним значенням", Pos(rightContext));
            }

            BigInteger l = AsInteger(left, context.additive(0));
            BigInteger r = AsInteger(right, rightContext);
            return op == "<" ? l < r : l > r;
        }

        public override object VisitAdditive(LabCalculatorParser.AdditiveContext context)
        {
            object first = Visit(context.multiplicative(0));
            int count = context.multiplicative().Length;
            if (count == 1) return first;

            BigInteger result = AsInteger(first, context.multiplicative(0));
            for (int i = 1; i < count; i++)
            {
                BigInteger operand = AsInteger(Visit(context.multiplicative(i)), context.multiplicative(i));
                string op = context.GetChild(2 * i - 1).GetText();
                result = op == "+" ? result + operand : result - operand;
            }
            return result;
        }

        public override object VisitMultiplicative(LabCalculatorParser.MultiplicativeContext context)
        {
            object first = Visit(context.atom(0));
            int count = context.atom().Length;
            if (count == 1) return first;

            BigInteger result = AsInteger(first, context.atom(0));
            for (int i = 1; i < count; i++)
            {
                BigInteger operand = AsInteger(Visit(context.atom(i)), context.atom(i));
                string op = context.GetChild(2 * i - 1).GetText();
                if (op == "*")
                {
                    result *= operand;
                }
                else
                {
                    if (operand.IsZero)
                        throw new ExpressionException("Ділення на нуль", Pos(context.atom(i)));
                    result = BigInteger.Divide(result, operand); // цілочисельне ділення (до нуля)
                }
            }
            return result;
        }

        public override object VisitAtom(LabCalculatorParser.AtomContext context)
        {
            if (context.NUMBER() != null)
                return BigInteger.Parse(context.NUMBER().GetText());

            if (context.IDENTIFIER() != null)
            {
                string address = context.IDENTIFIER().GetText().ToUpperInvariant();
                try
                {
                    return _sheet.GetValue(address);
                }
                catch (ExpressionException ex) when (ex.Position == 0)
                {
                    // помилка з таблиці (немає клітинки, цикл, помилка в клітинці) — прив'язуємо до місця посилання
                    throw new ExpressionException(ex.Message, Pos(context));
                }
            }

            if (context.TRUE() != null) return true;
            if (context.FALSE() != null) return false;

            return Visit(context.expression()); // ( вираз )
        }

        private static int Pos(ParserRuleContext context) => context.Start.StartIndex + 1;

        private static bool AsBool(object value, ParserRuleContext context)
        {
            if (value is bool b) return b;
            throw new ExpressionException("Очікувалось логічне значення, а отримано ціле число", Pos(context));
        }

        private static BigInteger AsInteger(object value, ParserRuleContext context)
        {
            if (value is BigInteger i) return i;
            throw new ExpressionException("Очікувалось ціле число, а отримано логічне значення", Pos(context));
        }
    }
}
