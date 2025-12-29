using EasyFlips.Interfaces;
using EasyFlips.Models;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using EasyFlips.Helpers;
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

            string? GetExportName(string? path)
            {
                if (string.IsNullOrEmpty(path)) return null;
                if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return path;
                return Path.GetFileName(path);
            }

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

            if (File.Exists(filePath)) File.Delete(filePath);

            using (var zipStream = new FileStream(filePath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var jsonEntry = archive.CreateEntry("deck.json");
                using (var writer = new StreamWriter(jsonEntry.Open()))
                {
                    await writer.WriteAsync(jsonString);
                }

                foreach (var c in cards)
                {
                    void AddMediaToZip(string? relativeFileName)
                    {
                        if (string.IsNullOrEmpty(relativeFileName)) return;

                        if (relativeFileName.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return;

                        string fullPath = PathHelper.GetFullPath(relativeFileName);

                        if (File.Exists(fullPath))
                        {
                            try
                            {
                                string entryName = Path.Combine("media", Path.GetFileName(relativeFileName));

                                archive.CreateEntryFromFile(fullPath, entryName);
                            }
                            catch
                            {
                            }
                        }
                    }

                    AddMediaToZip(c.FrontImagePath);
                    AddMediaToZip(c.BackImagePath);
                    AddMediaToZip(c.FrontAudioPath);
                    AddMediaToZip(c.BackAudioPath);
                }
            }
        }
    }
}