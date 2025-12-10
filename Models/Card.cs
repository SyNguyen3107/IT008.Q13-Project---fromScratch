using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Supabase.Postgrest.Models;
using Postgrest = Supabase.Postgrest.Attributes;

namespace EasyFlips.Models
{
    [Postgrest.Table("cards")]
    public class Card : BaseModel
    {
        [Key]
        // Đổi thành true để gửi ID local lên server (đồng bộ ID)
        [Postgrest.PrimaryKey("id", true)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Postgrest.Column("deck_id")]
        public string DeckId { get; set; }

        [Postgrest.Column("answer")]
        public string Answer { get; set; } = string.Empty;

        [Postgrest.Column("front_text")]
        public string FrontText { get; set; }

        [Postgrest.Column("front_image_path")]
        public string? FrontImagePath { get; set; } = null;

        [Postgrest.Column("front_audio_path")]
        public string? FrontAudioPath { get; set; } = null;

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

        [JsonIgnore]
        public Deck Deck { get; set; }

        [JsonIgnore]
        public CardProgress Progress { get; set; }
    }
}