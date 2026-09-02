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

    public BattleConfigData ToData()
    {
        if (DevilStrategy is not IPersistableDevilStrategy persistableStrategy
            || !IsSupportedPersistableStrategy(DevilStrategy, persistableStrategy.PersistenceId))
            return null;

        var data = new BattleConfigData
        {
            encounterId = EncounterId,
            devilId = DevilId,
            opponentStartingMoney = OpponentStartingMoney,
            startingHandSize = StartingHandSize,
            burstThreshold = BurstThreshold,
            baseWager = BaseWager,
            strategyId = persistableStrategy.PersistenceId,
            strategyDrawValue = DevilStrategy.DrawValue,
            strategyStartingMoney = DevilStrategy is Devil1Strategy devil1 ? devil1.StartingMoney : 0,
            hasPlayerDeckOverride = PlayerDeckOverride != null,
            hasOpponentDeckOverride = OpponentDeckOverride != null
        };

        for (int i = 0; i < InitialModifiers.Count; i++)
            data.initialModifiers.Add(ModifierData.FromModifier(InitialModifiers[i]));
        if (PlayerDeckOverride != null)
        {
            for (int i = 0; i < PlayerDeckOverride.Count; i++)
                data.playerDeckOverride.Add(CardData.FromCard(PlayerDeckOverride[i]));
        }
        if (OpponentDeckOverride != null)
        {
            for (int i = 0; i < OpponentDeckOverride.Count; i++)
                data.opponentDeckOverride.Add(CardData.FromCard(OpponentDeckOverride[i]));
        }

        return data;
    }

    public static BattleConfig FromData(BattleConfigData data)
    {
        if (data == null)
            return null;

        IDevilStrategy strategy = CreateStrategy(data);
        if (strategy == null)
            return null;

        return new BattleConfig(
            data.encounterId,
            data.opponentStartingMoney,
            strategy,
            data.startingHandSize,
            data.burstThreshold,
            data.baseWager,
            data.devilId,
            ConvertModifiers(data.initialModifiers),
            data.hasPlayerDeckOverride ? ConvertCards(data.playerDeckOverride) : null,
            data.hasOpponentDeckOverride ? ConvertCards(data.opponentDeckOverride) : null);
    }

    private static IDevilStrategy CreateStrategy(BattleConfigData data)
    {
        return data.strategyId switch
        {
            "basic" => new BasicDevilStrategy(data.strategyDrawValue),
            "devil1" => new Devil1Strategy(data.strategyStartingMoney > 0 ? data.strategyStartingMoney : data.opponentStartingMoney),
            "devil2" => new Devil2Strategy(data.strategyDrawValue),
            "devil3" => new Devil3Strategy(data.strategyDrawValue),
            "devil4" => new Devil4Strategy(data.strategyDrawValue),
            _ => null
        };
    }

    private static bool IsSupportedPersistableStrategy(IDevilStrategy strategy, string persistenceId)
    {
        return persistenceId switch
        {
            "basic" => strategy.GetType() == typeof(BasicDevilStrategy),
            "devil1" => strategy.GetType() == typeof(Devil1Strategy),
            "devil2" => strategy.GetType() == typeof(Devil2Strategy),
            "devil3" => strategy.GetType() == typeof(Devil3Strategy),
            "devil4" => strategy.GetType() == typeof(Devil4Strategy),
            _ => false
        };
    }

    private static List<Modifier> ConvertModifiers(List<ModifierData> source)
    {
        var result = new List<Modifier>();
        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
                result.Add(source[i].ToModifier());
        }
        return result;
    }

    private static List<Card> ConvertCards(List<CardData> source)
    {
        var result = new List<Card>();
        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
                result.Add(source[i].ToCard());
        }
        return result;
    }
}
