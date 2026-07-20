using System;
using System.Collections.Generic;

public static class ShopOfferGenerator
{
    public static int CreateSeed(RunState runState, int roundNumber)
    {
        int shopCycle = GetShopCycle(roundNumber);
        if (runState == null)
            return shopCycle;

        unchecked
        {
            int seed = runState.Seed;
            seed = (seed * 397) ^ runState.EncounterIndex;
            seed = (seed * 397) ^ shopCycle;
            return seed;
        }
    }

    public static int GetShopCycle(int roundNumber)
    {
        int normalizedRound = Math.Max(1, roundNumber);
        return ((normalizedRound - 1) / 3) + 1;
    }

    public static List<T> TakeRandom<T>(IReadOnlyList<T> source, int count, Random random)
    {
        var pool = source != null ? new List<T>(source) : new List<T>();
        var result = new List<T>(Math.Min(Math.Max(0, count), pool.Count));
        random ??= new Random(0);

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int index = random.Next(pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
    }

    public static List<CardUpgradeOffer> CreateCardUpgradeOfferPool(IReadOnlyList<CardUpgradeDefinition> definitions)
    {
        var offers = new List<CardUpgradeOffer>();
        if (definitions == null)
            return offers;

        for (int i = 0; i < definitions.Count; i++)
        {
            CardUpgradeDefinition definition = definitions[i];
            if (definition == null)
                continue;

            foreach (Rank rank in Enum.GetValues(typeof(Rank)))
            {
                if (definition.CanApplyToRank(rank))
                    offers.Add(new CardUpgradeOffer(definition, rank));
            }
        }

        return offers;
    }
}
