using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text.Json;
using SpreadsheetApp.Models;
using Cell = SpreadsheetApp.Models.Cell;

namespace SpreadsheetApp.Services
{
    /// <summary>
    /// Модель таблиці: клітинки, обчислення, збереження та відкриття файлів.
    /// Не залежить від інтерфейсу користувача.
    /// </summary>
    public class Spreadsheet
    {
        public const int MaxRows = 100;
        public const int MaxColumns = 26;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly Dictionary<string, Cell> _cells = new();
        private readonly ExpressionEngine _engine = new();
        private readonly HashSet<string> _evaluating = new(); // клітинки, що обчислюються зараз (виявлення циклів)
        private readonly HashSet<string> _computed = new();   // клітинки, обчислені в поточному перерахунку
        private bool _isModified;

        public Spreadsheet(int rows, int columns)
        {
            Reset(rows, columns);
        }

        public int RowCount { get; private set; }
        public int ColumnCount { get; private set; }

        public IEnumerable<Cell> Cells => _cells.Values;

        public bool IsModified
        {
            get => _isModified;
            private set
            {
                if (_isModified == value) return;
                _isModified = value;
                ModifiedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler ModifiedChanged;

        /// <summary>Нова порожня таблиця заданого розміру.</summary>
        public void Reset(int rows, int columns)
        {
            foreach (var cell in _cells.Values)
                cell.PropertyChanged -= OnCellPropertyChanged;

            _cells.Clear();
            _computed.Clear();
            _evaluating.Clear();
            Resize(rows, columns);
            IsModified = false;
        }

        /// <summary>Змінює розмір таблиці, зберігаючи клітинки, що лишилися в межах.</summary>
        public void Resize(int rows, int columns)
        {
            if (rows < 1 || rows > MaxRows)
                throw new ArgumentOutOfRangeException(nameof(rows), $"Кількість рядків має бути від 1 до {MaxRows}");
            if (columns < 1 || columns > MaxColumns)
                throw new ArgumentOutOfRangeException(nameof(columns), $"Кількість стовпців має бути від 1 до {MaxColumns}");

            foreach (var address in _cells.Keys.ToList())
            {
                CellAddress.TryParse(address, out int col, out int row);
                if (row > rows || col >= columns)
                {
                    _cells[address].PropertyChanged -= OnCellPropertyChanged;
                    _cells.Remove(address);
                }
            }

            for (int r = 1; r <= rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    string address = CellAddress.Make(c, r);
                    if (_cells.ContainsKey(address)) continue;

                    var cell = new Cell(address);
                    cell.PropertyChanged += OnCellPropertyChanged;
                    _cells.Add(address, cell);
                }
            }

            RowCount = rows;
            ColumnCount = columns;
            IsModified = true;
        }

        /// <summary>Чи є непорожні клітинки поза межами таблиці заданого розміру.</summary>
        public bool HasDataOutside(int rows, int columns)
        {
            foreach (var cell in _cells.Values)
            {
                CellAddress.TryParse(cell.Address, out int col, out int row);
                if ((row > rows || col >= columns) && !string.IsNullOrWhiteSpace(cell.Expression))
                    return true;
            }
            return false;
        }

        /// <summary>Клітинка за адресою (регістр не важливий) або null, якщо такої немає.</summary>
        public Cell GetCell(string address)
        {
            if (!CellAddress.TryParse(address, out int col, out int row)) return null;
            return _cells.TryGetValue(CellAddress.Make(col, row), out var cell) ? cell : null;
        }

        /// <summary>Перераховує всю таблицю. Порядок клітинок не має значення: посилання обчислюються за потреби.</summary>
        public void RecalculateAll()
        {
            _computed.Clear();
            _evaluating.Clear();
            foreach (var cell in _cells.Values)
                Compute(cell);
        }

        /// <summary>
        /// Значення клітинки для посилань з виразів (BigInteger або bool). Порожня клітинка дорівнює 0.
        /// </summary>
        /// <exception cref="ExpressionException">Клітинки немає, циклічне посилання, у клітинці помилка або текст.</exception>
        public object GetValue(string address)
        {
            var cell = GetCell(address);
            string name = address.ToUpperInvariant();

            if (cell == null)
                throw new ExpressionException($"Клітинки {name} не існує");
            if (_evaluating.Contains(cell.Address))
                throw new ExpressionException($"Циклічне посилання на клітинку {cell.Address}");

            Compute(cell);

            if (cell.HasError)
                throw new ExpressionException($"Клітинка {cell.Address} містить помилку");
            if (cell.Value == null)
                return BigInteger.Zero;
            if (cell.Value is string)
                throw new ExpressionException($"Клітинка {cell.Address} містить текст, а не число чи логічне значення");

            return cell.Value;
        }

        public void SaveToFile(string path)
        {
            var model = new SheetFileModel { Rows = RowCount, Columns = ColumnCount };
            foreach (var cell in _cells.Values.OrderBy(c => RowOf(c)).ThenBy(c => ColumnOf(c)))
            {
                if (!string.IsNullOrWhiteSpace(cell.Expression))
                    model.Cells[cell.Address] = cell.Expression;
            }

            File.WriteAllText(path, JsonSerializer.Serialize(model, JsonOptions));
            IsModified = false;
        }

        /// <exception cref="InvalidDataException">Файл має неправильний формат. Таблиця при цьому не змінюється.</exception>
        public void LoadFromFile(string path)
        {
            SheetFileModel model;
            try
            {
                model = JsonSerializer.Deserialize<SheetFileModel>(File.ReadAllText(path));
            }
            catch (JsonException)
            {
                throw new InvalidDataException("Файл має неправильний формат.");
            }

            if (model == null)
                throw new InvalidDataException("Файл має неправильний формат.");
            if (model.Rows < 1 || model.Rows > MaxRows || model.Columns < 1 || model.Columns > MaxColumns)
                throw new InvalidDataException($"Розмір таблиці у файлі поза допустимими межами (до {MaxRows} рядків і {MaxColumns} стовпців).");

            var loaded = new List<(string Address, string Expression)>();
            foreach (var pair in model.Cells ?? new Dictionary<string, string>())
            {
                if (!CellAddress.TryParse(pair.Key, out int col, out int row) || row > model.Rows || col >= model.Columns)
                    throw new InvalidDataException($"У файлі є клітинка {pair.Key}, що виходить за межі таблиці.");
                loaded.Add((CellAddress.Make(col, row), pair.Value ?? ""));
            }

            Reset(model.Rows, model.Columns);
            foreach (var (address, expression) in loaded)
                _cells[address].Expression = expression;

            RecalculateAll();
            IsModified = false;
        }

        private void Compute(Cell cell)
        {
            if (_computed.Contains(cell.Address)) return;

            _evaluating.Add(cell.Address);
            try
            {
                cell.SetResult(EvaluateCell(cell));
            }
            catch (ExpressionException ex)
            {
                // +1: позиція в виразі рахується без початкового «=»
                string position = ex.Position > 0 ? $" (позиція {ex.Position + 1})" : "";
                cell.SetError(ex.Message + position);
            }
            catch (Exception ex)
            {
                cell.SetError($"Внутрішня помилка: {ex.Message}");
            }
            finally
            {
                _evaluating.Remove(cell.Address);
                _computed.Add(cell.Address);
            }
        }

        private object EvaluateCell(Cell cell)
        {
            string text = cell.Expression?.Trim() ?? "";

            if (text.Length == 0) return null;
            if (text[0] == '=') return _engine.Evaluate(text.Substring(1), this);

            if (BigInteger.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out BigInteger number))
                return number;
            if (bool.TryParse(text, out bool flag))
                return flag;

            return text; // звичайний текст
        }

        private void OnCellPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Cell.Expression))
                IsModified = true;
        }

        private static int RowOf(Cell cell)
        {
            CellAddress.TryParse(cell.Address, out _, out int row);
            return row;
        }

        private static int ColumnOf(Cell cell)
        {
            CellAddress.TryParse(cell.Address, out int column, out _);
            return column;
        }
    }
}
