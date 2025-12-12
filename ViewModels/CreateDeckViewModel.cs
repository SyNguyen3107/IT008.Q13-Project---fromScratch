using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EasyFlips.Interfaces;
using EasyFlips.Messages;
using EasyFlips.Models;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace EasyFlips.ViewModels
{
    public partial class CreateDeckViewModel : ObservableObject
    {
        private readonly IDeckRepository _deckRepository;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        // Constructor Injection
        public CreateDeckViewModel(IDeckRepository deckRepository, IMessenger messenger)
        {
            _deckRepository = deckRepository;
            _messenger = messenger;
        }

        // Constructor mặc định (nếu cần thiết cho Design time, nhưng nên hạn chế dùng)
        public CreateDeckViewModel()
        {
            _messenger = WeakReferenceMessenger.Default;
        }

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(Name);
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task Save(object? param)
        {
            try
            {
                // Tạo Deck mới (ID sẽ được tự sinh trong Constructor của Deck hoặc Repository)
                var newDeck = new Deck
                {
                    Name = this.Name.Trim(),
                    Description = this.Description?.Trim(),
                    // UserId sẽ được DeckRepository tự động gán từ AuthService
                };

                await _deckRepository.AddAsync(newDeck);

                // Gửi tin nhắn để MainViewModel cập nhật lại danh sách
                _messenger.Send(new DeckAddedMessage(newDeck));

                // Đóng cửa sổ
                if (param is Window window)
                {
                    window.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating deck: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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