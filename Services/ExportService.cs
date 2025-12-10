using EasyFlips.Interfaces;
using EasyFlips.Models;
using System.IO;
using System.IO.Compression;
using System.Text.Json; // Giữ nguyên System.Text.Json như code gốc của bạn
using EasyFlips.Helpers; // ✅ BẮT BUỘC: Để dùng PathHelper
using System.Linq;
using System.Threading.Tasks;
using System;

namespace EasyFlips.Services
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

        public async Task ExportDeckToZipAsync(string deckId, string filePath)
        {
            var deck = await _deckRepository.GetByIdAsync(deckId);
            var cards = await _cardRepository.GetCardsByDeckIdAsync(deckId);
            if (deck == null) return;

            // Hàm phụ trợ: Chỉ lấy tên file để lưu vào JSON (giữ nguyên logic của bạn)
            string? GetExportName(string? path)
            {
                if (string.IsNullOrEmpty(path)) return null;
                if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return path;
                return Path.GetFileName(path);
            }

            // 1. Chuẩn bị dữ liệu JSON
            // Lưu ý: Trong JSON chỉ lưu tên file (vd: "image.png") để sang máy khác vẫn hiểu
            var exportData = new DeckExportModel
            {
                DeckName = deck.Name,
                Description = deck.Description,
                Cards = cards.Select(c => new CardExportModel
                {
                    FrontText = c.FrontText,
                    BackText = c.BackText,
                    Answer = c.Answer,
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
                // Ghi file deck.json vào zip
                var jsonEntry = archive.CreateEntry("deck.json");
                using (var writer = new StreamWriter(jsonEntry.Open()))
                {
                    await writer.WriteAsync(jsonString);
                }

                // 3. Copy media local vào zip
                foreach (var c in cards)
                {
                    // --- HÀM CỤC BỘ ĐÃ ĐƯỢC SỬA ĐỔI ---
                    void AddMediaToZip(string? relativeFileName)
                    {
                        if (string.IsNullOrEmpty(relativeFileName)) return;

                        // Bỏ qua nếu là link online
                        if (relativeFileName.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return;

                        // ✅ BƯỚC QUAN TRỌNG: 
                        // relativeFileName lúc này chỉ là "cat.png".
                        // Ta phải dùng PathHelper để tìm ra đường dẫn thật: "C:\Users\Sy\AppData\...\cat.png"
                        string fullPath = PathHelper.GetFullPath(relativeFileName);

                        // Kiểm tra file có thật sự tồn tại trên ổ cứng không
                        if (File.Exists(fullPath))
                        {
                            try
                            {
                                // Tạo đường dẫn trong zip: media/cat.png
                                string entryName = Path.Combine("media", Path.GetFileName(relativeFileName));

                                // Nhét file thật vào trong zip
                                archive.CreateEntryFromFile(fullPath, entryName);
                            }
                            catch
                            {
                                // Bỏ qua lỗi nếu file đã được thêm rồi (trường hợp nhiều card dùng chung 1 ảnh) 
                            }
                        }
                    }
                    // ----------------------------------

                    AddMediaToZip(c.FrontImagePath);
                    AddMediaToZip(c.BackImagePath);
                    AddMediaToZip(c.FrontAudioPath);
                    AddMediaToZip(c.BackAudioPath);
                }
            }
        }
    }
}