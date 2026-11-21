using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SpreadsheetApp.Models
{
    /// <summary>
    /// Клітинка таблиці: текст, який ввів користувач (Expression), та результат його обчислення (Value / ErrorMessage).
    /// </summary>
    public class Cell : INotifyPropertyChanged
    {
        public const string ErrorText = "#ПОМИЛКА";

        private string _expression = "";
        private object _value;
        private string _errorMessage;
        private bool _isValueMode;

        public Cell(string address)
        {
            Address = address;
        }

        public string Address { get; }

        /// <summary>Те, що ввів користувач. Рядок, який починається з «=», є виразом.</summary>
        public string Expression
        {
            get => _expression;
            set
            {
                value ??= "";
                if (_expression == value) return;
                _expression = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        /// <summary>Результат обчислення: BigInteger, bool, рядок (звичайний текст) або null (порожня клітинка).</summary>
        public object Value => _value;

        public string ErrorMessage => _errorMessage;

        public bool HasError => !string.IsNullOrEmpty(_errorMessage);

        /// <summary>true — таблиця в режимі ЗНАЧЕННЯ, false — у режимі ВИРАЗ.</summary>
        public bool IsValueMode
        {
            get => _isValueMode;
            set
            {
                if (_isValueMode == value) return;
                _isValueMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        public string ValueText
        {
            get
            {
                if (HasError) return ErrorText;
                return _value switch
                {
                    null => "",
                    bool b => b ? "true" : "false",
                    BigInteger i => i.ToString(CultureInfo.InvariantCulture),
                    _ => _value.ToString()
                };
            }
        }

        /// <summary>Текст, який показує клітинка: вираз або значення, залежно від режиму.</summary>
        public string DisplayText
        {
            get => IsValueMode ? ValueText : Expression;
            set
            {
                if (!IsValueMode) Expression = value;
            }
        }

        public void SetResult(object value)
        {
            _value = value;
            _errorMessage = null;
            NotifyResultChanged();
        }

        public void SetError(string message)
        {
            _value = null;
            _errorMessage = message;
            NotifyResultChanged();
        }

        private void NotifyResultChanged()
        {
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(ErrorMessage));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ValueText));
            OnPropertyChanged(nameof(DisplayText));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
