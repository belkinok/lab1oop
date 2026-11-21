using SpreadsheetApp.ViewModels;
using Cell = SpreadsheetApp.Models.Cell;

namespace SpreadsheetApp
{
    public partial class MainPage : ContentPage
    {
        private readonly MainViewModel _viewModel;

        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
        }

        // Лише передає ViewModel, яку клітинку вибрано; жодної логіки тут немає.
        private void OnCellFocused(object sender, FocusEventArgs e)
        {
            if (sender is Entry { BindingContext: Cell cell })
                _viewModel.SelectCell(cell);
        }
    }
}
