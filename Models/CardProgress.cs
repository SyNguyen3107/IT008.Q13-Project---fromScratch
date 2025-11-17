using System;

namespace IT008.Q13_Project___fromScratch.Models
{
    public class CardProgress
    {
        public int ID { get; private set; }
        public int CardId { get; set; } // Foreign key

        //Thông tin ôn tập
        public DateTime DueDate { get; set; }
        public double Interval { get; set; }    // Tính bằng ngày
        public double EaseFactor { get; set; }  // Thường bắt đầu từ 2.5 (250%)

        // Quan hệ 1-1 ngược lại với Card
        public Card Card { get; set; }

        // Constructor để set giá trị mặc định khi thẻ mới được tạo
        public CardProgress()
        {
            DueDate = DateTime.UtcNow; // Học ngay lập tức
            Interval = 0;
            EaseFactor = 2.5; // Giá trị mặc định của Anki
        }
    }
}