public interface IDevilStrategy
{
    int DrawValue { get; }
    int ChooseCardIndex(BattleState battle, RoundState round);
}


// Default strategy that always plays first card in hand and has a fixed draw value (default 2). Can be used for testing or as a simple baseline strategy.
// Expand upon here to create complex AI strategies that consider the current battle and round state to make informed decisions on which card to play and how much to draw.
public sealed class BasicDevilStrategy : IDevilStrategy
{
    public BasicDevilStrategy(int drawValue = 2)
    {
        DrawValue = drawValue <= 0 ? 2 : drawValue;
    }

    public int DrawValue { get; }

    public int ChooseCardIndex(BattleState battle, RoundState round)
    {
        return 0;
    }
}
