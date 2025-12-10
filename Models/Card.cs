using System.ComponentModel.DataAnnotations;

namespace EasyFlips.Models
{
    public class Card
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Khóa ngoại trỏ về Deck cũng phải đổi sang string
        public string DeckId { get; set; }

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
