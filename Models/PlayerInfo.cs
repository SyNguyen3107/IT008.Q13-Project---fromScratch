using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using Supabase.Realtime.Models;
using System.Collections.Generic;

namespace EasyFlips.Models
{
    public partial class PlayerInfo : ObservableObject
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string AvatarUrl { get; set; }
        public bool IsHost { get; set; }

        // Trường Score cũ của bạn (giữ nguyên)
        [ObservableProperty]
        private int score;

        // [MỚI] Thứ hạng (1, 2, 3...)
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RankColor))] // Cập nhật màu khi Rank thay đổi
        private int _rank;

        // [MỚI] Helper để lấy màu cho UI (Vàng, Bạc, Đồng, Trắng)
        public string RankColor => Rank switch
        {
            1 => "#FFD700", // Gold
            2 => "#C0C0C0", // Silver
            3 => "#CD7F32", // Bronze
            _ => "#FFFFFF"  // White
        };
    }
    public enum GameControlSignal
    {
        None,
        ReturnToLobby,
        CloseRoom
    }

    public class GameControlPayload : BaseBroadcast
    {
        [JsonProperty("signal")]
        public GameControlSignal Signal { get; set; }
    }
}