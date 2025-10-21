using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT008.Q13_Project___fromScratch.Models
{
    public class Card
    {
        public int ID { get; private set; }
        public int DeckId { get; set; }

        //Nội dung mặt trước
        public string FrontText { get; set; }
        public string? FrontImagePath { get; set; } = null;
        public string? FrontAudioPath { get; set; } = null;
         
        //Nội dung mặt sau

        public string BackText { get; set; }
        public string? BackImagePath { get; set; } = null;
        public string? BackAudioPath { get; set; } = null;

        //Thông tin ôn tập (giữ nguyên)
        public DateTime DueDate { get; set; }
        public double Interval { get; set; }
        public double EaseFactor { get; set; }

        //Mỗi card chỉ nằm trong 1 Deck thui
        public Deck Deck { get; set; }



    }
}
