namespace SpreadsheetApp.Services
{
    /// <summary>Формат файлу таблиці (JSON): розмір і вирази непорожніх клітинок.</summary>
    internal sealed class SheetFileModel
    {
        public int Rows { get; set; }
        public int Columns { get; set; }
        public Dictionary<string, string> Cells { get; set; } = new();
    }
}
