using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Repositories; // Cần thiết để IDE hiểu DeckRepository nếu lỡ dùng, nhưng nên dùng Interface
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Supabase.Postgrest; // Cần cho Constants.Operator

namespace EasyFlips.Services
{
    public class SyncService
    {
        private readonly IDeckRepository _localDeckRepo;
        private readonly Supabase.Client _supabaseClient;
        private readonly IAuthService _authService;
        private readonly AppDbContext _localContext;

        public SyncService(
            IDeckRepository localDeckRepo,
            SupabaseService supabaseService,
            IAuthService authService,
            AppDbContext localContext)
        {
            _localDeckRepo = localDeckRepo;
            _supabaseClient = supabaseService.Client;
            _authService = authService;
            _localContext = localContext;
        }

        public class SyncPlan
        {
            public List<Deck> ToUpload { get; set; } = new List<Deck>();
            public List<Deck> ToDownload { get; set; } = new List<Deck>();
            public List<Deck> ToUpdateCloud { get; set; } = new List<Deck>();
            public List<Deck> ToUpdateLocal { get; set; } = new List<Deck>();

            public bool IsEmpty => ToUpload.Count == 0 && ToDownload.Count == 0 &&
                                   ToUpdateCloud.Count == 0 && ToUpdateLocal.Count == 0;
        }

        public async Task<SyncPlan> PlanSyncAsync()
        {
            var plan = new SyncPlan();
            var userId = _authService.CurrentUserId;
            if (string.IsNullOrEmpty(userId)) return plan;

            var localDecks = await _localContext.Decks
                .Where(d => d.UserId == userId)
                .AsNoTracking()
                .ToListAsync();

            var cloudResponse = await _supabaseClient
                .From<Deck>()
                .Select("id, updated_at, name, description")
                .Get();
            var cloudDecks = cloudResponse.Models;

            foreach (var lDeck in localDecks)
            {
                // [FIX]: So sánh ID không phân biệt hoa thường để tránh lỗi duplicate
                var cDeck = cloudDecks.FirstOrDefault(c => string.Equals(c.Id, lDeck.Id, StringComparison.OrdinalIgnoreCase));

                if (cDeck == null)
                {
                    plan.ToUpload.Add(lDeck);
                }
                else
                {
                    if (lDeck.UpdatedAt > cDeck.UpdatedAt)
                        plan.ToUpdateCloud.Add(lDeck);
                    else if (cDeck.UpdatedAt > lDeck.UpdatedAt)
                        plan.ToUpdateLocal.Add(cDeck);
                }
            }

            foreach (var cDeck in cloudDecks)
            {
                if (!localDecks.Any(l => string.Equals(l.Id, cDeck.Id, StringComparison.OrdinalIgnoreCase)))
                    plan.ToDownload.Add(cDeck);
            }

            return plan;
        }

        public async Task ExecuteSyncAsync(SyncPlan plan)
        {
            foreach (var deck in plan.ToUpload.Concat(plan.ToUpdateCloud))
            {
                var fullDeck = await _localDeckRepo.GetByIdAsync(deck.Id);
                if (fullDeck != null) await UploadDeckToCloud(fullDeck);
            }

            foreach (var deckHeader in plan.ToDownload.Concat(plan.ToUpdateLocal))
            {
                await DownloadDeckFromCloud(deckHeader.Id);
            }
        }

        private async Task UploadDeckToCloud(Deck deck)
        {
            // Upsert Deck
            // Giờ Model đã có PrimaryKey(true) nên ID sẽ được gửi lên và khớp với Local
            await _supabaseClient.From<Deck>().Upsert(deck);

            if (deck.Cards != null && deck.Cards.Any())
            {
                var cardsToUpload = deck.Cards.Select(c => {
                    c.Deck = null;
                    c.Progress = null;
                    return c;
                }).ToList();

                await _supabaseClient.From<Card>().Upsert(cardsToUpload);

                var progresses = deck.Cards
                    .Where(c => c.Progress != null)
                    .Select(c => {
                        var p = c.Progress;
                        p.Card = null;
                        return p;
                    })
                    .ToList();

                if (progresses.Any())
                {
                    await _supabaseClient.From<CardProgress>().Upsert(progresses);
                }
            }
        }

        private async Task DownloadDeckFromCloud(string deckId)
        {
            var userId = _authService.CurrentUserId;

            var deck = await _supabaseClient
                .From<Deck>()
                .Where(d => d.Id == deckId)
                .Single();

            if (deck == null) return;

            var cardsResponse = await _supabaseClient
                .From<Card>()
                .Where(c => c.DeckId == deckId)
                .Get();
            var cards = cardsResponse.Models;

            if (cards.Any() && !string.IsNullOrEmpty(userId))
            {
                var cardIds = cards.Select(c => c.Id).ToList();

                var progressResponse = await _supabaseClient
                    .From<CardProgress>()
                    .Filter("card_id", Constants.Operator.In, cardIds)
                    .Filter("user_id", Constants.Operator.Equals, userId)
                    .Get();

                var cloudProgresses = progressResponse.Models;

                foreach (var card in cards)
                {
                    var p = cloudProgresses.FirstOrDefault(cp => cp.CardId == card.Id);
                    if (p != null) card.Progress = p;
                }
            }

            var existingDeck = await _localDeckRepo.GetByIdAsync(deckId);
            if (existingDeck != null)
            {
                await _localDeckRepo.DeleteAsync(deckId);
            }

            deck.Cards = cards;
            deck.UserId = userId;

            await _localDeckRepo.AddAsync(deck);
        }
    }
}