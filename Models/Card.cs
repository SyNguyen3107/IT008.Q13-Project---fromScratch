using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Postgrest = Supabase.Postgrest.Attributes;

namespace EasyFlips.Models
{
    [Postgrest.Table("cards")]
    public class Card
    {
        [Key]
        [Postgrest.PrimaryKey("id", false)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Khóa ngoại trỏ về Deck
        [Postgrest.Column("deck_id")]
        public string DeckId { get; set; }

        // Câu trả lời
        [Postgrest.Column("answer")]
        public string Answer { get; set; } = string.Empty;

        // Nội dung mặt trước
        [Postgrest.Column("front_text")]
        public string FrontText { get; set; }

        [Postgrest.Column("front_image_path")]
        public string? FrontImagePath { get; set; } = null;

        [Postgrest.Column("front_audio_path")]
        public string? FrontAudioPath { get; set; } = null;

        // Nội dung mặt sau
        [Postgrest.Column("back_text")]
        public string BackText { get; set; }

        [Postgrest.Column("back_image_path")]
        public string? BackImagePath { get; set; } = null;

        [Postgrest.Column("back_audio_path")]
        public string? BackAudioPath { get; set; } = null;

        [Postgrest.Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Postgrest.Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // EF Core Relations
        public Deck Deck { get; set; }

        // Supabase Relation (1-1 is tricky in Postgrest, usually handled manually)
        public CardProgress Progress { get; set; }
    }
}