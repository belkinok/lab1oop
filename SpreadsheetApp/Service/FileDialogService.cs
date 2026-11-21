namespace SpreadsheetApp.Services
{
    public class FileDialogService : IFileDialogService
    {
        public async Task<string> PickOpenPathAsync()
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Відкрити таблицю" });
            return result?.FullPath;
        }

        public async Task<string> PickSavePathAsync(string suggestedFileName)
        {
#if WINDOWS
            var picker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedFileName)
            };
            picker.FileTypeChoices.Add("Таблиця (JSON)", new List<string> { ".json" });

            if (Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView is not Microsoft.Maui.MauiWinUIWindow window)
                return null;

            WinRT.Interop.InitializeWithWindow.Initialize(picker, window.WindowHandle);
            var file = await picker.PickSaveFileAsync();
            return file?.Path;
#else
            // На інших платформах немає системного діалогу збереження: питаємо ім'я й пишемо у теку застосунку.
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page == null) return null;

            string name = await page.DisplayPromptAsync("Зберегти", "Ім'я файлу:", accept: "Зберегти", cancel: "Скасувати",
                initialValue: Path.GetFileNameWithoutExtension(suggestedFileName));
            if (string.IsNullOrWhiteSpace(name)) return null;

            return Path.Combine(FileSystem.AppDataDirectory, name.Trim() + ".json");
#endif
        }
    }
}
