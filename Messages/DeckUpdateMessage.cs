using CommunityToolkit.Mvvm.Messaging.Messages;
using IT008.Q13_Project___fromScratch.Models;

public class DeckUpdatedMessage : ValueChangedMessage<Deck>
{
    public DeckUpdatedMessage(Deck updatedDeck) : base(updatedDeck)
    {
    }
}