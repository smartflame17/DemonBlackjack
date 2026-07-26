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
        string devilId = null,
        IEnumerable<Modifier> initialModifiers = null,
        IEnumerable<Card> playerDeckOverride = null,
        IEnumerable<Card> opponentDeckOverride = null)
    {
        EncounterId = string.IsNullOrWhiteSpace(encounterId) ? "default" : encounterId;
        DevilId = string.IsNullOrWhiteSpace(devilId) ? EncounterId : devilId;
        OpponentStartingMoney = opponentStartingMoney <= 0 ? 100 : opponentStartingMoney;
        DevilStrategy = devilStrategy ?? new BasicDevilStrategy();
        StartingHandSize = startingHandSize <= 0 ? 3 : startingHandSize;
        TargetScore = targetScore <= 0 ? 21 : targetScore;
        BurstThreshold = burstThreshold <= 0 ? 21 : burstThreshold;
        BaseWager = Math.Max(1, baseWager);
        InitialModifiers = initialModifiers?.ToList() ?? new List<Modifier>();
        PlayerDeckOverride = playerDeckOverride?.ToList();
        OpponentDeckOverride = opponentDeckOverride?.ToList();
    }

    public string EncounterId { get; }
    public string DevilId { get; }
    public int OpponentStartingMoney { get; }
    public IDevilStrategy DevilStrategy { get; }
    public int StartingHandSize { get; }
    public int TargetScore { get; }
    public int BurstThreshold { get; }
    public int BaseWager { get; }
    public IReadOnlyList<Modifier> InitialModifiers { get; }
    public IReadOnlyList<Card> PlayerDeckOverride { get; }
    public IReadOnlyList<Card> OpponentDeckOverride { get; }
    public bool HasPlayerDeckOverride => PlayerDeckOverride != null && PlayerDeckOverride.Count > 0;
    public bool HasOpponentDeckOverride => OpponentDeckOverride != null && OpponentDeckOverride.Count > 0;
}
