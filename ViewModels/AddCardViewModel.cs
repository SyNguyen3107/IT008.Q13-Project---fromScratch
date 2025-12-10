using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EasyFlips.Helpers;
using EasyFlips.Interfaces;
using EasyFlips.Messages;
using EasyFlips.Models;
using EasyFlips.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace EasyFlips.ViewModels
{
    public partial class AddCardViewModel : ObservableObject
    {
        private readonly ICardRepository _cardRepository;
        private readonly IDeckRepository _deckRepository;
        private readonly IMessenger _messenger;

        public ObservableCollection<Deck> AllDecks { get; } = new ObservableCollection<Deck>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCardCommand))]
        private Deck _selectedDeck;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCardCommand))]
        private string _frontText;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCardCommand))]
        private string _backText;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCardCommand))]
        private string _answer = string.Empty;

        // Media properties
        [ObservableProperty] private string _frontImagePath;
        [ObservableProperty] private string _frontAudioPath;
        [ObservableProperty] private string _backImagePath;
        [ObservableProperty] private string _backAudioPath;

        // Display names
        [ObservableProperty] private string _frontImageName;
        [ObservableProperty] private string _backImageName;
        [ObservableProperty] private string _frontAudioName;
        [ObservableProperty] private string _backAudioName;

        public AddCardViewModel(ICardRepository cardRepository, IDeckRepository deckRepository, IMessenger messenger)
        {
            _cardRepository = cardRepository;
            _deckRepository = deckRepository;
            _messenger = messenger;

            // [QUAN TRỌNG]: Tự động tải danh sách Deck khi khởi tạo ViewModel
            _ = LoadDecksAsync();
        }

        public async Task LoadDecksAsync()
        {
            try
            {
                var decks = await _deckRepository.GetAllAsync();
                AllDecks.Clear();
                foreach (var deck in decks)
                {
                    AllDecks.Add(deck);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading decks: {ex.Message}");
            }
        }

        private bool CanSaveCard()
        {
            // Kiểm tra tối thiểu: Phải chọn Deck và có nội dung mặt trước/sau
            return SelectedDeck != null &&
                   !string.IsNullOrWhiteSpace(FrontText) &&
                   !string.IsNullOrWhiteSpace(BackText);
            // Answer có thể tùy chọn, hoặc bắt buộc tùy logic của bạn
        }

        [RelayCommand(CanExecute = nameof(CanSaveCard))]
        private async Task SaveCard()
        {
            try
            {
                var newCard = new Card
                {
                    // ID tự sinh trong Model hoặc Repository
                    DeckId = SelectedDeck.Id,
                    FrontText = this.FrontText,
                    BackText = this.BackText,
                    Answer = this.Answer ?? string.Empty, // Tránh null

                    FrontImagePath = this.FrontImagePath,
                    FrontAudioPath = this.FrontAudioPath,
                    BackImagePath = this.BackImagePath,
                    BackAudioPath = this.BackAudioPath,
                };

                await _cardRepository.AddAsync(newCard);

                // Gửi tin nhắn cập nhật UI
                _messenger.Send(new CardAddedMessage(SelectedDeck.Id));

                MessageBox.Show("Card added successfully!", "Success");

                // Reset Form để nhập thẻ tiếp theo
                FrontText = string.Empty;
                BackText = string.Empty;
                Answer = string.Empty;
                RemoveFrontImage(); RemoveFrontAudio();
                RemoveBackImage(); RemoveBackAudio();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving card: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- MEDIA LOGIC ---

        private void ProcessAndSetMedia(string sourcePath, ref string pathProperty, ref string nameProperty)
        {
            if (string.IsNullOrWhiteSpace(sourcePath)) return;

            try
            {
                if (sourcePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    pathProperty = sourcePath;
                    nameProperty = "🌐 Online Link";
                }
                else
                {
                    string fileName = Path.GetFileName(sourcePath);
                    string extension = Path.GetExtension(fileName);
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    string uniqueName = $"{nameWithoutExt}_{Guid.NewGuid().ToString().Substring(0, 8)}{extension}";

                    string mediaFolder = PathHelper.GetMediaFolderPath();
                    string destPath = Path.Combine(mediaFolder, uniqueName);

                    File.Copy(sourcePath, destPath, overwrite: true);

                    pathProperty = uniqueName;
                    nameProperty = fileName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing media: {ex.Message}", "Error");
            }
        }

        private string? PickFile(string filter)
        {
            OpenFileDialog dialog = new OpenFileDialog { Title = "Select Media", Filter = filter };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        [RelayCommand]
        private void PickFrontImage()
        {
            var path = PickFile("Images|*.png;*.jpg;*.jpeg;*.gif|All Files|*.*");
            if (path != null) { ProcessAndSetMedia(path, ref _frontImagePath, ref _frontImageName); OnPropertyChanged(nameof(FrontImagePath)); OnPropertyChanged(nameof(FrontImageName)); }
        }

        [RelayCommand]
        private void PickFrontAudio()
        {
            var path = PickFile("Audio|*.mp3;*.wav;*.m4a|All Files|*.*");
            if (path != null) { ProcessAndSetMedia(path, ref _frontAudioPath, ref _frontAudioName); OnPropertyChanged(nameof(FrontAudioPath)); OnPropertyChanged(nameof(FrontAudioName)); }
        }

        [RelayCommand]
        private void PickBackImage()
        {
            var path = PickFile("Images|*.png;*.jpg;*.jpeg;*.gif|All Files|*.*");
            if (path != null) { ProcessAndSetMedia(path, ref _backImagePath, ref _backImageName); OnPropertyChanged(nameof(BackImagePath)); OnPropertyChanged(nameof(BackImageName)); }
        }

        [RelayCommand]
        private void PickBackAudio()
        {
            var path = PickFile("Audio|*.mp3;*.wav;*.m4a|All Files|*.*");
            if (path != null) { ProcessAndSetMedia(path, ref _backAudioPath, ref _backAudioName); OnPropertyChanged(nameof(BackAudioPath)); OnPropertyChanged(nameof(BackAudioName)); }
        }

        // --- OTHER COMMANDS ---

        [RelayCommand]
        private async Task ChooseDeck()
        {
            // Sử dụng ServiceProvider của App để lấy Window (đã đăng ký DI)
            var chooseDeckWindow = EasyFlips.App.ServiceProvider.GetRequiredService<ChooseDeckWindow>();

            chooseDeckWindow.ShowDialog();

            if (chooseDeckWindow.SelectedDeck != null)
            {
                SelectedDeck = chooseDeckWindow.SelectedDeck;
            }
        }

        [RelayCommand]
        private void Cancel(object? param)
        {
            if (param is Window window) window.Close();
        }

        // --- REMOVE COMMANDS ---
        [RelayCommand] private void RemoveFrontImage() { FrontImagePath = null; FrontImageName = null; }
        [RelayCommand] private void RemoveFrontAudio() { FrontAudioPath = null; FrontAudioName = null; }
        [RelayCommand] private void RemoveBackImage() { BackImagePath = null; BackImageName = null; }
        [RelayCommand] private void RemoveBackAudio() { BackAudioPath = null; BackAudioName = null; }

        // ... (Giữ nguyên PasteLink và OpenFileLocation nếu có) ...
    }
}