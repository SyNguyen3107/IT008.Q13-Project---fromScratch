using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
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

            // 1. Giải nén zip ra thư mục tạm
            string extractPath = Path.Combine(Path.GetTempPath(), "DeckImport_" + Guid.NewGuid());
            ZipFile.ExtractToDirectory(zipPath, extractPath);

            // 2. Đọc deck.json
            string jsonPath = Path.Combine(extractPath, "deck.json");
            if (!File.Exists(jsonPath)) return null;

            string jsonString = await File.ReadAllTextAsync(jsonPath);
            var exportData = JsonSerializer.Deserialize<DeckExportModel>(jsonString);
            if (exportData == null) return null;

            // 3. Xử lý tên deck tránh trùng
            // Lấy tên file zip (không có phần mở rộng)
            string zipFileName = Path.GetFileNameWithoutExtension(zipPath);

            // Nếu muốn dùng tên file zip làm tên deck thay vì tên trong deck.json:
            string finalDeckName = zipFileName;

            // Nếu vẫn muốn fallback sang tên trong deck.json:
            if (string.IsNullOrEmpty(finalDeckName))
            {
                finalDeckName = exportData.DeckName;
            }

            // Xử lý tránh trùng
            int count = 1;
            while (await _deckRepository.GetByNameAsync(finalDeckName) != null)
            {
                finalDeckName = $"{zipFileName} ({count})";
                count++;
            }


            var newDeck = new Deck
            {
                Name = finalDeckName,
                Description = exportData.Description ?? ""
            };
            await _deckRepository.AddAsync(newDeck);

            // 4. Lấy đường dẫn media ngay tại thư mục export
            string mediaSource = Path.Combine(extractPath, "media");

            // 5. Tạo cards, gắn đường dẫn tuyệt đối tới mediaSource
            if (exportData.Cards != null)
            {
                foreach (var cardModel in exportData.Cards)
                {
                    var newCard = new Card
                    {
                        DeckId = newDeck.ID,
                        FrontText = cardModel.FrontText,
                        BackText = cardModel.BackText,
                        Answer = cardModel.Answer ?? "",
                        FrontImagePath = string.IsNullOrEmpty(cardModel.FrontImageName) ? null : Path.Combine(mediaSource, cardModel.FrontImageName),
                        BackImagePath = string.IsNullOrEmpty(cardModel.BackImageName) ? null : Path.Combine(mediaSource, cardModel.BackImageName),
                        FrontAudioPath = string.IsNullOrEmpty(cardModel.FrontAudioName) ? null : Path.Combine(mediaSource, cardModel.FrontAudioName),
                        BackAudioPath = string.IsNullOrEmpty(cardModel.BackAudioName) ? null : Path.Combine(mediaSource, cardModel.BackAudioName),
                        Progress = null
                    };
                    await _cardRepository.AddAsync(newCard);
                }
            }

            // 6. Reload deck để có thống kê chính xác
            var reloadedDeck = await _deckRepository.GetByIdAsync(newDeck.ID);

            

            return reloadedDeck ?? newDeck;
        }
    }
}
