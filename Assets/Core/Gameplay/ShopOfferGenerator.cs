using System;
using System.Collections.Generic;

public static class ShopOfferGenerator
{
    public static int CreateSeed(RunState runState, int roundNumber)
    {
        if (runState == null)
            return roundNumber;

        unchecked
        {
            int seed = runState.Seed;
            seed = (seed * 397) ^ runState.EncounterIndex;
            seed = (seed * 397) ^ roundNumber;
            return seed;
        }
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
}
