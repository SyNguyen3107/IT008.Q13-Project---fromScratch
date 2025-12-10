using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyFlips.Models
{
    public class Deck
    {
        // ID này sẽ giống hệt ID trên Supabase, không cần map qua lại.
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Name { get; set; }
        public string? Description { get; set; }

        public string? UserId { get; set; }

        public DateTime? LastSyncedAt { get; set; }

        public virtual ICollection<Card> Cards { get; set; } = new List<Card>();

        // Các thuộc tính thống kê (Không lưu vào DB)
        [NotMapped]
        public int NewCount { get; set; }

        [NotMapped]
        public int LearnCount { get; set; }

        [NotMapped]
        public int DueCount { get; set; }
    }
}