using CommunityToolkit.Mvvm.ComponentModel; // Cần cho ObservableObject, [ObservableProperty]
using CommunityToolkit.Mvvm.Input; // Cần cho [RelayCommand]
using CommunityToolkit.Mvvm.Messaging;
using EasyFlips.Interfaces;
using EasyFlips.Messages;
using EasyFlips.Models;
using System.Windows;

namespace EasyFlips.ViewModels
{
    // --- SỬA LỖI KẾ THỪA Ở ĐÂY ---
    // Kế thừa trực tiếp từ ObservableObject thay vì BaseViewModel
    public partial class CreateDeckViewModel : ObservableObject
    {
        private readonly IDeckRepository _deckRepository;
        private readonly IMessenger _messenger;

        // --- SỬA LỖI NOT NULL (DATABASE) ---
        // Gán giá trị mặc định là string.Empty, không phải null
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;
        // ---------------------------------

        // Constructor
        public CreateDeckViewModel(IDeckRepository deckRepository, IMessenger messenger)
        {
            _deckRepository = deckRepository;
            _messenger = messenger;
        }
        // Constructor không tham số
        public CreateDeckViewModel()
        {
            // Tự tạo messenger mặc định
            _messenger = WeakReferenceMessenger.Default;
        }

        // --- Các Command ---

        // [RelayCommand] sẽ tự động tạo một thuộc tính ICommand tên là "SaveCommand"
        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task Save(object? param) // object? param là cửa sổ được truyền vào
        {
            var newDeck = new Deck
            {
                Name = this.Name, // 'Name' bây
                Description = this.Description // 'Description' cũng sẽ được tạo
            };

            await _deckRepository.AddAsync(newDeck);

            // Gửi tin nhắn báo cho MainViewModel cập nhật
            _messenger.Send(new DeckAddedMessage(newDeck));

            // Đóng cửa sổ
            if (param is Window window)
                window.Close();
        }

        // Hàm kiểm tra điều kiện cho SaveCommand
        private bool CanSave()
        {
            // 'Name' là thuộc tính được [ObservableProperty] tự động tạo ra
            return !string.IsNullOrWhiteSpace(Name);
        }

        // [RelayCommand] sẽ tự động tạo một thuộc tính ICommand tên là "CancelCommand"
        [RelayCommand]
        private void Cancel(object? param)
        {
            if (param is Window window)
                window.Close();
        }
    }
}