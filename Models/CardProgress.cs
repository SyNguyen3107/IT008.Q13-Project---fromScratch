using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json; // Cần thêm
using Supabase.Postgrest.Models;
using Postgrest = Supabase.Postgrest.Attributes;

namespace EasyFlips.Models
{
    [Postgrest.Table("card_progresses")]
    public class CardProgress : BaseModel
    {
        [Key]
        // true để gửi ID local lên server (đồng bộ ID)
        [Postgrest.PrimaryKey("id", true)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Postgrest.Column("card_id")]
        public string CardId { get; set; }

        [Postgrest.Column("user_id")]
        public string UserId { get; set; }

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

        [ForeignKey(nameof(CardId))]
        [JsonIgnore] // [FIX]: Bỏ qua khi sync để tránh lỗi vòng lặp
        public virtual Card? Card { get; set; }

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