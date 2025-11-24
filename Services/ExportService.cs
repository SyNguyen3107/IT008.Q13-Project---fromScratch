using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using System.Text.Json;
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

        public async Task ExportDeckToZipAsync(int deckId, string filePath)
        {
            var deck = await _deckRepository.GetByIdAsync(deckId);
            var cards = await _cardRepository.GetCardsByDeckIdAsync(deckId);
            if (deck == null) return;

            // Chuẩn bị dữ liệu export
            var exportData = new DeckExportModel
            {
                DeckName = deck.Name,
                Description = deck.Description,
                Cards = cards.Select(c => new CardExportModel
                {
                    FrontText = c.FrontText,
                    BackText = c.BackText,
                    Answer = c.Answer,
                    FrontImageName = string.IsNullOrEmpty(c.FrontImagePath) ? null : Path.GetFileName(c.FrontImagePath),
                    BackImageName = string.IsNullOrEmpty(c.BackImagePath) ? null : Path.GetFileName(c.BackImagePath),
                    FrontAudioName = string.IsNullOrEmpty(c.FrontAudioPath) ? null : Path.GetFileName(c.FrontAudioPath),
                    BackAudioName = string.IsNullOrEmpty(c.BackAudioPath) ? null : Path.GetFileName(c.BackAudioPath)
                }).ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string jsonString = JsonSerializer.Serialize(exportData, options);

            // Đảm bảo tên file zip
            string zipPath = filePath.EndsWith(".zip") ? filePath : filePath + ".zip";
            if (File.Exists(zipPath)) File.Delete(zipPath);

            // Tạo zip trực tiếp
            using (var zipStream = new FileStream(zipPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                // Ghi deck.json trực tiếp
                var jsonEntry = archive.CreateEntry("deck.json");
                using (var writer = new StreamWriter(jsonEntry.Open()))
                {
                    await writer.WriteAsync(jsonString);
                }

                // Copy media trực tiếp vào zip
                foreach (var c in cards)
                {
                    void CopyIfExists(string? path)
                    {
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        {
                            archive.CreateEntryFromFile(path, Path.Combine("media", Path.GetFileName(path)));
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
