using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using IT008.Q13_Project___fromScratch.Repositories;
using IT008.Q13_Project___fromScratch.Models;

namespace IT008.Q13_Project___fromScratch
{
    public class MainAnkiViewModel
    {
        private readonly IDeckRepository _deckRepository;

        //Tạo ObservableCollection để lưu trữ danh sách Decks
        public ObservableCollection<Deck> Decks { get; private set; } = new ObservableCollection<Deck>();
        // Nhận IDeckRepository vào hàm khoi tạo Constructor
        public MainAnkiViewModel(IDeckRepository deckRepository)
        {
            this._deckRepository = deckRepository;
        }
        // Tải danh sách Decks từ repository
        public async Task LoadDecksAsync()
        {
            var decks = await _deckRepository.GetAllAsync();
            Decks.Clear(); // Xóa danh sách hiện tại trước khi thêm mới
            foreach (var deck in decks)
            {
                Decks.Add(deck); // Thêm từng Deck vào ObservableCollection
            }
        }

    }
}
