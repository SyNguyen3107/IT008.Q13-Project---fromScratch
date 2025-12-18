using Newtonsoft.Json;
using Supabase.Realtime.Models;
using Supabase.Realtime.Presence;

namespace EasyFlips.Models
{
    /// <summary>
    /// Enum ??nh ngh?a các lo?i hành ??ng ??ng b? flashcard.
    /// </summary>
    public enum FlashcardAction
    {
        /// <summary>Hi?n th? card (m?t tr??c).</summary>
        ShowCard,
        /// <summary>L?t card (hi?n th? m?t sau).</summary>
        FlipCard,
        /// <summary>Chuy?n sang card ti?p theo.</summary>
        NextCard,
        /// <summary>Quay l?i card tr??c ?ó.</summary>
        PreviousCard,
        /// <summary>B?t ??u phiên h?c.</summary>
        StartSession,
        /// <summary>K?t thúc phiên h?c.</summary>
        EndSession,
        /// <summary>T?m d?ng phiên h?c.</summary>
        PauseSession,
        /// <summary>Ti?p t?c phiên h?c.</summary>
        ResumeSession
    }

    /// <summary>
    /// Model ch?a tr?ng thái hi?n t?i c?a flashcard session.
    /// ???c broadcast qua Realtime ?? ??ng b? gi?a các client.
    /// </summary>
    public class FlashcardSyncState
    {
        /// <summary>ID c?a phòng h?c.</summary>
        [JsonProperty("classroom_id")]
        public string ClassroomId { get; set; } = string.Empty;

        /// <summary>ID c?a b? th? (Deck) ?ang h?c.</summary>
        [JsonProperty("deck_id")]
        public string DeckId { get; set; } = string.Empty;

        /// <summary>ID c?a card hi?n t?i ?ang hi?n th?.</summary>
        [JsonProperty("current_card_id")]
        public string CurrentCardId { get; set; } = string.Empty;

        /// <summary>V? trí (index) c?a card trong danh sách.</summary>
        [JsonProperty("current_card_index")]
        public int CurrentCardIndex { get; set; }

        /// <summary>T?ng s? card trong b? th?.</summary>
        [JsonProperty("total_cards")]
        public int TotalCards { get; set; }

        /// <summary>Card ?ang ? tr?ng thái l?t (hi?n m?t sau) hay ch?a.</summary>
        [JsonProperty("is_flipped")]
        public bool IsFlipped { get; set; }

        /// <summary>Hành ??ng v?a th?c hi?n.</summary>
        [JsonProperty("action")]
        public FlashcardAction Action { get; set; }

        /// <summary>ID c?a Host ?ã g?i l?nh.</summary>
        [JsonProperty("triggered_by")]
        public string TriggeredBy { get; set; } = string.Empty;

        /// <summary>Th?i gian còn l?i (giây) tr??c khi t? ??ng chuy?n card.</summary>
        [JsonProperty("time_remaining")]
        public int TimeRemaining { get; set; }

        /// <summary>Timestamp c?a s? ki?n (UTC).</summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Phiên h?c ?ang ho?t ??ng hay không.</summary>
        [JsonProperty("is_session_active")]
        public bool IsSessionActive { get; set; }

        /// <summary>Phiên h?c ?ang t?m d?ng hay không.</summary>
        [JsonProperty("is_paused")]
        public bool IsPaused { get; set; }
    }

    /// <summary>
    /// Payload g?i khi Host th?c hi?n hành ??ng trên flashcard.
    /// </summary>
    public class FlashcardBroadcastPayload
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "flashcard_sync";

        [JsonProperty("state")]
        public FlashcardSyncState State { get; set; } = new();
    }

    /// <summary>
    /// Model Presence cho flashcard session - theo dõi ai ?ang xem card nào.
    /// </summary>
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

    /// <summary>
    /// K?t qu? callback khi nh?n ???c s? ki?n ??ng b? flashcard.
    /// </summary>
    public class FlashcardSyncEventArgs : EventArgs
    {
        public FlashcardSyncState State { get; set; } = new();
        public bool IsFromHost { get; set; }
    }
}

