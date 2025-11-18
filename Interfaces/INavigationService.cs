using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT008.Q13_Project___fromScratch.Interfaces
{
    public interface INavigationService
    {
        // Mở cửa sổ học (cần biết học deck nào)
        void ShowStudyWindow(int deckId);

        // Mở cửa sổ tạo deck
        void ShowCreateDeckWindow();
        void ShowCardWindow();
        void ShowSyncWindow();
        void ShowAddCardWindow();
        void ImportFileWindow();

    }
}
