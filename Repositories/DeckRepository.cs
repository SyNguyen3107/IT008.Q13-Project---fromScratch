using EasyFlips.Interfaces;
using EasyFlips.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EasyFlips.Repositories
{
    public class DeckRepository : IDeckRepository
    {
        private readonly AppDbContext _context;
        private readonly IAuthService _authService;

        public DeckRepository(AppDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        public async Task AddAsync(Deck deck)
        {
            if (_authService?.IsLoggedIn == true && !string.IsNullOrEmpty(_authService.CurrentUserId))
            {
                deck.UserId = _authService.CurrentUserId;
            }

            // [FIX]: Đảm bảo ID luôn là chuỗi UUID hợp lệ
            if (string.IsNullOrEmpty(deck.Id))
            {
                deck.Id = Guid.NewGuid().ToString();
            }

            // Đặt thời gian tạo/sync nếu chưa có (tùy chọn)
            if (deck.LastSyncedAt == null)
            {
                // Có thể để null hoặc set DateTime.Now tùy logic sync
            }

            await _context.Decks.AddAsync(deck);
            await _context.SaveChangesAsync();
        }

        public async Task ClaimData()
        {
            if (_authService?.IsLoggedIn == true)
            {
                var uid = _authService.CurrentUserId;

                // Lấy các deck chưa có chủ sở hữu (UserId is null)
                var anonymousDecks = await _context.Decks
                    .Where(d => d.UserId == null)
                    .ToListAsync();

                foreach (var deck in anonymousDecks)
                {
                    deck.UserId = uid;
                }

                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<Deck>> GetAllAsync()
        {
            var query = _context.Decks
                .Include(d => d.Cards)
                .ThenInclude(c => c.Progress)
                .AsQueryable();

            if (_authService?.IsLoggedIn == true && !string.IsNullOrEmpty(_authService.CurrentUserId))
            {
                var uid = _authService.CurrentUserId;
                query = query.Where(d => d.UserId == uid);
            }
            else
            {
                query = query.Where(d => d.UserId == null);
            }

            var decks = await query.ToListAsync();

            foreach (var deck in decks)
            {
                CalculateStats(deck);
            }

            return decks;
        }

        public async Task<Deck?> GetByIdAsync(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            var deck = await _context.Decks
                .Include(d => d.Cards)
                .ThenInclude(c => c.Progress)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (deck != null)
            {
                CalculateStats(deck);
            }

            return deck;
        }

        public async Task UpdateAsync(Deck deck)
        {
            var existingDeck = await _context.Decks.FindAsync(deck.Id);
            if (existingDeck != null)
            {
                _context.Entry(existingDeck).CurrentValues.SetValues(deck);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteAsync(string id)
        {
            var deck = await _context.Decks.FindAsync(id);
            if (deck != null)
            {
                _context.Decks.Remove(deck);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Deck?> GetByNameAsync(string name)
        {
            var query = _context.Decks.AsQueryable();

            if (_authService?.IsLoggedIn == true && !string.IsNullOrEmpty(_authService.CurrentUserId))
            {
                query = query.Where(d => d.UserId == _authService.CurrentUserId);
            }
            else
            {
                query = query.Where(d => d.UserId == null);
            }

            return await query.FirstOrDefaultAsync(d => d.Name == name);
        }

        // Phương thức bổ sung cho Interface nếu cần
        public async Task<Deck?> GetDeckWithCardsAsync(string deckId)
        {
            return await GetByIdAsync(deckId);
        }

        private void CalculateStats(Deck deck)
        {
            if (deck.Cards == null) return;

            var now = DateTime.Now;
            deck.NewCount = deck.Cards.Count(c => c.Progress == null);

            deck.LearnCount = deck.Cards.Count(c => c.Progress != null &&
                                                    c.Progress.Interval > 0 &&
                                                    c.Progress.Interval < 1);

            deck.DueCount = deck.Cards.Count(c => c.Progress != null &&
                                                   c.Progress.Interval >= 1 &&
                                                   c.Progress.DueDate <= now);
        }
    }
}