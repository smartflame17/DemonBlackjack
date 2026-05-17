using System;
using System.Collections.Generic;
using System.Linq;

public sealed class BattleConfig
{
    public BattleConfig(
        string encounterId,
        int opponentMaxHp,
        IDevilStrategy devilStrategy = null,
        int startingHandSize = 3,
        int targetScore = 21,
        int burstThreshold = 21,
        int baseWager = 1,
        IEnumerable<Modifier> initialModifiers = null)
    {
        EncounterId = string.IsNullOrWhiteSpace(encounterId) ? "default" : encounterId;
        OpponentMaxHp = opponentMaxHp <= 0 ? 30 : opponentMaxHp;
        DevilStrategy = devilStrategy ?? new BasicDevilStrategy();
        StartingHandSize = startingHandSize <= 0 ? 3 : startingHandSize;
        TargetScore = targetScore <= 0 ? 21 : targetScore;
        BurstThreshold = burstThreshold <= 0 ? 21 : burstThreshold;
        BaseWager = baseWager <= 0 ? 1 : baseWager;
        InitialModifiers = initialModifiers?.ToList() ?? new List<Modifier>();
    }

    public string EncounterId { get; }
    public int OpponentMaxHp { get; }
    public IDevilStrategy DevilStrategy { get; }
    public int StartingHandSize { get; }
    public int TargetScore { get; }
    public int BurstThreshold { get; }
    public int BaseWager { get; }
    public IReadOnlyList<Modifier> InitialModifiers { get; }
}
