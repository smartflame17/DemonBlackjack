public static class RelicRuleResolver
{
    public const string BurstTwentyTwo = "relic_burst_22";
    public const string AddJqk = "add_jqk";
    public const string BurstExtend = "burst_extend";
    public const string SuitOverride = "suit_override";

    public static int ResolvePlayerBurstThreshold(RunState runState, int defaultBurstThreshold)
    {
        int baseThreshold = runState != null && runState.HasRelic(BurstTwentyTwo) ? 22 : defaultBurstThreshold;
        return baseThreshold + (runState != null ? runState.PlayerBurstThresholdBonus : 0);
    }

    public static int ResolveOpponentBurstThreshold(RunState runState, int defaultBurstThreshold)
    {
        return defaultBurstThreshold;
    }

    public static int ResolveBurstThreshold(RunState runState, int defaultBurstThreshold)
    {
        return ResolvePlayerBurstThreshold(runState, defaultBurstThreshold);
    }
}
