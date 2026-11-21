using System.Globalization;

namespace SpreadsheetApp.Services
{
    /// <summary>Адреси клітинок у стилі Excel: стовпець — літери (A, B, ..., Z, AA, ...), рядок — число з 1.</summary>
    public static class CellAddress
    {
        /// <summary>Назва стовпця за індексом, що починається з 0: 0 → A, 25 → Z, 26 → AA.</summary>
        public static string GetColumnName(int index)
        {
            string name = "";
            int n = index + 1;
            while (n > 0)
            {
                n--;
                name = (char)('A' + n % 26) + name;
                n /= 26;
            }
            return name;
        }

        public static string Make(int column, int row) => GetColumnName(column) + row.ToString(CultureInfo.InvariantCulture);

        /// <summary>Розбирає адресу (регістр не важливий). column — з 0, row — з 1.</summary>
        public static bool TryParse(string address, out int column, out int row)
        {
            column = 0;
            row = 0;
            if (string.IsNullOrEmpty(address)) return false;

            int i = 0;
            int col = 0;
            while (i < address.Length && char.IsAsciiLetter(address[i]))
            {
                col = col * 26 + (char.ToUpperInvariant(address[i]) - 'A' + 1);
                if (col > 1_000_000) return false;
                i++;
            }
            if (i == 0 || i == address.Length) return false;

            if (!int.TryParse(address.AsSpan(i), NumberStyles.None, CultureInfo.InvariantCulture, out int parsedRow) || parsedRow < 1)
                return false;

            column = col - 1;
            row = parsedRow;
            return true;
        }
    }
}
