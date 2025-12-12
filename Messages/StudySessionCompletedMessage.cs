using CommunityToolkit.Mvvm.Messaging.Messages;

namespace EasyFlips.Messages
{
    // Tin nhắn báo hiệu một buổi học đã kết thúc (để cập nhật thống kê)
    public class StudySessionCompletedMessage : ValueChangedMessage<string>
    {
        // Giá trị truyền đi là DeckID vừa học xong
        public StudySessionCompletedMessage(string deckId) : base(deckId)
        {
        }
    }
}