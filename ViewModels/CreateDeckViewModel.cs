using System;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
using IT008.Q13_Project___fromScratch.Models;
using IT008.Q13_Project___fromScratch.Repositories;

namespace IT008.Q13_Project___fromScratch.ViewModels
{
    public class CreateDeckViewModel  : BaseViewModel
    {
        private readonly IDeckRepository _deckRepository;
        // Thuộc tính Name (binding từ TextBox)
        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
        // Command cho nút OK và Cancel
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        // Constructor 
        public CreateDeckViewModel(IDeckRepository deckRepository)
        {
            _deckRepository = deckRepository;

            SaveCommand = new RelayCommand(async (param) =>
            {
                // (1) Tạo đối tượng Deck mới
                var newDeck = new Deck
                {
                    Name = this.Name,
                    Description = this.Description,
                    Cards = new System.Collections.Generic.List<Card>()
                };
                // (2) Gọi repository để lưu deck
                await _deckRepository.AddAsync(newDeck);
                // (3) Đóng cửa sổ (nếu CommandParameter là Window)
                if (param is Window window)
                    window.Close();

            }, (param) => !string.IsNullOrWhiteSpace(Name)); // Chỉ khi có Name mới cho bấm Save

            CancelCommand = new RelayCommand((param) =>
            {
                if (param is Window window)
                    window.Close();
            });
        }
    }
}
