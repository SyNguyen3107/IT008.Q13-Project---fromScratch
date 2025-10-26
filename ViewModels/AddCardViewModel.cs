using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using IT008.Q13_Project___fromScratch.Models;
using IT008.Q13_Project___fromScratch.Repositories;

namespace IT008.Q13_Project___fromScratch.ViewModels
{
    public class AddCardViewModel : BaseViewModel
    {
        private readonly ICardRepository _cardRepository;
        private readonly IDeckRepository _deckRepository;
        // Các thuộc tính dùng Binding
        public string _frontText;
        public string FrontText
        {
            get => _frontText;
            set
            {
                _frontText = value;
                OnPropertyChanged(nameof(FrontText));
            }
        }
        public string _backText;
        public string BackText
        {
            get => _backText;
            set
            {
                _backText = value;
                OnPropertyChanged(nameof(BackText));
            }
        }
        private Deck _selectedDeck;
        public Deck SelectedDeck
        {
            get => _selectedDeck;
            set
            {
                _selectedDeck = value;
                OnPropertyChanged(nameof(SelectedDeck));
            }
        }
        // DS tất cả các Deck để hiển thị trong ListBox (chọn nơi lưu thẻ)
        public ObservableCollection<Deck> Decks { get; set; } = new();
        // Các Command
        public ICommand AddCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand ChooseDeckCommand { get; }
        public ICommand HelpCommand { get; }
        public ICommand HistoryCommand { get; }
        // Constructor
        public AddCardViewModel(ICardRepository cardRepo, IDeckRepository deckRepo)
        {
            _cardRepository = cardRepo;
            _deckRepository = deckRepo;
            // Tải DS Deck hiện có (từ database hoặc bộ nhớ tạm)
            _ = LoadDecksAsync();
            // Thêm thẻ
            AddCommand = new RelayCommand(async(param) =>
            {
                if (SelectedDeck == null)
                {
                    // có thể thêm Message.Show() nếu muốn
                    return;
                }
                var newCard = new Card
                {
                    DeckId = SelectedDeck.ID,
                    Deck = SelectedDeck,
                    FrontText = FrontText,
                    BackText = BackText,
                    // Thời điểm thẻ này cần được ôn lại
                    DueDate = DateTime.Now, // Nếu nhớ tốt, DueDate sẽ dời xa hơn và ngược lại
                    // Khoảng thời gian tính theo ngày giữa 2 lần ôn thẻ
                    Interval = 1, // ôn lại sau 1 ngày, mới thêm nên ôn sớm
                    /* Độ dễ nhớ của thẻ (ease trong thuật toán SM2 của Anki)
                     * => Nó quyết định tốc độ tăng của Interval, Interval = Interval * EaseFactor
                     * Nếu trả lời sai, EaseFactor sẽ giảm xuống
                    */
                    EaseFactor = 2.5
                };
                await _cardRepository.AddAsync(newCard);
                FrontText = string.Empty;
                BackText = string.Empty;
                // Khi thêm thành công 1 thẻ thì cột New ở DeckView sẽ tăng thêm 1
                SelectedDeck.NewCount += 1;
                await _deckRepository.UpdateAsync(SelectedDeck);
                // Xóa nội dung để chuẩn bị cho việc thêm thẻ kế tiếp
                FrontText = string.Empty;
                BackText = string.Empty;
            },
            (param) => !string.IsNullOrWhiteSpace(FrontText) && !string.IsNullOrWhiteSpace(BackText));
            // Close 
            CloseCommand = new RelayCommand((param) =>
            {
                if (param is Window window)
                    window.Close();
            });
            // Mở của sổ ChooseDeck
            ChooseDeckCommand = new RelayCommand((param) =>
            {
                // Tạm thời chưa biết
            });
            // Help 
            HelpCommand = new RelayCommand((param) =>
            {
                // Tạm thời chưa biết
            });
            // History
            HistoryCommand = new RelayCommand((param) =>
            {
                // Tạm thời chưa biết
            });
        }
        // Hàm tải DS Decks
        private async Task LoadDecksAsync()
        {
            var decks = await _deckRepository.GetAllAsync();
            Decks.Clear();
            foreach (var deck in decks)
                Decks.Add(deck);
        }
    }
}
