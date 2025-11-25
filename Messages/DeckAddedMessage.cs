using CommunityToolkit.Mvvm.Messaging.Messages;
using EasyFlips.Models;

namespace EasyFlips.Messages
{
    // Một "bức thư" mang theo thông tin về Deck vừa được thêm
    public class DeckAddedMessage : ValueChangedMessage<Deck>
    {
        public DeckAddedMessage(Deck newDeck) : base(newDeck)
        {
        }
    }
}