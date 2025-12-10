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

            // Nếu DeckId chưa có, gán nó (phòng hờ)
            if (card.Deck != null && string.IsNullOrEmpty(card.DeckId))
            {
                card.DeckId = card.Deck.Id;
            }

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
            return await _context.Cards
                .Where(c => c.DeckId == deckId)
                .Include(c => c.Progress)
                .ToListAsync();
        }

        public async Task UpdateAsync(Card card)
        {
            // 1. Cập nhật bản thân Card
            var existingCard = await _context.Cards.FindAsync(card.Id);
            if (existingCard != null)
            {
                // Copy giá trị từ card truyền vào sang card trong DB để tránh lỗi Tracking
                _context.Entry(existingCard).CurrentValues.SetValues(card);
            }
            else
            {
                // Trường hợp hiếm: Card chưa có thì add luôn
                _context.Cards.Add(card);
            }

            // 2. Xử lý CardProgress (Quan hệ 1-1)
            if (card.Progress != null)
            {
                // [FIX LỖI QUAN TRỌNG]: Không check ID == "0" nữa.
                // Tìm Progress hiện tại trong DB dựa trên CardId
                var existingProgress = await _context.CardProgresses
                                             .FirstOrDefaultAsync(p => p.CardId == card.Id);

                if (existingProgress == null)
                {
                    // Chưa có trong DB -> Thêm mới (Insert)

                    // Ràng buộc khóa ngoại
                    card.Progress.CardId = card.Id;

                    // Nếu ID chưa có thì sinh mới
                    if (string.IsNullOrEmpty(card.Progress.Id))
                        card.Progress.Id = Guid.NewGuid().ToString();

                    await _context.CardProgresses.AddAsync(card.Progress);
                }
                else
                {
                    // Đã có trong DB -> Cập nhật (Update)
                    // Dùng SetValues để cập nhật các trường (Interval, EaseFactor,...)
                    _context.Entry(existingProgress).CurrentValues.SetValues(card.Progress);

                    // Đảm bảo ID và CardId không bị thay đổi sai lệch
                    existingProgress.Id = existingProgress.Id;
                    existingProgress.CardId = card.Id;
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}