using SpreadsheetApp.Services;
using Xunit;

namespace SpreadsheetApp.Tests
{
    public class SpreadsheetTests
    {
        private static Spreadsheet Sheet(params (string Address, string Expression)[] cells)
        {
            var sheet = new Spreadsheet(5, 5);
            foreach (var (address, expression) in cells)
                sheet.GetCell(address).Expression = expression;
            sheet.RecalculateAll();
            return sheet;
        }

        [Fact]
        public void References_ArePassedBetweenCells()
        {
            var sheet = Sheet(("A1", "5"), ("B1", "=A1*2"), ("C1", "=B1+A1"));
            Assert.Equal("10", sheet.GetCell("B1").ValueText);
            Assert.Equal("15", sheet.GetCell("C1").ValueText);
        }

        [Fact]
        public void References_DoNotDependOnCellOrder()
        {
            // A1 залежить від B1, B1 від C1, а C1 задано пізніше за всі
            var sheet = Sheet(("A1", "=B1+1"), ("B1", "=C1+1"), ("C1", "5"));
            Assert.Equal("7", sheet.GetCell("A1").ValueText);
            Assert.Equal("6", sheet.GetCell("B1").ValueText);
        }

        [Fact]
        public void References_AreCaseInsensitive()
        {
            var sheet = Sheet(("A1", "4"), ("B1", "=a1+1"));
            Assert.Equal("5", sheet.GetCell("B1").ValueText);
        }

        [Fact]
        public void References_ToLogicalCells()
        {
            var sheet = Sheet(("A1", "=3>2"), ("B1", "=not A1"), ("C1", "=A1 or B1"), ("D1", "true"), ("E1", "=D1 and A1"));
            Assert.Equal("true", sheet.GetCell("A1").ValueText);
            Assert.Equal("false", sheet.GetCell("B1").ValueText);
            Assert.Equal("true", sheet.GetCell("C1").ValueText);
            Assert.Equal("true", sheet.GetCell("E1").ValueText);
        }

        [Fact]
        public void EmptyCell_InReference_IsZero()
        {
            var sheet = Sheet(("A1", "=D4+1"));
            Assert.Equal("1", sheet.GetCell("A1").ValueText);
        }

        [Fact]
        public void TextCell_InReference_IsError()
        {
            var sheet = Sheet(("A1", "привіт"), ("B1", "=A1+1"));
            Assert.Equal("привіт", sheet.GetCell("A1").ValueText);
            Assert.True(sheet.GetCell("B1").HasError);
            Assert.Contains("текст", sheet.GetCell("B1").ErrorMessage);
        }

        [Fact]
        public void UnknownCell_IsError()
        {
            var sheet = Sheet(("A1", "=Z99+1"));
            Assert.True(sheet.GetCell("A1").HasError);
            Assert.Contains("не існує", sheet.GetCell("A1").ErrorMessage);
        }

        [Fact]
        public void Cycle_MarksAllCellsOfTheCycle()
        {
            var sheet = Sheet(("A1", "=B1"), ("B1", "=A1"), ("C1", "=A1+1"));
            Assert.True(sheet.GetCell("A1").HasError);
            Assert.True(sheet.GetCell("B1").HasError);
            Assert.True(sheet.GetCell("C1").HasError);
            Assert.Contains("Циклічне", sheet.GetCell("A1").ErrorMessage + sheet.GetCell("B1").ErrorMessage);
        }

        [Fact]
        public void SelfReference_IsError()
        {
            var sheet = Sheet(("A1", "=A1+1"));
            Assert.True(sheet.GetCell("A1").HasError);
        }

        [Fact]
        public void ErrorInReferencedCell_Propagates()
        {
            var sheet = Sheet(("A1", "=1/0"), ("B1", "=A1+1"));
            Assert.True(sheet.GetCell("A1").HasError);
            Assert.True(sheet.GetCell("B1").HasError);
            Assert.Contains("A1", sheet.GetCell("B1").ErrorMessage);
        }

        [Fact]
        public void OneBrokenCell_DoesNotBreakOthers()
        {
            var sheet = Sheet(("A1", "=1+"), ("B1", "=2+2"));
            Assert.True(sheet.GetCell("A1").HasError);
            Assert.Equal("4", sheet.GetCell("B1").ValueText);
        }

        [Fact]
        public void Recalculate_UsesCurrentExpressions()
        {
            var sheet = Sheet(("A1", "1"), ("B1", "=A1+1"));
            sheet.GetCell("A1").Expression = "10";
            sheet.RecalculateAll();
            Assert.Equal("11", sheet.GetCell("B1").ValueText);
        }

        [Fact]
        public void Modes_ShowExpressionOrValue()
        {
            var sheet = Sheet(("A1", "=1+2"));
            var cell = sheet.GetCell("A1");

            cell.IsValueMode = false;
            Assert.Equal("=1+2", cell.DisplayText);

            cell.IsValueMode = true;
            Assert.Equal("3", cell.DisplayText);

            cell.DisplayText = "=100";             // у режимі ЗНАЧЕННЯ редагування ігнорується
            Assert.Equal("=1+2", cell.Expression);
        }

        [Fact]
        public void ErrorCell_ShowsErrorMarkerInValueMode()
        {
            var sheet = Sheet(("A1", "=1/0"));
            var cell = sheet.GetCell("A1");
            cell.IsValueMode = true;
            Assert.Equal("#ПОМИЛКА", cell.DisplayText);
        }

        [Fact]
        public void Resize_KeepsCellsInsideAndRemovesOutside()
        {
            var sheet = Sheet(("A1", "1"), ("E5", "5"));

            sheet.Resize(3, 3);
            Assert.Equal(3, sheet.RowCount);
            Assert.Equal(3, sheet.ColumnCount);
            Assert.Equal("1", sheet.GetCell("A1").Expression);
            Assert.Null(sheet.GetCell("E5"));

            sheet.Resize(6, 6);
            Assert.NotNull(sheet.GetCell("F6"));
            Assert.Equal("", sheet.GetCell("E5")?.Expression);
        }

        [Fact]
        public void Resize_ReferenceToRemovedCell_BecomesError()
        {
            var sheet = Sheet(("A1", "=E5+1"));
            sheet.Resize(3, 3);
            sheet.RecalculateAll();
            Assert.True(sheet.GetCell("A1").HasError);
        }

        [Fact]
        public void HasDataOutside_DetectsLostData()
        {
            var sheet = Sheet(("E5", "1"));
            Assert.True(sheet.HasDataOutside(4, 5));
            Assert.False(sheet.HasDataOutside(5, 5));
        }

        [Theory]
        [InlineData(0, 5)]
        [InlineData(5, 0)]
        [InlineData(Spreadsheet.MaxRows + 1, 5)]
        [InlineData(5, Spreadsheet.MaxColumns + 1)]
        public void Resize_InvalidSize_Throws(int rows, int columns)
        {
            var sheet = new Spreadsheet(5, 5);
            Assert.Throws<ArgumentOutOfRangeException>(() => sheet.Resize(rows, columns));
        }

        [Fact]
        public void IsModified_TracksEditsAndSaving()
        {
            var sheet = new Spreadsheet(3, 3);
            Assert.False(sheet.IsModified);

            sheet.GetCell("A1").Expression = "1";
            Assert.True(sheet.IsModified);

            string path = Path.GetTempFileName();
            try
            {
                sheet.SaveToFile(path);
                Assert.False(sheet.IsModified);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void SaveAndLoad_RoundTrip()
        {
            var original = Sheet(("A1", "10"), ("B1", "=A1*A1"), ("C2", "=B1>50 and true"), ("D3", "привіт"));
            original.Resize(7, 4);

            string path = Path.GetTempFileName();
            try
            {
                original.SaveToFile(path);

                var loaded = new Spreadsheet(2, 2);
                loaded.LoadFromFile(path);

                Assert.Equal(7, loaded.RowCount);
                Assert.Equal(4, loaded.ColumnCount);
                Assert.Equal("=A1*A1", loaded.GetCell("B1").Expression);
                Assert.Equal("100", loaded.GetCell("B1").ValueText);   // після відкриття таблицю обчислено
                Assert.Equal("true", loaded.GetCell("C2").ValueText);
                Assert.Equal("привіт", loaded.GetCell("D3").Expression);
                Assert.False(loaded.IsModified);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Load_InvalidFile_ThrowsAndKeepsSheet()
        {
            var sheet = Sheet(("A1", "42"));
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "це не JSON");
                Assert.Throws<InvalidDataException>(() => sheet.LoadFromFile(path));

                File.WriteAllText(path, "{\"Rows\":2,\"Columns\":2,\"Cells\":{\"Z9\":\"1\"}}");
                Assert.Throws<InvalidDataException>(() => sheet.LoadFromFile(path));

                File.WriteAllText(path, "{\"Rows\":0,\"Columns\":2,\"Cells\":{}}");
                Assert.Throws<InvalidDataException>(() => sheet.LoadFromFile(path));

                Assert.Equal("42", sheet.GetCell("A1").Expression);   // стара таблиця не зіпсована
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Load_MissingFile_ThrowsIOException()
        {
            var sheet = new Spreadsheet(2, 2);
            Assert.ThrowsAny<IOException>(() => sheet.LoadFromFile(Path.Combine(Path.GetTempPath(), "no-such-file.json")));
        }
    }

    public class CellAddressTests
    {
        [Theory]
        [InlineData(0, "A")]
        [InlineData(1, "B")]
        [InlineData(25, "Z")]
        [InlineData(26, "AA")]
        [InlineData(27, "AB")]
        [InlineData(51, "AZ")]
        [InlineData(52, "BA")]
        [InlineData(701, "ZZ")]
        [InlineData(702, "AAA")]
        public void GetColumnName(int index, string expected)
        {
            Assert.Equal(expected, CellAddress.GetColumnName(index));
        }

        [Theory]
        [InlineData("A1", 0, 1)]
        [InlineData("b12", 1, 12)]
        [InlineData("Z100", 25, 100)]
        [InlineData("AA5", 26, 5)]
        public void TryParse_Valid(string address, int column, int row)
        {
            Assert.True(CellAddress.TryParse(address, out int c, out int r));
            Assert.Equal(column, c);
            Assert.Equal(row, r);
        }

        [Theory]
        [InlineData("")]
        [InlineData("A")]
        [InlineData("1")]
        [InlineData("1A")]
        [InlineData("A0")]
        [InlineData("A-1")]
        [InlineData("A1B")]
        public void TryParse_Invalid(string address)
        {
            Assert.False(CellAddress.TryParse(address, out _, out _));
        }
    }
}
