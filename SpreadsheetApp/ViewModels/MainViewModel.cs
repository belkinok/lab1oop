using System.Collections.ObjectModel;
using System.Windows.Input;
using SpreadsheetApp.Services;
using Cell = SpreadsheetApp.Models.Cell;

namespace SpreadsheetApp.ViewModels
{
    /// <summary>
    /// ViewModel головного вікна. Містить лише логіку взаємодії (команди, режими, діалоги);
    /// обчислення, файли й структура таблиці — у Spreadsheet.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private const int DefaultRows = 10;
        private const int DefaultColumns = 8;

     
        private const string AuthorInfo = "Автор: <Роман Бєлянушкін, К-24>";

        private const string HelpText =
            "Електронна таблиця з обчисленням виразів.\n\n" +
            "• Рядок, що починається з «=», є виразом; інший текст — звичайне значення (число, true/false або текст).\n" +
            "• У виразах: цілі числа довільної довжини, true і false, посилання на клітинки (A1, B12), дужки.\n" +
            "• Операції (від найнижчого пріоритету до найвищого): eqv, or, and, not, порівняння = < >, + -, * /.\n" +
            "• Ділення цілочисельне. Порожня клітинка в посиланні дорівнює 0.\n" +
            "• «Обчислити» перераховує всю таблицю. Кнопка режиму перемикає ВИРАЗ / ЗНАЧЕННЯ.\n" +
            "• Клітинка з помилкою показує #ПОМИЛКА; текст і позицію помилки видно в рядку стану після вибору клітинки.\n" +
            "• Таблицю можна зберегти у файл і відкрити знову, а також змінити кількість рядків і стовпців.";

        private readonly Spreadsheet _spreadsheet = new(DefaultRows, DefaultColumns);
        private readonly IDialogService _dialogs;
        private readonly IFileDialogService _fileDialogs;

        private string _currentPath;
        private bool _showValues;
        private string _statusText = "Готово";

        public MainViewModel(IDialogService dialogs, IFileDialogService fileDialogs)
        {
            _dialogs = dialogs;
            _fileDialogs = fileDialogs;
            _spreadsheet.ModifiedChanged += (_, _) => OnPropertyChanged(nameof(Title));

            NewCommand = CreateCommand(NewAsync);
            OpenCommand = CreateCommand(OpenAsync);
            SaveCommand = CreateCommand(SaveAsync);
            SaveAsCommand = CreateCommand(SaveAsAsync);
            CalculateCommand = new Command(Calculate);
            ToggleModeCommand = new Command(ToggleMode);
            AddRowCommand = CreateCommand(() => ResizeAsync(1, 0));
            RemoveRowCommand = CreateCommand(() => ResizeAsync(-1, 0));
            AddColumnCommand = CreateCommand(() => ResizeAsync(0, 1));
            RemoveColumnCommand = CreateCommand(() => ResizeAsync(0, -1));
            HelpCommand = CreateCommand(() => _dialogs.AlertAsync("Довідка", HelpText));
            AboutCommand = CreateCommand(() => _dialogs.AlertAsync("Про програму",
                "Електронна таблиця\nЛабораторна робота №1, варіант 58\n" + AuthorInfo));
            ExitCommand = CreateCommand(ExitAsync);

            RebuildGrid();
        }

        public ObservableCollection<string> ColumnHeaders { get; } = new();

        public ObservableCollection<SheetRow> Rows { get; } = new();

        public ICommand NewCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand SaveAsCommand { get; }
        public ICommand CalculateCommand { get; }
        public ICommand ToggleModeCommand { get; }
        public ICommand AddRowCommand { get; }
        public ICommand RemoveRowCommand { get; }
        public ICommand AddColumnCommand { get; }
        public ICommand RemoveColumnCommand { get; }
        public ICommand HelpCommand { get; }
        public ICommand AboutCommand { get; }
        public ICommand ExitCommand { get; }

        /// <summary>true — режим ЗНАЧЕННЯ, false — режим ВИРАЗ.</summary>
        public bool ShowValues
        {
            get => _showValues;
            private set
            {
                if (!SetProperty(ref _showValues, value)) return;
                ApplyMode();
                OnPropertyChanged(nameof(ModeText));
                OnPropertyChanged(nameof(ToggleModeText));
            }
        }

        public string ModeText => ShowValues ? "Режим: ЗНАЧЕННЯ" : "Режим: ВИРАЗ";

        public string ToggleModeText => ShowValues ? "Показати вирази" : "Показати значення";

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public string Title
        {
            get
            {
                string file = _currentPath == null ? "без назви" : Path.GetFileName(_currentPath);
                return $"Електронна таблиця — {file}{(_spreadsheet.IsModified ? " *" : "")}";
            }
        }

        /// <summary>Викликається сторінкою, коли користувач ставить курсор у клітинку.</summary>
        public void SelectCell(Cell cell)
        {
            if (cell == null)
                StatusText = "Готово";
            else if (cell.HasError)
                StatusText = $"{cell.Address}: {cell.ErrorMessage}";
            else
                StatusText = $"Клітинка {cell.Address}";
        }

        private ICommand CreateCommand(Func<Task> action)
        {
            return new Command(async () =>
            {
                try
                {
                    await action();
                }
                catch (Exception ex)
                {
                    await _dialogs.AlertAsync("Помилка", ex.Message);
                }
            });
        }

        private void Calculate()
        {
            _spreadsheet.RecalculateAll();
            int errors = _spreadsheet.Cells.Count(c => c.HasError);
            StatusText = errors == 0
                ? "Обчислення завершено без помилок"
                : $"Обчислення завершено. Клітинок з помилками: {errors}";
        }

        private void ToggleMode()
        {
            if (!ShowValues) Calculate(); // у режимі ЗНАЧЕННЯ мають бути актуальні значення
            ShowValues = !ShowValues;
        }

        private void ApplyMode()
        {
            foreach (var cell in _spreadsheet.Cells)
                cell.IsValueMode = _showValues;
        }

        private async Task NewAsync()
        {
            if (!await ConfirmDiscardAsync()) return;

            _spreadsheet.Reset(DefaultRows, DefaultColumns);
            _currentPath = null;
            ShowValues = false;
            RebuildGrid();
            OnPropertyChanged(nameof(Title));
            StatusText = "Створено нову таблицю";
        }

        private async Task OpenAsync()
        {
            if (!await ConfirmDiscardAsync()) return;

            string path = await _fileDialogs.PickOpenPathAsync();
            if (path == null) return;

            try
            {
                _spreadsheet.LoadFromFile(path);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                await _dialogs.AlertAsync("Не вдалося відкрити файл", ex.Message);
                return;
            }

            _currentPath = path;
            RebuildGrid();
            OnPropertyChanged(nameof(Title));
            StatusText = $"Відкрито: {Path.GetFileName(path)}";
        }

        private async Task<bool> SaveAsync()
        {
            return _currentPath == null ? await SaveAsAsync() : await WriteAsync(_currentPath);
        }

        private async Task<bool> SaveAsAsync()
        {
            string suggested = _currentPath == null ? "table.json" : Path.GetFileName(_currentPath);
            string path = await _fileDialogs.PickSavePathAsync(suggested);
            return path != null && await WriteAsync(path);
        }

        private async Task<bool> WriteAsync(string path)
        {
            try
            {
                _spreadsheet.SaveToFile(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await _dialogs.AlertAsync("Не вдалося зберегти файл", ex.Message);
                return false;
            }

            _currentPath = path;
            OnPropertyChanged(nameof(Title));
            StatusText = $"Збережено: {Path.GetFileName(path)}";
            return true;
        }

        /// <summary>Якщо є незбережені зміни — питає, що робити. false означає «скасувати дію».</summary>
        private async Task<bool> ConfirmDiscardAsync()
        {
            if (!_spreadsheet.IsModified) return true;

            string choice = await _dialogs.ChooseAsync("Є незбережені зміни", "Скасувати", "Зберегти", "Не зберігати");
            if (choice == "Зберегти") return await SaveAsync();
            return choice == "Не зберігати";
        }

        private async Task ResizeAsync(int deltaRows, int deltaColumns)
        {
            int rows = _spreadsheet.RowCount + deltaRows;
            int columns = _spreadsheet.ColumnCount + deltaColumns;

            if (rows < 1 || columns < 1 || rows > Spreadsheet.MaxRows || columns > Spreadsheet.MaxColumns)
            {
                await _dialogs.AlertAsync("Зміна розміру",
                    $"Рядків має бути від 1 до {Spreadsheet.MaxRows}, стовпців — від 1 до {Spreadsheet.MaxColumns}.");
                return;
            }

            bool shrinking = deltaRows < 0 || deltaColumns < 0;
            if (shrinking && _spreadsheet.HasDataOutside(rows, columns))
            {
                bool confirmed = await _dialogs.ConfirmAsync("Зменшення таблиці",
                    "Дані в клітинках, що видаляються, буде втрачено. Продовжити?", "Так", "Ні");
                if (!confirmed) return;
            }

            _spreadsheet.Resize(rows, columns);
            _spreadsheet.RecalculateAll();
            RebuildGrid();
            StatusText = $"Розмір таблиці: {rows} × {columns}";
        }

        private async Task ExitAsync()
        {
            if (!await ConfirmDiscardAsync()) return;
            Application.Current?.Quit();
        }

        private void RebuildGrid()
        {
            ColumnHeaders.Clear();
            Rows.Clear();

            for (int c = 0; c < _spreadsheet.ColumnCount; c++)
                ColumnHeaders.Add(CellAddress.GetColumnName(c));

            for (int r = 1; r <= _spreadsheet.RowCount; r++)
            {
                var cells = new List<Cell>();
                for (int c = 0; c < _spreadsheet.ColumnCount; c++)
                    cells.Add(_spreadsheet.GetCell(CellAddress.Make(c, r)));
                Rows.Add(new SheetRow(r, cells));
            }

            ApplyMode();
        }
    }
}
