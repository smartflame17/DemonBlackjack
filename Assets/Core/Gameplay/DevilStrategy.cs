public interface IDevilStrategy
{
    int DrawValue { get; }
    int ChooseCardIndex(BattleState battle, RoundState round);
}

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
