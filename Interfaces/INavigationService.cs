using EasyFlips.Models;

namespace EasyFlips.Interfaces
{
    public interface INavigationService
    {
        // Mở cửa sổ học (cần biết học deck nào)
        void ShowStudyWindow(string deckId);

        // Mở cửa sổ tạo deck
        void ShowCreateDeckWindow();
        void ShowCardWindow();
        void ShowSyncWindow();
        void ShowAddCardWindow();
        void ImportFileWindow();
        void ShowDeckChosenWindow(string deckId);
        void ShowDeckRenameWindow(Deck deck);
        void ShowLoginWindow();
        void ShowRegisterWindow();
        void ShowMainWindow();

    }
}
