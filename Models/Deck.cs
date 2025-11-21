using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace IT008.Q13_Project___fromScratch.Models
{
    public class Deck
    {
        public int ID { get; private set; }
        public string Name { get; set; } = " ";
        public string Description { get; set; } = " ";
        public List<Card> Cards { get; set; } = new List<Card>();
        public int NewCount { get; set; } // Số thẻ mới
        public int LearnCount { get; set; } // Số thẻ đang học
        public int DueCount { get; set; } // Số thẻ đến hạn ôn tập
        //3 thuộc tính trên dùng để hiển thị trong MainAnkiWindow,
        //còn 3 thuộc trong DeckStats dùng để hiển thị trong ChosenDeckWindow
        //Dữ liệu binding: ListView trong MainAnkiWindow đang bind ItemsSource = "{Binding Decks}"
        ////Điều này có nghĩa là mỗi dòng(row) trong ListView đang bind tới một đối tượng Deck.
        //Kết nối: Để cột "New" hiển thị được, TextBlock trong GridViewColumn phải bind tới một
        //thuộc tính có sẵn trong đối tượng Deck của dòng đó.
    }
}
