using System.Windows.Input; // chứa ICommand

namespace EasyFlips.ViewModels
{
    // Một class trung gian để bind các nút trong XAML với hàm trong ViewModel
    class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute; // hàm chạy khi nhấn nút
        private readonly Func<object?, bool>? _canExecute; // điều kiện (nút được nhấn hay vô hiệu)

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);

        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
