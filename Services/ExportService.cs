using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using System.Text.Json; // Thư viện có sẵn cực mạnh của .NET
using System.IO;
using System.IO.Compression;
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
                    Answer = c.Answer,
                    // Lấy tên file từ đường dẫn đầy đủ
                    FrontImageName = string.IsNullOrEmpty(c.FrontImagePath) ? null : Path.GetFileName(c.FrontImagePath),
                    BackImageName = string.IsNullOrEmpty(c.BackImagePath) ? null : Path.GetFileName(c.BackImagePath),
                    FrontAudioName = string.IsNullOrEmpty(c.FrontAudioPath) ? null : Path.GetFileName(c.FrontAudioPath),
                    BackAudioName = string.IsNullOrEmpty(c.BackAudioPath) ? null : Path.GetFileName(c.BackAudioPath)

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



            // 5. Tạo thư mục export kèm media
            string exportFolder = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath));
            Directory.CreateDirectory(exportFolder);

            string mediaFolder = Path.Combine(exportFolder, "media");
            Directory.CreateDirectory(mediaFolder);

            // Ghi deck.json vào exportFolder
            string jsonPath = Path.Combine(exportFolder, "deck.json");
            await File.WriteAllTextAsync(jsonPath, jsonString);

            // Copy media vào mediaFolder
            foreach (var c in cards)
            {
                // Copy hình ảnh nếu có
                if (!string.IsNullOrEmpty(c.FrontImagePath) && File.Exists(c.FrontImagePath))
                    File.Copy(c.FrontImagePath, Path.Combine(mediaFolder, Path.GetFileName(c.FrontImagePath)), true);
                if (!string.IsNullOrEmpty(c.BackImagePath) && File.Exists(c.BackImagePath))
                    File.Copy(c.BackImagePath, Path.Combine(mediaFolder, Path.GetFileName(c.BackImagePath)), true);
                // Copy audio nếu có
                if (!string.IsNullOrEmpty(c.BackAudioPath) && File.Exists(c.BackAudioPath))
                    File.Copy(c.BackAudioPath, Path.Combine(mediaFolder, Path.GetFileName(c.BackAudioPath)), true);
                if (!string.IsNullOrEmpty(c.FrontAudioPath) && File.Exists(c.FrontAudioPath))
                    File.Copy(c.FrontAudioPath, Path.Combine(mediaFolder, Path.GetFileName(c.FrontAudioPath)), true);
            }

            // 6. Nén thư mục exportFolder thành file .zip
            string zipPath = filePath.EndsWith(".zip") ? filePath : filePath + ".zip";
            if (File.Exists(zipPath)) File.Delete(zipPath); // tránh lỗi nếu file đã tồn tại
            ZipFile.CreateFromDirectory(exportFolder, zipPath);

        }
    }
}