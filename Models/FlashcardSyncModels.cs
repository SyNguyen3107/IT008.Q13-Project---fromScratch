using Newtonsoft.Json;
using Supabase.Realtime.Models;
using Supabase.Realtime.Presence;

namespace EasyFlips.Models
{
    #region Enums

    /// <summary>
    /// Các hành ??ng ??ng b? flashcard qua Realtime.
    /// </summary>
    public enum FlashcardAction
    {
        None = 0,
        ShowCard,
        FlipCard,
        NextCard,
        PreviousCard,
        StartSession,
        EndSession,
        PauseSession,
        ResumeSession,
        SyncState  // G?i tr?ng thái hi?n t?i cho Member vào tr?
    }

    /// <summary>
    /// Pha hi?n t?i c?a game loop.
    /// </summary>
    public enum GamePhase
    {
        Waiting,    // ?ang ch? b?t ??u
        Countdown,  // ??m ng??c 3-2-1
        Question,   // Hi?n th? câu h?i, ng??i ch?i tr? l?i
        Result,     // Hi?n th? ?áp án, xem k?t qu?
        Finished    // K?t thúc phiên
    }

    #endregion

    #region Sync Models

    /// <summary>
    /// Tr?ng thái ??ng b? flashcard - broadcast qua Realtime.
    /// </summary>
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

        [JsonProperty("phase")]
        public GamePhase Phase { get; set; } = GamePhase.Waiting;
    }

    /// <summary>
    /// Payload broadcast flashcard sync.
    /// </summary>
    public class FlashcardBroadcastPayload
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "flashcard_sync";

        [JsonProperty("state")]
        public FlashcardSyncState State { get; set; } = new();
    }

    #endregion

    #region Presence Models

    /// <summary>
    /// Presence cho flashcard session - theo dõi ai ?ang online.
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

    #endregion

    #region Score Models

    /// <summary>
    /// ?i?m s? g?i t? Member lên Host.
    /// </summary>
    public class ScoreSubmission
    {
        [JsonProperty("user_id")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("card_index")]
        public int CardIndex { get; set; }

        [JsonProperty("answer")]
        public string Answer { get; set; } = string.Empty;

        [JsonProperty("is_correct")]
        public bool IsCorrect { get; set; }

        [JsonProperty("score")]
        public int Score { get; set; }

        [JsonProperty("time_taken_ms")]
        public long TimeTakenMs { get; set; }

        [JsonProperty("submitted_at")]
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Entry trong b?ng x?p h?ng.
    /// </summary>
    public class LeaderboardEntry
    {
        [JsonProperty("user_id")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonProperty("total_score")]
        public int TotalScore { get; set; }

        [JsonProperty("correct_count")]
        public int CorrectCount { get; set; }

        [JsonProperty("total_answered")]
        public int TotalAnswered { get; set; }

        [JsonProperty("rank")]
        public int Rank { get; set; }
    }

    #endregion

    #region Event Args

    /// <summary>
    /// EventArgs khi nh?n ???c s? ki?n ??ng b? flashcard.
    /// </summary>
    public class FlashcardSyncEventArgs : EventArgs
    {
        public FlashcardSyncState State { get; set; } = new();
        public bool IsFromHost { get; set; }
    }

    /// <summary>
    /// EventArgs khi nh?n ???c ?i?m t? Member.
    /// </summary>
    public class ScoreReceivedEventArgs : EventArgs
    {
        public ScoreSubmission Score { get; set; } = new();
    }

    #endregion

    #region Channel Subscription Result

    /// <summary>
    /// K?t qu? khi subscribe vào channel.
    /// </summary>
    public class ChannelSubscriptionResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string ChannelName { get; set; } = string.Empty;
    }

    #endregion
}

