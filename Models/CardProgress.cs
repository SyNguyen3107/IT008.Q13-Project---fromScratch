using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyFlips.Models
{
    public class CardProgress
    {
        // [QUAN TRỌNG]: Primary Key là String (UUID) & Tự sinh ID
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // [QUAN TRỌNG]: Foreign Key sang Card cũng là String
        public string CardId { get; set; }

        // --- Thông tin ôn tập (SM2 Algorithm Parameters) ---

        public DateTime DueDate { get; set; }

        public double Interval { get; set; }    // Khoảng cách ngày giữa các lần ôn

        public double EaseFactor { get; set; }  // Độ khó (Mặc định 2.5)

        // [THÊM MỚI]: Số lần ôn tập liên tiếp (Cần thiết cho thuật toán)
        public int Repetitions { get; set; }

        // [THÊM MỚI]: Ngày ôn tập gần nhất (Để tính toán trôi dạt thời gian)
        public DateTime LastReviewDate { get; set; }

        // Quan hệ 1-1 ngược lại với Card
        [ForeignKey(nameof(CardId))]
        public virtual Card? Card { get; set; }

        // Constructor mặc định
        public CardProgress()
        {
            DueDate = DateTime.Now; // Học ngay lập tức
            LastReviewDate = DateTime.Now;
            Interval = 0;
            EaseFactor = 2.5; // Giá trị mặc định chuẩn của Anki/SM2
            Repetitions = 0;
        }
    }
}