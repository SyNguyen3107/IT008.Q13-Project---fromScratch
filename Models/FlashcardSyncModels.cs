using Newtonsoft.Json;
using Supabase.Realtime.Models;
using Supabase.Realtime.Presence;

namespace EasyFlips.Models
{
    public enum FlashcardAction
    {
        ShowCard,
        FlipCard,
        NextCard,
        PreviousCard,
        StartSession,
        EndSession,
        PauseSession,
        ResumeSession
    }

    public class FlashcardSyncState
    {
        [JsonProperty("classroom_id")]
        public string ClassroomId { get; set; } = string.Empty;

        [JsonProperty("deck_id")]
        public string DeckId { get; set; } = string.Empty;

        [JsonProperty("current_card_id")]
        public string CurrentCardId { get; set; } = string.Empty;

        [JsonProperty("current_card_index")]
        public int CurrentCardIndex { get; set; }

        [JsonProperty("total_cards")]
        public int TotalCards { get; set; }

        [JsonProperty("is_flipped")]
        public bool IsFlipped { get; set; }

        [JsonProperty("action")]
        public FlashcardAction Action { get; set; }

        [JsonProperty("triggered_by")]
        public string TriggeredBy { get; set; } = string.Empty;

        [JsonProperty("time_remaining")]
        public int TimeRemaining { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [JsonProperty("is_session_active")]
        public bool IsSessionActive { get; set; }

        [JsonProperty("is_paused")]
        public bool IsPaused { get; set; }
    }

    public class FlashcardBroadcastPayload
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "flashcard_sync";

        [JsonProperty("state")]
        public FlashcardSyncState State { get; set; } = new();
    }

    public class FlashcardPresence : BasePresence
    {
        [JsonProperty("user_id")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("current_card_index")]
        public int CurrentCardIndex { get; set; }

        [JsonProperty("is_ready")]
        public bool IsReady { get; set; }

        [JsonProperty("joined_at")]
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }

    public class FlashcardSyncEventArgs : EventArgs
    {
        public FlashcardSyncState State { get; set; } = new();
        public bool IsFromHost { get; set; }
    }
}

