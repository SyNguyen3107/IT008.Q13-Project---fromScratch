using System.Collections.Generic;

namespace IT008.Q13_Project___fromScratch.Models
{
    // Class này đại diện cho cấu trúc file sẽ được lưu
    public class DeckExportModel
    {
        public string DeckName { get; set; }
        public string Description { get; set; }
        public List<CardExportModel> Cards { get; set; } = new List<CardExportModel>();
    }

    public class CardExportModel
    {
        public string FrontText { get; set; }
        public string BackText { get; set; }

        //Để xuát file hình ảnh và âm thanh, ta không thể xuất các đường dẫn local
        //trên máy mà phải nén vào 1 file zip và gửi cùng, vì vậy hiện tại chỉ xuất text

        //update: TẠM THỜI dùng đường dẫn file từ internet thay vì đường dẫn local
        public string Answer { get; set; }

        public string? FrontImagePath { get; set; }
        public string? BackImagePath { get; set; }
    }
}