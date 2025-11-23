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

        //Câu trả lời cho card này
        public string Answer { get; set; } = string.Empty;

        //Nội dung mặt trước
        public string FrontText { get; set; }
        public string? FrontImagePath { get; set; } = null;
        public string? FrontAudioPath { get; set; } = null;
         
        //Nội dung mặt sau

        public string BackText { get; set; }
        public string? BackImagePath { get; set; } = null;
        public string? BackAudioPath { get; set; } = null;
        
        //Mỗi card chỉ nằm trong 1 Deck
        public Deck Deck { get; set; }

        //lưu các thông tin về tiến trình học tập của thẻ
        public CardProgress Progress { get; set; }
    }
}
