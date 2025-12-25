using CommunityToolkit.Mvvm.ComponentModel;
using EasyFlips.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyFlips.ViewModels
{
    public class LeaderBoardViewModel : ObservableObject
    {
        public string RoomId { get; private set; }
        public string ClassroomId { get; private set; }
        public ObservableCollection<PlayerInfo> Players { get; private set; } = new();

        public void Initialize(string roomId, string classroomId, IEnumerable<PlayerInfo> players)
        {
            RoomId = roomId;
            ClassroomId = classroomId;
            Players = new ObservableCollection<PlayerInfo>(players);
        }
    }


}
