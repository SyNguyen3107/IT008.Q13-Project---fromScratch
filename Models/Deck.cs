using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
// Sử dụng alias để tránh xung đột tên với DataAnnotations
using Postgrest = Supabase.Postgrest.Attributes;

namespace EasyFlips.Models
{
    // Attribute cho Supabase
    [Postgrest.Table("decks")]
    public class Deck
    {
        // ID này sẽ giống hệt ID trên Supabase
        [Key]
        [Postgrest.PrimaryKey("id", false)] // false nghĩa là ID không được sinh bởi DB (ta tự sinh UUID)
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Postgrest.Column("name")]
        public string Name { get; set; }

        [Postgrest.Column("description")]
        public string? Description { get; set; }

        [Postgrest.Column("user_id")]
        public string? UserId { get; set; }

        [Postgrest.Column("last_synced_at")]
        public DateTime? LastSyncedAt { get; set; }

        // [QUAN TRỌNG]: CreatedAt/UpdatedAt được Supabase tự quản lý, 
        // nhưng ta có thể map về để đọc nếu cần.
        [Postgrest.Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Postgrest.Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Quan hệ 1-N (EF Core) - Supabase thường không map trực tiếp list này qua API mà dùng query riêng
        [Postgrest.Reference(typeof(Card))]
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