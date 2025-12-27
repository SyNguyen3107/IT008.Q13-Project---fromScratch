using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyFlips.Models
{
    public class DashboardStats
    {
        public int MemoryStrength { get; set; }
        public int New { get; set; }
        public int Learning { get; set; }
        public int Review { get; set; }
        public int Streak { get; set; }
        public int DueToday { get; set; }
    }
}
