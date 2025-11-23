using CommunityToolkit.Mvvm.Messaging.Messages;

namespace IT008.Q13_Project___fromScratch.Messages
{
    // Tin nhắn báo hiệu một buổi học đã kết thúc (để cập nhật thống kê)
    public class StudySessionCompletedMessage : ValueChangedMessage<int>
    {
        // Giá trị truyền đi là DeckID vừa học xong
        public StudySessionCompletedMessage(int deckId) : base(deckId)
        {
        }
    }
}