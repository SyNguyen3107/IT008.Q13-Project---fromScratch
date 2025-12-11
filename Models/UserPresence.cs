using Newtonsoft.Json;
using Supabase.Realtime.Models;
using Supabase.Realtime.Presence; // Required for BasePresence

namespace EasyFlips.Models
{
    public class UserPresence : BasePresence
    {
        [JsonProperty("user_id")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("display_name")]
        public string? DisplayName { get; set; }

        // Add other fields as needed (e.g., online_at)
    }
}