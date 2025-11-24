using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
//Để giải nén file .zip
using System.IO.Compression;

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

        public async Task<Deck?> ImportDeckFromZipAsync(string zipPath)
        {
            if (!File.Exists(zipPath)) return null;

            // 1. Giải nén file zip ra thư mục tạm
            string extractPath = Path.Combine(Path.GetTempPath(), "DeckImport_" + Guid.NewGuid());
            ZipFile.ExtractToDirectory(zipPath, extractPath);

            // 2. Đọc file deck.json
            string jsonPath = Path.Combine(extractPath, "deck.json");
            if (!File.Exists(jsonPath)) return null;

            string jsonString = await File.ReadAllTextAsync(jsonPath);
            var exportData = JsonSerializer.Deserialize<DeckExportModel>(jsonString);

            if (exportData == null) return null;

            // 3. Xử lý tên deck tránh trùng lặp
            string finalDeckName = exportData.DeckName;
            int count = 1;
            while (await _deckRepository.GetByNameAsync(finalDeckName) != null)
            {
                finalDeckName = $"{exportData.DeckName} ({count})";
                count++;
            }

            var newDeck = new Deck
            {
                Name = finalDeckName,
                Description = exportData.Description ?? ""
            };

            await _deckRepository.AddAsync(newDeck);

            // 4. Tạo cards và nối lại đường dẫn media
            if (exportData.Cards != null)
            {
                foreach (var cardModel in exportData.Cards)
                {
                    var newCard = new Card
                    {
                        DeckId = newDeck.ID,
                        FrontText = cardModel.FrontText,
                        BackText = cardModel.BackText,
                        Answer = cardModel.Answer ?? "", // Đặt giá trị mặc định nếu null
                        FrontImagePath = string.IsNullOrEmpty(cardModel.FrontImageName) ? null : Path.Combine(extractPath, "media", cardModel.FrontImageName),
                        BackImagePath = string.IsNullOrEmpty(cardModel.BackImageName) ? null : Path.Combine(extractPath, "media", cardModel.BackImageName),
                        FrontAudioPath = string.IsNullOrEmpty(cardModel.FrontAudioName) ? null : Path.Combine(extractPath, "media", cardModel.FrontAudioName),
                        BackAudioPath = string.IsNullOrEmpty(cardModel.BackAudioName) ? null : Path.Combine(extractPath, "media", cardModel.BackAudioName),
                        Progress = null
                    };
                    await _cardRepository.AddAsync(newCard);
                }
            }

            // 5. Reload deck để có thống kê chính xác
            var reloadedDeck = await _deckRepository.GetByIdAsync(newDeck.ID);
            return reloadedDeck ?? newDeck;
        }
    }
}