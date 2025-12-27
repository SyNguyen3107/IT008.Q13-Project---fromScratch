using EasyFlips.Interfaces;
using EasyFlips.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EasyFlips.Repositories
{
    public class CardRepository : ICardRepository
    {
        private readonly AppDbContext _context;
        

        public CardRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Card card)
        {
            // Đảm bảo ID tồn tại
            if (string.IsNullOrEmpty(card.Id))
            {
                card.Id = Guid.NewGuid().ToString();
            }

            if (card.Deck != null && string.IsNullOrEmpty(card.DeckId))
            {
                card.DeckId = card.Deck.Id;
            }

            if (card.CreatedAt == default) card.CreatedAt = DateTime.Now;
            if (card.UpdatedAt == default) card.UpdatedAt = DateTime.Now;

            await _context.Cards.AddAsync(card);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(string id)
        {
            var card = await _context.Cards.FindAsync(id);
            if (card != null)
            {
                _context.Cards.Remove(card);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Card> GetByIdAsync(string id)
        {
            return await _context.Cards
                .Include(c => c.Progress)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<List<Card>> GetCardsByDeckIdAsync(string deckId)
        {
            // [FIX LỖI KẸT CARD]: Dùng AsNoTracking để luôn lấy dữ liệu mới nhất từ DB
            return await _context.Cards
                .AsNoTracking()
                .Where(c => c.DeckId == deckId)
                .Include(c => c.Progress)
                .ToListAsync();
        }

        public async Task UpdateAsync(Card card)
        {
            card.UpdatedAt = DateTime.Now;

            // 1. Cập nhật bản thân Card
            var existingCard = await _context.Cards.FindAsync(card.Id);
            if (existingCard != null)
            {
                _context.Entry(existingCard).CurrentValues.SetValues(card);
            }
            else
            {
                // Trường hợp hiếm: Card chưa có thì add luôn
                await _context.Cards.AddAsync(card);
            }

            // 2. Xử lý CardProgress (Quan hệ 1-1)
            if (card.Progress != null)
            {
                // Tìm xem Progress đã có trong DB chưa dựa vào CardId
                var existingProgress = await _context.CardProgresses
                                             .FirstOrDefaultAsync(p => p.CardId == card.Id);

                if (existingProgress == null)
                {
                    // Chưa có -> Thêm mới (Insert)
                    card.Progress.CardId = card.Id;

                    // [FIX LỖI CRASH QUAN TRỌNG]: Ngắt tham chiếu ngược
                    card.Progress.Card = null;

                    if (string.IsNullOrEmpty(card.Progress.Id))
                        card.Progress.Id = Guid.NewGuid().ToString();

                    await _context.CardProgresses.AddAsync(card.Progress);
                }
                else
                {
                    // Đã có -> Cập nhật (Update)
                    _context.Entry(existingProgress).CurrentValues.SetValues(card.Progress);

                    // Giữ nguyên khóa
                    existingProgress.Id = existingProgress.Id;
                    existingProgress.CardId = card.Id;
                }
            }

            await _context.SaveChangesAsync();
        }

    }
}