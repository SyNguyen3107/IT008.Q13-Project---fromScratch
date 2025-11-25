namespace EasyFlips.Models
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
        public string Answer { get; set; }

        public string? FrontImageName { get; set; }
        public string? BackImageName { get; set; }
        public string? FrontAudioName { get; set; }
        public string? BackAudioName { get; set; }


    }
}