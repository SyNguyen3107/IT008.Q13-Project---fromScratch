using EasyFlips.Models;

namespace EasyFlips.Interfaces
{
    public interface INavigationService
    {
        void ShowStudyWindow(string deckId);
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
        void OpenSyncWindow();
        void ShowResetPasswordWindow();
        void ShowOtpWindow(string email);

    }
}
