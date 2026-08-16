using System;

namespace GameplayConstants
{
    public static class GameSettingConfig
    {
        ///////////////////
        // 돈 관련 설정 값 //
        ///////////////////

        // 21억 4천만은 넘기지 마십시오 (int.MaxValue)

        // 게임 시작 시 플레이어의 초기 자금
        public static readonly int PlayerStartingMoney = 10000;

        // 상점 관련 설정 값
        public static readonly float ShopItemPriceInflationRate = 1.2f; // 매 라운드 상점 가격 상승폭
        public static readonly int ShopItemPriceRoundingUnit = 10; // 상점 가격 반올림 단위  (10원 단위로 반올림)

        // 게임 시작 시 플레이어의 초기 아이템 슬롯 수
        public const int DefaultMaxActiveItemSlots = 3;
        // 게임 시작 시 플레이어의 초기 유물 슬롯 수
        public const int DefaultMaxActiveRelicSlots = 1;

        public const int MaxPlayedPileCardCount = 7; // 한 라운드에서 플레이할 수 있는 최대 카드 수
    }

    // 1스테이지 관련 설정 값
    public static class Devil1Config
    {
        // 1스테이지 악마 초기 자금
        public static readonly int Devil1StartingMoney = 100000;
        // 1스테이지 악마 라운드배팅 기본금
        public static readonly int Devil1Wager = 1000;

        public static readonly float Devil1HeartMultiplier = 1f; // 1스테이지 악마 하트 카드 배율 (하트 카드 수+1 x 배율)
        public static readonly float Devil1SpadeMultiplier = 4f; // 1스테이지 악마 스페이드 카드 배율 ((하트 카드 수+1) x 스페이드 카드 x 배율)
    }

    // 2스테이지 관련 설정 값
    public static class Devil2Config
    {
        // 2스테이지 악마 초기 자금
        public static readonly int Devil2StartingMoney = 1000000;
        // 2스테이지 악마 라운드배팅 기본금
        public static readonly int Devil2Wager = 1000;
        public static readonly float Devil2WagerInflationRate = 2f; // 2스테이지 악마 패배 시 기본금 인플레이션율 (다음 라운드 기본 배팅금이 증가)
        public static readonly int Devil2CircuitBreakerThreshold = 200000; // 2스테이지 악마 족보 제한 모드 발동 기준
        public static readonly int Devil2PokerPayoutThreshold = 10000; // 족보 제한 시, 한 라운드에 플레이어가 받을 수 있는 포커 상금의 최대 한도
    }

    // 3스테이지 관련 설정 값
    public static class Devil3Config
    {
        // 3스테이지 악마 초기 자금
        public static readonly int Devil3StartingMoney = 300000;
        // 3스테이지 악마 라운드배팅 기본금
        public static readonly int Devil3Wager = 3000;
    }

    // 4스테이지 관련 설정 값
    public static class Devil4Config
    {
        // 4스테이지 악마 초기 자금
        public static readonly int Devil4StartingMoney = 400000;
        // 4스테이지 악마 라운드배팅 기본금
        public static readonly int Devil4Wager = 4000;
    }


    // 포커 족보 배율 값
    public static class PokerMultiplierValue
    {
        public static readonly float HighCardMultiplier = 0f;           // 하이카드
        public static readonly float PairMultiplier = 0.2f;             // 원페어
        public static readonly float LowStraightMultiplier = 1.5f;      // 4개짜리 스트레이트
        public static readonly float TwoPairMultiplier = 2f;            // 투페어
        public static readonly float LowFlushMultiplier = 2.5f;         // 4개짜리 플러시
        public static readonly float ThreeOfAKindMultiplier = 5f;       // 트리플
        public static readonly float StraightMultiplier = 25f;          // 5개짜리 스트레이트
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

    // 카드 업그레이드 관련 값
    public static class CardUpgradeValue
    {
        
    }

    // 아이템 관련 값
    public static class ActiveItemValue
    {
        public static readonly int DrawTwoItemDrawCount = 2;      // 내정자 면접 XX 아이템 사용 시 플레이어가 뽑는 XX 문양 카드 수 (DrawTwoHearts, DrawTwoDiamonds, DrawTwoClubs, DrawTwoSpades)
        public static readonly int DrawThreeItemDrawCount = 3;    // 특근 지시서 아이템 사용 시 플레이어가 손패로 뽑는 카드 수 (DrawThree)
        public static readonly int LeverageTripleItemMultiplier = 3; // 레버리지 아이템 사용 시 플레이어가 배팅하는 금액의 배수 (LeverageTriple)
        public static readonly int BurstThresholdPlusThreeItemIncrease = 3; // 분식 회계 아이템 사용 시 플레이어의 버스트 기준치 증가량 (BurstThresholdPlusThree)
    }

    // 유물 관련 값
    public static class RelicValue
    {
        public static readonly int BurstExtendRelicHitTarget = 5;   // 히트 횟수에 따라 플레이어의 버스트 기준치를 증가시키는 유물(BurstExtendRelic) 사용 시, 플레이어가 히트를 몇 번 해야 버스트 기준치가 증가하는지 (BurstExtendRelic)
    }
}