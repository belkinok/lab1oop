namespace SpreadsheetApp.Services
{
    /// <summary>Вибір файлу для відкриття та збереження. Повертає null, якщо користувач скасував вибір.</summary>
    public interface IFileDialogService
    {
        Task<string> PickOpenPathAsync();

        Task<string> PickSavePathAsync(string suggestedFileName);
    }
}
