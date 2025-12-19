using CommunityToolkit.Mvvm.ComponentModel;

namespace EasyFlips.Models
{
    public partial class PlayerInfo : ObservableObject
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string AvatarUrl { get; set; }
        public bool IsHost { get; set; }

        public byte Score { get; set; }

    }
}