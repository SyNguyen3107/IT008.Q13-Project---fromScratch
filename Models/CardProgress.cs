using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Postgrest = Supabase.Postgrest.Attributes;

namespace EasyFlips.Models
{
    [Postgrest.Table("card_progresses")]
    public class CardProgress
    {
        [Key]
        [Postgrest.PrimaryKey("id", false)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Postgrest.Column("card_id")]
        public string CardId { get; set; }

        // [QUAN TRỌNG - MỚI THÊM]: Cần thiết để Sync đúng tiến độ của user hiện tại
        [Postgrest.Column("user_id")]
        public string UserId { get; set; }

        // --- Thông tin ôn tập (SM2 Algorithm) ---

        [Postgrest.Column("due_date")]
        public DateTime DueDate { get; set; }

        [Postgrest.Column("interval")]
        public double Interval { get; set; }

        [Postgrest.Column("ease_factor")]
        public double EaseFactor { get; set; }

        [Postgrest.Column("repetitions")]
        public int Repetitions { get; set; }

        [Postgrest.Column("last_review_date")]
        public DateTime LastReviewDate { get; set; }

        [Postgrest.Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Postgrest.Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Quan hệ 1-1 ngược lại với Card (EF Core)
        [ForeignKey(nameof(CardId))]
        public virtual Card? Card { get; set; }

        // Constructor
        public CardProgress()
        {
            DueDate = DateTime.Now;
            LastReviewDate = DateTime.Now;
            Interval = 0;
            EaseFactor = 2.5;
            Repetitions = 0;
        }
    }
}