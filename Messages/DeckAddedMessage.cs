using CommunityToolkit.Mvvm.Messaging.Messages;
using IT008.Q13_Project___fromScratch.Models;

namespace IT008.Q13_Project___fromScratch.Messages
{
    // Một "bức thư" mang theo thông tin về Deck vừa được thêm
    public class DeckAddedMessage : ValueChangedMessage<Deck>
    {
        public DeckAddedMessage(Deck newDeck) : base(newDeck)
        {
        }
    }
}