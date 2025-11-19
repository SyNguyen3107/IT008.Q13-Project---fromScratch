using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using CommunityToolkit.Mvvm.Messaging;
using IT008.Q13_Project___fromScratch.Messages;
using System.Threading.Tasks;
using System.Windows;

namespace IT008.Q13_Project___fromScratch.ViewModels
{
    public partial class DeckRenameViewModel : ObservableObject
    {
        private readonly IDeckRepository _deckRepository;
        private readonly IMessenger _messenger;
        private Deck _targetDeck;

        // Thuộc tính binding với TextBox
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
        private string _newName = string.Empty;

        // Constructor
        public DeckRenameViewModel(IDeckRepository deckRepository, IMessenger messenger)
        {
            _deckRepository = deckRepository;
            _messenger = messenger;
        }

        // Hàm này được gọi từ bên ngoài để truyền Deck cần sửa vào
        public void Initialize(Deck deck)
        {
            _targetDeck = deck;
            NewName = deck.Name; // Hiển thị tên cũ lên ô nhập để người dùng sửa
        }

        // Kiểm tra điều kiện: Tên không được để trống
        private bool CanRename()
        {
            return !string.IsNullOrWhiteSpace(NewName);
        }

        // Command cho nút OK
        [RelayCommand(CanExecute = nameof(CanRename))]
        private async Task Rename(object? param)
        {
            if (_targetDeck == null) return;

            // 1. Cập nhật tên mới vào object
            _targetDeck.Name = NewName;

            // 2. Lưu vào CSDL
            await _deckRepository.UpdateAsync(_targetDeck);

            // 3. Gửi tin nhắn báo cho MainAnkiViewModel biết để cập nhật UI
            _messenger.Send(new DeckUpdatedMessage(_targetDeck));

            // 4. Đóng cửa sổ
            if (param is Window window)
            {
                window.Close();
            }
        }

        // Command cho nút Cancel
        [RelayCommand]
        private void Cancel(object? param)
        {
            if (param is Window window)
            {
                window.Close();
            }
        }
    }
}