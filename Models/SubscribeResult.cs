using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyFlips.Models
{
    public class SubscribeResult
    {
        public string ChannelName { get; set; } = string.Empty;
        public bool Success { get; set; } = false;
        public string? ErrorMessage { get; set; }
    }
}

