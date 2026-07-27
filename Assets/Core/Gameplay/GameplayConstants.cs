using System;

namespace GameplayConstants
{
    // 포커 족보 배율 값
    public static class PokerMultiplierValue
    {
        public static readonly float HighCardMultiplier = 0f;           // 하이카드
        public static readonly float PairMultiplier = 0.2f;             // 원페어
        public static readonly float LowStraightMultiplier = 1.5f;      // 4개짜리 스트레이트
        public static readonly float TwoPairMultiplier = 2f;            // 투페어
        public static readonly float LowFlushMultiplier = 2.5f;         // 4개짜리 플러시
        public static readonly float ThreeOfAKindMultiplier = 5f;       // 트리플
        public static readonly float StraightMultiplier = 25f;          // 스트레이트
        public static readonly float FlushMultiplier = 50f;             // 5개짜리 플러시
        public static readonly float FullHouseMultiplier = 70f;         // 풀하우스
        public static readonly float FourOfAKindMultiplier = 400f;      // 포카드
        public static readonly float StraightFlushMultiplier = 777f;    // 스트레이트 플러시
        public static readonly float RoyalFlushMultiplier = 777f;       // 로얄 스트레이트 플러시

    }

    // 버스트 벌금 계산 값
    public static class BurstTransferValue
    {
        public static readonly int burstPenaltyAmount = 50;
    }

    // 포커 상금 계산 값
    public static class PokerTransferValue
    {
        public static readonly int pokerPayoutAmount = 100;
    }
}