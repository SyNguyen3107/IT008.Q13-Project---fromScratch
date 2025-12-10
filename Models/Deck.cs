using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Postgrest = Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace EasyFlips.Models
{
    [Postgrest.Table("decks")]
    public class Deck : BaseModel
    {
        [Key]
        // true để gửi ID local lên server (đồng bộ ID)
        [Postgrest.PrimaryKey("id", true)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Postgrest.Column("name")]
        public string Name { get; set; }

        [Postgrest.Column("description")]
        public string? Description { get; set; }

        [Postgrest.Column("user_id")]
        public string? UserId { get; set; }

        [Postgrest.Column("last_synced_at")]
        public DateTime? LastSyncedAt { get; set; }

        [Postgrest.Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Postgrest.Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [Postgrest.Reference(typeof(Card))]
        [JsonIgnore]
        public virtual ICollection<Card> Cards { get; set; } = new List<Card>();

        [NotMapped]
        [JsonIgnore]
        public int NewCount { get; set; }

        [NotMapped]
        [JsonIgnore]
        public int LearnCount { get; set; }

        [NotMapped]
        [JsonIgnore]
        public int DueCount { get; set; }
    }
}