using EasyFlips.Interfaces;
using EasyFlips.Models;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace EasyFlips.Services
{
    public class ImportService
    {
        private readonly IDeckRepository _deckRepository;
        private readonly ICardRepository _cardRepository;

        // Đường dẫn thư mục Media bền vững (AppData)
        private readonly string _appMediaFolder;

        public ImportService(IDeckRepository deckRepository, ICardRepository cardRepository)
        {
            _deckRepository = deckRepository;
            _cardRepository = cardRepository;

            // Tạo đường dẫn: C:\Users\[User]\AppData\Roaming\EasyFlips\Media
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _appMediaFolder = Path.Combine(appData, "EasyFlips", "Media");

            if (!Directory.Exists(_appMediaFolder))
            {
                Directory.CreateDirectory(_appMediaFolder);
            }
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

                // 3. Xử lý tên deck
                string zipFileName = Path.GetFileNameWithoutExtension(zipPath);
                string finalDeckName = string.IsNullOrEmpty(exportData.DeckName) ? zipFileName : exportData.DeckName;
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

                // 4. Chuẩn bị xử lý Media
                string extractedMediaFolder = Path.Combine(extractPath, "media");

                // Hàm cục bộ để xử lý đường dẫn
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
                        string destFile = Path.Combine(_appMediaFolder, mediaName);

                        // Chỉ copy nếu chưa tồn tại để tránh lỗi (hoặc ghi đè nếu muốn)
                        if (!File.Exists(destFile))
                        {
                            File.Copy(sourceFile, destFile);
                        }
                        return destFile; // Trả về đường dẫn AppData bền vững
                    }

                    return null; // File không tìm thấy
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

                            // Xử lý đường dẫn media
                            FrontImagePath = ProcessMediaPath(cardModel.FrontImageName),
                            BackImagePath = ProcessMediaPath(cardModel.BackImageName),
                            FrontAudioPath = ProcessMediaPath(cardModel.FrontAudioName),
                            BackAudioPath = ProcessMediaPath(cardModel.BackAudioName),

                            Progress = null
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