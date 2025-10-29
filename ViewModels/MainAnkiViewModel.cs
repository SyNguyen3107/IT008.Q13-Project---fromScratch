using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using IT008.Q13_Project___fromScratch.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Microsoft.Win32; // Dùng cho OpenFileDialog
using System;             // Dùng cho Environment
using System.Diagnostics; // Dùng cho Debug.WriteLine
using System.Windows.Input;
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
            _navigationService = navigationService; //tạo đối tượng navigationService để gọi gián tiếp các View
        }

        [RelayCommand]
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
        [RelayCommand]
        private void ImportFile()
        {
            // Đây là code bạn đã viết trong code-behind
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Import";
            openFileDialog.Filter = "All supported formats (*.apkg, *.txt, *.zip)|*.apkg;*.txt;*.zip" +
                                    "|Anki Deck Package (*.apkg)|*.apkg" +
                                    "|Text file (*.txt)|*.txt" +
                                    "|Zip file (*.zip)|*.zip";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                Debug.WriteLine($"File to import: {selectedFilePath}");
                // TODO: Gọi service để xử lý file (ví dụ: _importService.Import(selectedFilePath))
            }
            else
            {
                // Người dùng đã nhấn "Cancel"
            }
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