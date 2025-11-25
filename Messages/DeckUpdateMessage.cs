using CommunityToolkit.Mvvm.Messaging.Messages;
using EasyFlips.Models;

public class DeckUpdatedMessage : ValueChangedMessage<Deck>
{
    public DeckUpdatedMessage(Deck updatedDeck) : base(updatedDeck)
    {
    }
}