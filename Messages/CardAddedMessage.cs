using CommunityToolkit.Mvvm.Messaging.Messages;

namespace IT008.Q13_Project___fromScratch.Messages
{
    // Tin nhắn báo hiệu một thẻ mới vừa được thêm vào Deck có ID cụ thể
    public class CardAddedMessage : ValueChangedMessage<int>
    {
        // Giá trị truyền đi là DeckID (int)
        public CardAddedMessage(int deckId) : base(deckId)
        {
        }
    }
}