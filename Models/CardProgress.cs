namespace EasyFlips.Models
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
            DueDate = DateTime.Now; // Học ngay lập tức
            Interval = 0; // Ban đầu = 0, sẽ được set khi chọn option lần đầu
            EaseFactor = 2.5; // Giá trị mặc định của Anki
        }
    }
}