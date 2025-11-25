using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using System.Text.Json;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Linq;
using System;

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

        public async Task ExportDeckToZipAsync(int deckId, string filePath)
        {
            var deck = await _deckRepository.GetByIdAsync(deckId);
            var cards = await _cardRepository.GetCardsByDeckIdAsync(deckId);
            if (deck == null) return;

            // Hàm phụ trợ: Xử lý tên file để lưu vào JSON
            // - Nếu là URL -> Giữ nguyên
            // - Nếu là Local -> Chỉ lấy tên file
            string? GetExportName(string? path)
            {
                if (string.IsNullOrEmpty(path)) return null;
                if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return path;
                return Path.GetFileName(path);
            }

            // 1. Chuẩn bị dữ liệu JSON
            var exportData = new DeckExportModel
            {
                DeckName = deck.Name,
                Description = deck.Description,
                Cards = cards.Select(c => new CardExportModel
                {
                    FrontText = c.FrontText,
                    BackText = c.BackText,
                    Answer = c.Answer,
                    // SỬA LỖI: Dùng hàm GetExportName để không làm hỏng URL
                    FrontImageName = GetExportName(c.FrontImagePath),
                    BackImageName = GetExportName(c.BackImagePath),
                    FrontAudioName = GetExportName(c.FrontAudioPath),
                    BackAudioName = GetExportName(c.BackAudioPath)
                }).ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string jsonString = JsonSerializer.Serialize(exportData, options);

            // 2. Tạo file Zip
            if (File.Exists(filePath)) File.Delete(filePath);

            using (var zipStream = new FileStream(filePath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                // Ghi deck.json
                var jsonEntry = archive.CreateEntry("deck.json");
                using (var writer = new StreamWriter(jsonEntry.Open()))
                {
                    await writer.WriteAsync(jsonString);
                }

                // 3. Copy media local vào zip
                foreach (var c in cards)
                {
                    void CopyIfExists(string? path)
                    {
                        // Chỉ copy nếu là file local tồn tại (Bỏ qua URL)
                        if (!string.IsNullOrEmpty(path)
                            && !path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                            && File.Exists(path))
                        {
                            // Tránh lỗi trùng tên file trong zip (nếu cần thiết có thể xử lý thêm)
                            try
                            {
                                archive.CreateEntryFromFile(path, Path.Combine("media", Path.GetFileName(path)));
                            }
                            catch { /* Bỏ qua nếu file đã được thêm rồi */ }
                        }
                    }

                    CopyIfExists(c.FrontImagePath);
                    CopyIfExists(c.BackImagePath);
                    CopyIfExists(c.FrontAudioPath);
                    CopyIfExists(c.BackAudioPath);
                }
            }
        }
    }
}