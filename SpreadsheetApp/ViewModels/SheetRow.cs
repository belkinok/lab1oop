using Cell = SpreadsheetApp.Models.Cell;

namespace SpreadsheetApp.ViewModels
{
    /// <summary>Один рядок таблиці для відображення: номер і клітинки.</summary>
    public class SheetRow
    {
        public SheetRow(int number, IReadOnlyList<Cell> cells)
        {
            Number = number;
            Cells = cells;
        }

        public int Number { get; }

        public IReadOnlyList<Cell> Cells { get; }
    }
}
