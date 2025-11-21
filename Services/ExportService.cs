using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using System.Text.Json; // Thư viện có sẵn cực mạnh của .NET
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace IT008.Q13_Project___fromScratch.Services
{
    public class ExportService
    {
        private readonly IDeckRepository _deckRepository;
        private readonly ICardRepository _cardRepository;

        public ExportService(IDeckRepository deckRepository, ICardRepository cardRepository)
        {
            _deckRepository = deckRepository;
            _cardRepository = cardRepository;
        }

        public async Task ExportDeckToJsonAsync(int deckId, string filePath)
        {
            // 1. Lấy dữ liệu từ DB
            var deck = await _deckRepository.GetByIdAsync(deckId);
            var cards = await _cardRepository.GetCardsByDeckIdAsync(deckId);

            if (deck == null) return;

            // 2. Chuyển đổi sang Model Export (Bỏ qua ID và Progress)
            var exportData = new DeckExportModel
            {
                DeckName = deck.Name,
                Description = deck.Description,
                Cards = cards.Select(c => new CardExportModel
                {
                    FrontText = c.FrontText,
                    BackText = c.BackText,
                    FrontImagePath = c.FrontImagePath,
                    BackImagePath = c.BackImagePath
                }).ToList()
            };

            // 3. Cấu hình JSON cho đẹp (có xuống dòng, dễ đọc)
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Để không bị lỗi font tiếng Việt
            };

            // 4. Chuyển object thành chuỗi JSON
            string jsonString = JsonSerializer.Serialize(exportData, options);

            // 5. Ghi ra file
            await File.WriteAllTextAsync(filePath, jsonString);
        }
    }
}