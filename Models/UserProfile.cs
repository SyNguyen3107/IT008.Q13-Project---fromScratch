using Newtonsoft.Json;
using Supabase.Postgrest.Models; // thêm namespace này

namespace EasyFlips.Models
{
    public class UserProfile : BaseModel
    {
        [JsonProperty("user_id")]
        public string UserId { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }
        [JsonProperty("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public DateTime? UpdatedAt { get; set; }

    }
}
