namespace SpreadsheetApp.Services
{
    /// <summary>Повідомлення та запитання користувачу. Окремий інтерфейс, щоб ViewModel не залежала від сторінок MAUI.</summary>
    public interface IDialogService
    {
        Task AlertAsync(string title, string message);

        Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

        /// <summary>Вибір однієї з кількох дій; повертає текст натиснутої кнопки.</summary>
        Task<string> ChooseAsync(string title, string cancel, params string[] options);
    }
}
