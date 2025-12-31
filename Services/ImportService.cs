using EasyFlips.Interfaces;
using EasyFlips.Models;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using EasyFlips.Helpers;
using System;
using System.Threading.Tasks;

namespace EasyFlips.Services
{
    public class ImportService
    {
        private readonly IDeckRepository _deckRepository;
        private readonly ICardRepository _cardRepository;
        private readonly IAuthService _authService;

        public ImportService(IDeckRepository deckRepository, ICardRepository cardRepository, IAuthService authService)
        {
            _deckRepository = deckRepository;
            _cardRepository = cardRepository;
            _authService = authService;
        }

        public async Task<Deck?> ImportDeckFromZipAsync(string zipPath)
        {
            if (!File.Exists(zipPath)) return null;

            string extractPath = Path.Combine(Path.GetTempPath(), "DeckImport_" + Guid.NewGuid());
            Directory.CreateDirectory(extractPath);

            try
            {
                ZipFile.ExtractToDirectory(zipPath, extractPath);

                string jsonPath = Path.Combine(extractPath, "deck.json");
                if (!File.Exists(jsonPath)) return null;

                string jsonString = await File.ReadAllTextAsync(jsonPath);
                var exportData = JsonSerializer.Deserialize<DeckExportModel>(jsonString);
                if (exportData == null) return null;

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
                    Description = exportData.Description ?? "",
                    UserId = _authService.CurrentUserId
                };
                await _deckRepository.AddAsync(newDeck);

                string extractedMediaFolder = Path.Combine(extractPath, "media");

                string appMediaFolder = PathHelper.GetMediaFolderPath();

                string? ProcessMediaPath(string? mediaName)
                {
                    if (string.IsNullOrEmpty(mediaName)) return null;

                    if (mediaName.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        return mediaName;
                    }

                    string sourceFile = Path.Combine(extractedMediaFolder, mediaName);

                    if (File.Exists(sourceFile))
                    {
                        string destFile = Path.Combine(appMediaFolder, mediaName);

                        if (!File.Exists(destFile))
                        {
                            File.Copy(sourceFile, destFile, true);
                        }

                        return mediaName;
                    }

                    return null;
                }

                if (exportData.Cards != null)
                {
                    foreach (var cardModel in exportData.Cards)
                    {
                        var newCard = new Card
                        {
                            DeckId = newDeck.Id,
                            FrontText = cardModel.FrontText,
                            BackText = cardModel.BackText,
                            Answer = cardModel.Answer ?? "",

                            FrontImagePath = ProcessMediaPath(cardModel.FrontImageName),
                            BackImagePath = ProcessMediaPath(cardModel.BackImageName),
                            FrontAudioPath = ProcessMediaPath(cardModel.FrontAudioName),
                            BackAudioPath = ProcessMediaPath(cardModel.BackAudioName),

                            Progress = null
                        };
                        await _cardRepository.AddAsync(newCard);
                    }
                }

                var reloadedDeck = await _deckRepository.GetByIdAsync(newDeck.Id);
                return reloadedDeck ?? newDeck;
            }
            finally
            {
                if (Directory.Exists(extractPath))
                {
                    try { Directory.Delete(extractPath, true); } catch { }
                }
            }
        }
    }
}