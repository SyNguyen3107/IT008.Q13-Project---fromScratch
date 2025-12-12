using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EasyFlips.Interfaces;
using EasyFlips.Messages;
using EasyFlips.Models;
using EasyFlips.Services;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace EasyFlips.ViewModels
{
    public partial class MainViewModel : ObservableObject,
                                         IRecipient<DeckAddedMessage>,
                                         IRecipient<DeckUpdatedMessage>,
                                         IRecipient<CardAddedMessage>,
                                         IRecipient<StudySessionCompletedMessage>,
                                         IRecipient<SyncCompletedMessage> // [FIX]: Đăng ký nhận tin nhắn Sync xong
    {
        private readonly IDeckRepository _deckRepository;
        private readonly INavigationService _navigationService;
        private readonly IMessenger _messenger;
        private readonly ImportService _importService;
        private readonly ExportService _exportService;
        private readonly IAuthService _authService;

        [ObservableProperty] private string currentEmail;
        [ObservableProperty] private bool _isConnected;
        public UserSession UserSession { get; private set; }
        public ObservableCollection<Deck> Decks { get; } = new ObservableCollection<Deck>();

        public MainViewModel(IDeckRepository deckRepository,
                             INavigationService navigationService,
                             IMessenger messenger,
                             ImportService importService,
                             ExportService exportService,
                             UserSession userSession,
                             IAuthService authService)
        {
            _deckRepository = deckRepository;
            _navigationService = navigationService;
            _messenger = messenger;
            _exportService = exportService;
            _importService = importService;
            _authService = authService;

            _messenger.RegisterAll(this);
            CurrentEmail = userSession.Email;
            UserSession = userSession;

            IsConnected = NetworkService.Instance.IsConnected;
            NetworkService.Instance.ConnectivityChanged += OnConnectivityChanged;

            RefreshDecks();
        }

        private void OnConnectivityChanged(bool isConnected)
        {
            Application.Current.Dispatcher.Invoke(() => IsConnected = isConnected);
        }

        public void Receive(DeckAddedMessage message) => RefreshDecks();
        public void Receive(CardAddedMessage message) => RefreshDecks();
        public void Receive(StudySessionCompletedMessage message) => RefreshDecks();
        public void Receive(DeckUpdatedMessage message) => RefreshDecks();

        // [FIX]: Xử lý khi Sync hoàn tất -> Reload toàn bộ Deck
        public void Receive(SyncCompletedMessage message) => RefreshDecks();

        private void RefreshDecks()
        {
            Application.Current.Dispatcher.Invoke(async () => await LoadDecksAsync());
        }

        public async Task LoadDecksAsync()
        {
            var decks = await _deckRepository.GetAllAsync();
            Decks.Clear();
            foreach (var deck in decks) Decks.Add(deck);
        }

        // --- COMMANDS ---

        // Lệnh Reload danh sách Deck (Gán vào nút "Decks" ở MainWindow)
        [RelayCommand]
        private async Task ReloadDecks()
        {
            await LoadDecksAsync();
        }

        [RelayCommand] private void ShowDeckChosen(Deck selectedDeck) { if (selectedDeck != null) _navigationService.ShowDeckChosenWindow(selectedDeck.Id); }
        [RelayCommand] private void StartStudy(Deck selectedDeck) { if (selectedDeck != null) _navigationService.ShowStudyWindow(selectedDeck.Id); }
        [RelayCommand] private void CreateDeck() => _navigationService.ShowCreateDeckWindow();
        [RelayCommand] private void AddCard() => _navigationService.ShowAddCardWindow();
        [RelayCommand] private void RenameDeck(Deck deck) { if (deck != null) _navigationService.ShowDeckRenameWindow(deck); }
        [RelayCommand] private void Sync() => _navigationService.ShowSyncWindow();

        [RelayCommand]
        private async Task ImportFile()
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = "Deck Files (.zip)|*.zip" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var newDeck = await _importService.ImportDeckFromZipAsync(dlg.FileName);
                    if (newDeck != null)
                    {
                        _messenger.Send(new DeckAddedMessage(newDeck));
                        MessageBox.Show($"Imported '{newDeck.Name}' successfully!");
                    }
                }
                catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
            }
        }

        [RelayCommand]
        private async Task ExportDeck(Deck deck)
        {
            if (deck == null) return;
            SaveFileDialog dlg = new SaveFileDialog { FileName = deck.Name, DefaultExt = ".zip", Filter = "Deck Package (.zip)|*.zip" };
            if (dlg.ShowDialog() == true)
            {
                await _exportService.ExportDeckToZipAsync(deck.Id, dlg.FileName);
                MessageBox.Show("Exported successfully!");
            }
        }

        [RelayCommand]
        private async Task DeleteDeck(Deck deck)
        {
            if (deck == null) return;
            if (MessageBox.Show($"Delete '{deck.Name}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                await _deckRepository.DeleteAsync(deck.Id);
                RefreshDecks();
            }
        }

        [RelayCommand]
        private async Task Logout()
        {
            await _authService.LogoutAsync();
            _navigationService.ShowLoginWindow();
            CloseCurrentWindow();
        }
        [RelayCommand]
        private void CloseCurrentWindow()
        {
            Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this)?.Close();
        }
    }
}