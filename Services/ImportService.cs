using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace IT008.Q13_Project___fromScratch.Services
{
    public class ImportService
    {
        private readonly IDeckRepository _deckRepository;
        private readonly ICardRepository _cardRepository;

        public ImportService(IDeckRepository deckRepository, ICardRepository cardRepository)
        {
            _deckRepository = deckRepository;
            _cardRepository = cardRepository;
        }

        public async Task<Deck?> ImportDeckFromJsonAsync(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            // 1. Đọc và Deserialize JSON
            string jsonString = await File.ReadAllTextAsync(filePath);
            var exportData = JsonSerializer.Deserialize<DeckExportModel>(jsonString);

            if (exportData == null) return null;

            // 2. Xử lý TÊN DECK (Tránh trùng lặp)
            string finalDeckName = exportData.DeckName;
            int count = 1;

            // Vòng lặp kiểm tra: Nếu tên đã tồn tại -> Thêm số (1), (2)...
            while (await _deckRepository.GetByNameAsync(finalDeckName) != null)
            {
                finalDeckName = $"{exportData.DeckName} ({count})";
                count++;
            }

            // 3. Tạo Deck Mới
            var newDeck = new Deck
            {
                Name = finalDeckName, // Dùng tên đã xử lý trùng
                Description = exportData.Description ?? "",
                // NewCount, LearnCount, DueCount sẽ được tính lại khi reload từ DB
            };

            // Lưu Deck trước để có ID
            await _deckRepository.AddAsync(newDeck);

            // 4. Tạo Cards (Tất cả cards mới import đều là New - không có Progress)
            if (exportData.Cards != null)
            {
                foreach (var cardModel in exportData.Cards)
                {
                    var newCard = new Card
                    {
                        DeckId = newDeck.ID, // Link với Deck vừa tạo
                        FrontText = cardModel.FrontText,
                        BackText = cardModel.BackText,
                        FrontImagePath = cardModel.FrontImagePath,
                        BackImagePath = cardModel.BackImagePath,
                        Answer = cardModel.Answer ?? "",
                        // FrontAudioPath, BackAudioPath... (tùy bạn mở rộng)

                        // Quan trọng: Không set Progress, để thẻ ở trạng thái New
                        Progress = null
                    };
                    await _cardRepository.AddAsync(newCard);
                }
            }

            // 5. Reload deck từ database để có thống kê chính xác (NewCount, LearnCount, DueCount)
            var reloadedDeck = await _deckRepository.GetByIdAsync(newDeck.ID);
            
            // Nếu reload thành công thì trả về deck đã có thống kê, nếu không thì trả về deck gốc
            return reloadedDeck ?? newDeck;
        }
    }
}