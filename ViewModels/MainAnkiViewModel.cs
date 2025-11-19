using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using IT008.Q13_Project___fromScratch.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using IT008.Q13_Project___fromScratch.Messages;
using Microsoft.Win32; // Dùng cho OpenFileDialog
using System;             // Dùng cho Environment
using System.Diagnostics; // Dùng cho Debug.WriteLine
using System.Windows.Input;
using System.Threading.Tasks;

namespace IT008.Q13_Project___fromScratch.ViewModels
{
    public partial class MainAnkiViewModel : ObservableObject, IRecipient<DeckAddedMessage>
    {
        private readonly IDeckRepository _deckRepository;
        private readonly INavigationService _navigationService;
        private readonly IMessenger _messenger;
        public ObservableCollection<Deck> Decks { get; } = new ObservableCollection<Deck>();
        public MainAnkiViewModel(IDeckRepository deckRepository, INavigationService navigationService, IMessenger messenger)
        {
            _deckRepository = deckRepository;
            _navigationService = navigationService; //tạo đối tượng navigationService để gọi gián tiếp các View
            _messenger = messenger;

            // Đăng ký nhận tất cả tin nhắn mà ViewModel này quan tâm
            _messenger.RegisterAll(this);
        }
        public void Receive(DeckAddedMessage message)
        {
            // message.Value chính là "newDeck" được gửi từ CreateDeckViewModel
            var newDeck = message.Value;

            // Thêm Deck mới vào danh sách.
            // Vì Decks là ObservableCollection, UI sẽ tự động cập nhật!
            // Chúng ta không cần gọi lại LoadDecksAsync()
            Decks.Add(newDeck);
        }

        [RelayCommand]
        private void StartStudy(Deck selectedDeck)
        {
            if (selectedDeck == null) return;
            _navigationService.ShowStudyWindow(selectedDeck.ID);
        }

        [RelayCommand]
        private void CreateDeck() //Command để mở cửa sổ CreateDeckWindow (thông qua NavigationService)
        {
            _navigationService.ShowCreateDeckWindow();
        }
        [RelayCommand]
        //Lý do định nghĩa hàm mở cửa sổ Import File ở đây thay vì trong lớp NavigationService và gọi thông qua đối tượng _naviagtionService (Giống với tính năng CreateDeck ở trên):
        //Đây không phải là "điều hướng" (navigation) đến một cửa sổ khác của ứng dụng. Nó là một hành động "hỏi" hệ điều hành để lấy một thông tin.
        private void ImportFile()
        {
            _navigationService.ImportFileWindow();
        }
        [RelayCommand]
        private void AddCard()
        {
            _navigationService.ShowAddCardWindow();
        }
        public async Task LoadDecksAsync() // async giúp ứng dụng không bị "đơ" khi đang tải dữ liệu từ CSDL.
        {
            var decks = await _deckRepository.GetAllAsync();
            Decks.Clear(); // Giờ code này sẽ chạy được
            foreach (var deck in decks)
            {
                Decks.Add(deck);
            }
        }

        [RelayCommand]
        void ShowDeckChosenCommand(Deck selectedDeck)
        {
            if (selectedDeck != null)
            {
                _navigationService.ShowDeckChosenWindow(selectedDeck.ID);
            }    
        }
    }
}