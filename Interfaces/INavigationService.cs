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
        void ShowJoinWindow();
        void ShowCreateRoomWindow();
        Task ShowCreateRoomWindowAsync();
        Task ShowHostLobbyWindowAsync(string roomId);
        Task ShowMemberLobbyWindowAsync(string roomId);
        Task ShowHostGameWindowAsync(string roomId, string classroomId, Deck deck, int timePerRound);
        Task ShowMemberGameWindowAsync(string roomId, string classroomId, Deck deck, int timePerRound);
        void ShowHostLeaderboardWindow(string roomId, string classroomId, List<PlayerInfo> finalResults);
        void ShowMemberLeaderboardWindow(string roomId, string classroomId);
        void CloseCurrentWindow();
        void NavigateToDashboard();
        public void NavigateToHome();

    }
}
