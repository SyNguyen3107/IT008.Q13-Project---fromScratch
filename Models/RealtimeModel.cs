using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Supabase.Realtime.Models;

namespace EasyFlips.Models
{
    // Cấu trúc tin nhắn BẤT TỬ. Không bao giờ thay đổi.
    public class SignalMessage : BaseBroadcast
    {
        [JsonProperty("action")]
        public string Action { get; set; } // Ví dụ: "JOIN", "LEFT", "UPDATE_LOBBY"

        [JsonProperty("payload")]
        public JObject Payload { get; set; } // Dữ liệu đi kèm (JSON)
    }
}