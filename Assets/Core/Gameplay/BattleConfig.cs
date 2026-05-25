using System;
using System.Collections.Generic;
using System.Linq;

public sealed class BattleConfig
{
    public BattleConfig(
        string encounterId,
        int opponentStartingMoney,
        IDevilStrategy devilStrategy = null,
        int startingHandSize = 3,
        int targetScore = 21,
        int burstThreshold = 21,
        int baseWager = 10,
        int minWager = 10,
        int maxWager = 100,
        int wagerStep = 10,
        string devilId = null,
        IEnumerable<Modifier> initialModifiers = null)
    {
        EncounterId = string.IsNullOrWhiteSpace(encounterId) ? "default" : encounterId;
        DevilId = string.IsNullOrWhiteSpace(devilId) ? EncounterId : devilId;
        OpponentStartingMoney = opponentStartingMoney <= 0 ? 100 : opponentStartingMoney;
        DevilStrategy = devilStrategy ?? new BasicDevilStrategy();
        StartingHandSize = startingHandSize <= 0 ? 3 : startingHandSize;
        TargetScore = targetScore <= 0 ? 21 : targetScore;
        BurstThreshold = burstThreshold <= 0 ? 21 : burstThreshold;
        WagerStep = wagerStep <= 0 ? 10 : wagerStep;
        MinWager = ClampWagerBound(minWager <= 0 ? 10 : minWager);
        MaxWager = Math.Max(MinWager, ClampWagerBound(maxWager <= 0 ? 100 : maxWager));
        BaseWager = ClampWager(baseWager <= 0 ? MinWager : baseWager);
        InitialModifiers = initialModifiers?.ToList() ?? new List<Modifier>();
    }

    public string EncounterId { get; }
    public string DevilId { get; }
    public int OpponentStartingMoney { get; }
    public int OpponentMaxHp => OpponentStartingMoney;
    public IDevilStrategy DevilStrategy { get; }
    public int StartingHandSize { get; }
    public int TargetScore { get; }
    public int BurstThreshold { get; }
    public int BaseWager { get; }
    public int MinWager { get; }
    public int MaxWager { get; }
    public int WagerStep { get; }
    public IReadOnlyList<Modifier> InitialModifiers { get; }

    public int ClampWager(int wager)
    {
        return Math.Clamp(ClampWagerBound(wager), MinWager, MaxWager);
    }

    private int ClampWagerBound(int wager)
    {
        int normalized = Math.Max(0, wager);
        int remainder = normalized % WagerStep;
        return remainder == 0 ? normalized : normalized - remainder;
    }
}
