using System.Collections.Generic;

public interface IDevilStrategy
{
    IReadOnlyList<Card> ChooseCards(BattleState battle, RoundState round);
}

public sealed class BasicDevilStrategy : IDevilStrategy
{
    public IReadOnlyList<Card> ChooseCards(BattleState battle, RoundState round)
    {
        var cards = new List<Card>();

        for (int i = 0; i < 2; i++)
        {
            if (!battle.TryDrawForOpponent(out Card card))
                break;

            cards.Add(card);
        }

        return cards;
    }
}
