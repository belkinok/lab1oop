namespace SpreadsheetApp.Services
{
    public class DialogService : IDialogService
    {
        private static Page CurrentPage => Application.Current?.Windows.FirstOrDefault()?.Page;

        public Task AlertAsync(string title, string message)
        {
            return CurrentPage?.DisplayAlert(title, message, "OK") ?? Task.CompletedTask;
        }

        public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
        {
            return CurrentPage?.DisplayAlert(title, message, accept, cancel) ?? Task.FromResult(false);
        }

        public Task<string> ChooseAsync(string title, string cancel, params string[] options)
        {
            return CurrentPage?.DisplayActionSheet(title, cancel, null, options) ?? Task.FromResult(cancel);
        }
    }
}
