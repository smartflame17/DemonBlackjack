using System;
using System.Collections.Generic;

public sealed class ReplayableRandom
{
    private readonly int seed;
    private readonly Random random;
    private readonly List<RandomCallData> calls = new();

    public ReplayableRandom(int seed)
    {
        this.seed = seed;
        random = new Random(seed);
    }

    private ReplayableRandom(RandomStateData data)
        : this(data.seed)
    {
        if (data.calls == null)
            return;

        for (int i = 0; i < data.calls.Count; i++)
        {
            RandomCallData call = data.calls[i];
            if (call == null || call.maximumExclusive <= call.minimumInclusive)
                throw new ArgumentException("Random state contains an invalid call range.", nameof(data));

            random.Next(call.minimumInclusive, call.maximumExclusive);
            calls.Add(new RandomCallData
            {
                minimumInclusive = call.minimumInclusive,
                maximumExclusive = call.maximumExclusive
            });
        }
    }

    public int Next(int maximumExclusive)
    {
        return Next(0, maximumExclusive);
    }

    public int Next(int minimumInclusive, int maximumExclusive)
    {
        int result = random.Next(minimumInclusive, maximumExclusive);
        calls.Add(new RandomCallData
        {
            minimumInclusive = minimumInclusive,
            maximumExclusive = maximumExclusive
        });
        return result;
    }

    public RandomStateData ToData()
    {
        var data = new RandomStateData { seed = seed };
        for (int i = 0; i < calls.Count; i++)
        {
            data.calls.Add(new RandomCallData
            {
                minimumInclusive = calls[i].minimumInclusive,
                maximumExclusive = calls[i].maximumExclusive
            });
        }

        return data;
    }

    public static ReplayableRandom FromData(RandomStateData data, int fallbackSeed)
    {
        return data == null ? new ReplayableRandom(fallbackSeed) : new ReplayableRandom(data);
    }
}
