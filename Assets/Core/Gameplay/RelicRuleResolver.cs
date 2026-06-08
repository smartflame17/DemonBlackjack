public static class RelicRuleResolver
{
    public const string BurstTwentyTwo = "relic_burst_22";

    public static int ResolveBurstThreshold(RunState runState, int defaultBurstThreshold)
    {
        if (runState != null && runState.HasRelic(BurstTwentyTwo))
            return 22;

        return defaultBurstThreshold;
    }

    public static int ResolveTargetScore(RunState runState, int defaultTargetScore)
    {
        return defaultTargetScore;
    }
}
