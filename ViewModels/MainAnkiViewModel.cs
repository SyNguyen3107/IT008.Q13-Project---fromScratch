using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using IT008.Q13_Project___fromScratch.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT008.Q13_Project___fromScratch.ViewModels
{
    public partial class MainAnkiViewModel : ObservableObject
    {
        private readonly IDeckRepository _deckRepository;
        private readonly INavigationService _navigationService;

        //ĐỊNH NGHĨA THUỘC TÍNH "Decks"
        public ObservableCollection<Deck> Decks { get; } = new ObservableCollection<Deck>(); //Bất kỳ thay đổi nào (thêm/xóa) trong ObservableCollection sẽ tự động cập nhật lên ListView trong giao diện.

        public MainAnkiViewModel(IDeckRepository deckRepository, INavigationService navigationService)
        {
            _deckRepository = deckRepository;
            _navigationService = navigationService;//tạo đối tượng navigationService để gọi gián tiếp các View
        }

        [RelayCommand] // Dòng 37 đây, giờ sẽ hết lỗi
        private void StartStudy(Deck selectedDeck)
        {
            if (selectedDeck == null) return;
            _navigationService.ShowStudyWindow(selectedDeck.ID);
        }

        [RelayCommand]
        private void CreateDeck()
        {
            _navigationService.ShowCreateDeckWindow();
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
    }
}