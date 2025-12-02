using EasyFlips.Interfaces;
using EasyFlips.Models;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using EasyFlips.Helpers; // ✅ Dùng PathHelper
using System;
using System.Threading.Tasks;

namespace EasyFlips.Services
{
    public class ImportService
    {
        private readonly IDeckRepository _deckRepository;
        private readonly ICardRepository _cardRepository;
        private readonly IAuthService _authService; // ✅ Cần để biết Deck này của ai

        public ImportService(IDeckRepository deckRepository, ICardRepository cardRepository, IAuthService authService)
        {
            _deckRepository = deckRepository;
            _cardRepository = cardRepository;
            _authService = authService;
        }

        public async Task<Deck?> ImportDeckFromZipAsync(string zipPath)
        {
            if (!File.Exists(zipPath)) return null;

            // 1. Giải nén zip ra thư mục tạm
            string extractPath = Path.Combine(Path.GetTempPath(), "DeckImport_" + Guid.NewGuid());
            Directory.CreateDirectory(extractPath);

            try
            {
                ZipFile.ExtractToDirectory(zipPath, extractPath);

                // 2. Đọc deck.json
                string jsonPath = Path.Combine(extractPath, "deck.json");
                if (!File.Exists(jsonPath)) return null;

                string jsonString = await File.ReadAllTextAsync(jsonPath);
                var exportData = JsonSerializer.Deserialize<DeckExportModel>(jsonString);
                if (exportData == null) return null;

                // 3. Xử lý tên deck (Tránh trùng tên trong DB)
                string zipFileName = Path.GetFileNameWithoutExtension(zipPath);
                string finalDeckName = string.IsNullOrEmpty(exportData.DeckName) ? zipFileName : exportData.DeckName;
                int count = 1;
                while (await _deckRepository.GetByNameAsync(finalDeckName) != null)
                {
                    finalDeckName = $"{exportData.DeckName} ({count})";
                    count++;
                }

                // Tạo Deck mới và gán chủ sở hữu (UserId)
                var newDeck = new Deck
                {
                    Name = finalDeckName,
                    Description = exportData.Description ?? "",
                    UserId = _authService.CurrentUserId // ✅ Gán cho User hiện tại (hoặc null nếu là Khách)
                };
                await _deckRepository.AddAsync(newDeck);

                // 4. Chuẩn bị xử lý Media
                string extractedMediaFolder = Path.Combine(extractPath, "media");

                // Lấy đường dẫn chuẩn từ Helper
                string appMediaFolder = PathHelper.GetMediaFolderPath();

                // Hàm cục bộ để xử lý và copy file
                string? ProcessMediaPath(string? mediaName)
                {
                    if (string.IsNullOrEmpty(mediaName)) return null;

                    // TRƯỜNG HỢP 1: Là URL Online -> Giữ nguyên
                    if (mediaName.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        return mediaName;
                    }

                    // TRƯỜNG HỢP 2: Là File Local -> Copy vào AppData
                    string sourceFile = Path.Combine(extractedMediaFolder, mediaName);

                    if (File.Exists(sourceFile))
                    {
                        string destFile = Path.Combine(appMediaFolder, mediaName);

                        // Chỉ copy nếu chưa tồn tại (hoặc ghi đè nếu muốn update ảnh)
                        if (!File.Exists(destFile))
                        {
                            File.Copy(sourceFile, destFile, true);
                        }

                        // ✅ QUAN TRỌNG: Chỉ trả về TÊN FILE (Relative Path) để lưu vào DB
                        // Code cũ trả về destFile (đường dẫn tuyệt đối) là SAI logic mới.
                        return mediaName;
                    }

                    return null; // File không tìm thấy trong gói zip -> bỏ qua
                }

                // 5. Tạo cards
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

                            // Xử lý đường dẫn media (Copy file và lấy tên)
                            FrontImagePath = ProcessMediaPath(cardModel.FrontImageName),
                            BackImagePath = ProcessMediaPath(cardModel.BackImageName),
                            FrontAudioPath = ProcessMediaPath(cardModel.FrontAudioName),
                            BackAudioPath = ProcessMediaPath(cardModel.BackAudioName),

                            Progress = null // Reset tiến độ học khi import
                        };
                        await _cardRepository.AddAsync(newCard);
                    }
                }

                var reloadedDeck = await _deckRepository.GetByIdAsync(newDeck.ID);
                return reloadedDeck ?? newDeck;
            }
            finally
            {
                // Dọn dẹp thư mục tạm
                if (Directory.Exists(extractPath))
                {
                    try { Directory.Delete(extractPath, true); } catch { }
                }
            }
        }
    }
}