using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TutorialScenarioConfig", menuName = "Demon Blackjack/Tutorial Scenario")]
public sealed class TutorialScenarioConfig : ScriptableObject
{
    [SerializeField] private int seed = 6767;
    [SerializeField] private List<TutorialStepSpec> steps = new();
    [SerializeField] private List<TutorialRoundSpec> rounds = new();

    public int Seed => seed;
    public IReadOnlyList<TutorialStepSpec> Steps => steps;
    public IReadOnlyList<TutorialRoundSpec> Rounds => rounds;

    public bool TryGetStep(TutorialStepKey key, out TutorialStepSpec step)
    {
        if (steps != null)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                TutorialStepSpec candidate = steps[i];
                if (candidate != null && candidate.Key == key)
                {
                    step = candidate;
                    return true;
                }
            }
        }

        step = null;
        return false;
    }

    public bool TryGetRound(int index, out TutorialRoundSpec round)
    {
        if (index >= 0 && rounds != null && index < rounds.Count)
        {
            round = rounds[index];
            return true;
        }

        round = null;
        return false;
    }

    public static TutorialScenarioConfig CreateDefaultRuntime()
    {
        TutorialScenarioConfig config = CreateInstance<TutorialScenarioConfig>();
        config.seed = 6767;
        config.steps = CreateDefaultSteps();
        config.rounds = CreateDefaultRounds();
        return config;
    }

    public static List<TutorialStepSpec> CreateDefaultSteps()
    {
        return new List<TutorialStepSpec>
        {
            TutorialStepSpec.Create(
                TutorialStepKey.SceneBoot,
                TutorialOverlayMode.Choice,
                "딜러",
                "어서 와. 말로만 들으면 복잡하니까 직접 한 판씩 해보자.",
                string.Empty,
                TutorialGuideTarget.None,
                new[] { "직접 해보면 되겠네.", "좋아, 따라가 볼게." }),
            TutorialStepSpec.Create(
                TutorialStepKey.RuleIntro,
                TutorialOverlayMode.Choice,
                "딜러",
                "손에 있는 카드는 가운데 네 필드로 내면 돼.\n덱에서 카드를 더 받는 건 Hit, 그만 받을 땐 Stand야.\n21을 넘기면 버스트가 되지만, Stand하기 전까지는 행동을 계속할 수 있어.",
                string.Empty,
                TutorialGuideTarget.None,
                new[] { "직접 해볼게.", "좋아, 시작하자." }),
            TutorialStepSpec.Create(
                TutorialStepKey.R1PlayThree,
                TutorialOverlayMode.GameplayGuide,
                "딜러",
                "손에 4 두 장과 3이 있지? 4는 그대로 두고 3부터 내봐.",
                string.Empty,
                TutorialGuideTarget.PlayerHand),
            TutorialStepSpec.Create(
                TutorialStepKey.R1HitNine,
                TutorialOverlayMode.GameplayGuide,
                "딜러",
                "좋아. 4 두 장은 손에 남았어. 이번에는 덱을 길게 눌러 카드를 한 장 네 필드로 가져와.",
                string.Empty,
                TutorialGuideTarget.DrawPile),
            TutorialStepSpec.Create(
                TutorialStepKey.R1HitTen,
                TutorialOverlayMode.GameplayGuide,
                "딜러",
                "아직 21 아래네. 덱에서 카드를 한 장 더 받아봐.",
                string.Empty,
                TutorialGuideTarget.DrawPile),
            TutorialStepSpec.Create(
                TutorialStepKey.R1BustExplanation,
                TutorialOverlayMode.GameplayGuide,
                "딜러",
                "3, 9, 10을 더하면 22야. 21을 넘겼으니 버스트지.\n버스트가 돼도 판이 바로 끝나는 건 아니야. 네가 Stand할 때까지는 계속 행동할 수 있어.",
                string.Empty,
                TutorialGuideTarget.PlayerScore,
                new[] { "버스트여도 직접 끝내야 하는구나.", "이제 Stand하면 되겠네." }),
            TutorialStepSpec.Create(
                TutorialStepKey.R1Stand,
                TutorialOverlayMode.GameplayGuide,
                "딜러",
                "이번 판은 여기까지야. Stand를 눌러.",
                string.Empty,
                TutorialGuideTarget.StandButton),
            TutorialStepSpec.Create(
                TutorialStepKey.R1Settlement,
                TutorialOverlayMode.Result,
                "딜러",
                "정산서는 이번 판의 결과를 보여줘.\n필드에 냈던 3, 9, 10은 정리됐지만, 내지 않은 4 두 장은 아직 네 손에 남아 있어.\n다음 판에서는 그 두 장으로 포커 족보를 만들어보자.",
                string.Empty,
                TutorialGuideTarget.None,
                new[] { "다음 판에서 만회할게.", "이번에는 족보를 만들어볼게." }),
            TutorialStepSpec.Create(
                TutorialStepKey.R2HitFour,
                TutorialOverlayMode.GameplayGuide,
                "딜러",
                "손에 남은 4 두 장이 보이지? 먼저 덱에서 한 장 받아봐.",
                string.Empty,
                TutorialGuideTarget.DrawPile),
            TutorialStepSpec.Create(
                TutorialStepKey.R2PlayPairFour,
                TutorialOverlayMode.GameplayGuide,
                "딜러",
                "필드에 첫 번째 4가 나왔어. 아직은 족보가 아니야. 손에 있는 4를 한 장 내봐.",
                string.Empty,
                TutorialGuideTarget.PlayerHand),
            TutorialStepSpec.Create(
                TutorialStepKey.R2PairExplanation,
                TutorialOverlayMode.GameplayGuide,
                "딜러",
                "같은 숫자 두 장. 이게 페어야.\n카드가 더 모이면 더 높은 족보로 바뀔 수 있어.",
                string.Empty,
                TutorialGuideTarget.PlayerPlayPile,
                new[] { "두 장이면 페어구나.", "한 장 더 모아볼게." }),
            TutorialStepSpec.Create(
                TutorialStepKey.R2PlayTripleFour,
                TutorialOverlayMode.GameplayGuide,
                "딜러",
                "남은 4도 내봐.",
                string.Empty,
                TutorialGuideTarget.PlayerHand),
            TutorialStepSpec.Create(
                TutorialStepKey.R2TripleExplanation,
                TutorialOverlayMode.GameplayGuide,
                "딜러",
                "같은 숫자 세 장. 이번에는 트리플이 됐네.",
                string.Empty,
                TutorialGuideTarget.PlayerPlayPile,
                new[] { "세 장이면 트리플이구나.", "새 손패도 확인할게." }),
            TutorialStepSpec.Create(
                TutorialStepKey.R2RefillExplanation,
                TutorialOverlayMode.GameplayGuide,
                "딜러",
                "그리고 손패를 전부 썼으니 새 카드 세 장이 들어왔어.\n새 손패는 2, 7, 4야.",
                string.Empty,
                TutorialGuideTarget.PlayerHand,
                new[] { "새 손패를 확인했어.", "남은 4를 내면 되겠네." }),
            TutorialStepSpec.Create(
                TutorialStepKey.R2PlayFourOfAKind,
                TutorialOverlayMode.GameplayGuide,
                "딜러",
                "새로 들어온 카드 중에도 4가 있지? 그 카드까지 내봐.",
                string.Empty,
                TutorialGuideTarget.PlayerHand),
            TutorialStepSpec.Create(
                TutorialStepKey.R2FourOfAKindExplanation,
                TutorialOverlayMode.GameplayGuide,
                "딜러",
                "같은 숫자 네 장. 포카드 완성이야.\n처음에는 페어, 세 장에서는 트리플, 네 장이 되면서 포카드로 올라왔어.\n블랙잭 점수와 별개로 포커 족보가 이번 판의 배율을 올려줘.",
                string.Empty,
                TutorialGuideTarget.PokerResult,
                new[] { "페어부터 포카드까지 이해했어.", "족보가 높을수록 배율도 커지는구나." }),
            TutorialStepSpec.Create(
                TutorialStepKey.R2Stand,
                TutorialOverlayMode.GameplayGuide,
                "딜러",
                "족보까지 확인했으면 Stand로 판을 마무리해.",
                string.Empty,
                TutorialGuideTarget.StandButton),
            TutorialStepSpec.Create(
                TutorialStepKey.R2Settlement,
                TutorialOverlayMode.Result,
                "딜러",
                "이번에는 16 대 15로 이겼어.\n거기에 포카드 배율까지 붙어서 정산 결과가 더 커졌지.",
                string.Empty,
                TutorialGuideTarget.None,
                new[] { "정산 결과를 확인했어.", "족보 배율도 확인했어." }),
            TutorialStepSpec.Create(
                TutorialStepKey.FinalResponse,
                TutorialOverlayMode.Choice,
                "딜러",
                "이제 기본 흐름은 다 봤어. 실전에 들어가기 전에 마지막으로 하나만 더 짚고 갈게.",
                string.Empty,
                TutorialGuideTarget.None,
                new[] { "이제 판이 어떻게 돌아가는지 알겠어.", "실전에서도 족보를 노려볼게." }),
            TutorialStepSpec.Create(
                TutorialStepKey.SystemPreview,
                TutorialOverlayMode.Choice,
                "딜러",
                "실전에서는 카드 효과와 아이템을 이용해서 이 흐름을 바꿀 수 있어.\n지금 배운 카드 플레이, Hit, Stand, 손패 리필과 포커 족보가 기본이야.",
                string.Empty,
                TutorialGuideTarget.None,
                new[] { "기본은 이해했어.", "마지막으로 확인할게." }),
            TutorialStepSpec.Create(
                TutorialStepKey.Complete,
                TutorialOverlayMode.Choice,
                "딜러",
                "여기까지 기억하면 돼. 준비됐으면 다음으로 넘어가자.",
                string.Empty,
                TutorialGuideTarget.None,
                new[] { "이제 실전으로 갈게.", "한 번 더 확인하고 갈게." })
        };
    }

    public static List<TutorialRoundSpec> CreateDefaultRounds()
    {
        return new List<TutorialRoundSpec>
        {
            TutorialRoundSpec.Create(
                "tutorial_battle",
                "devil1",
                1000,
                3,
                3,
                15,
                10,
                21,
                21,
                new[]
                {
                    new TutorialCardSpec(Suit.Hearts, Rank.Four),
                    new TutorialCardSpec(Suit.Diamonds, Rank.Four),
                    new TutorialCardSpec(Suit.Clubs, Rank.Three),
                    new TutorialCardSpec(Suit.Spades, Rank.Nine),
                    new TutorialCardSpec(Suit.Hearts, Rank.Ten),
                    new TutorialCardSpec(Suit.Clubs, Rank.Four),
                    new TutorialCardSpec(Suit.Diamonds, Rank.Two),
                    new TutorialCardSpec(Suit.Spades, Rank.Seven),
                    new TutorialCardSpec(Suit.Spades, Rank.Four)
                },
                new[]
                {
                    new TutorialCardSpec(Suit.Clubs, Rank.Ten),
                    new TutorialCardSpec(Suit.Diamonds, Rank.Seven),
                    new TutorialCardSpec(Suit.Hearts, Rank.Six),
                    new TutorialCardSpec(Suit.Clubs, Rank.Nine),
                    new TutorialCardSpec(Suit.Hearts, Rank.Two),
                    new TutorialCardSpec(Suit.Diamonds, Rank.Three)
                },
                new[]
                {
                    new TutorialPlayerActionSpec(TutorialPlayerActionType.PlayRank, Rank.Three),
                    new TutorialPlayerActionSpec(TutorialPlayerActionType.HitRank, Rank.Nine),
                    new TutorialPlayerActionSpec(TutorialPlayerActionType.HitRank, Rank.Ten),
                    new TutorialPlayerActionSpec(TutorialPlayerActionType.Stand)
                }),
            TutorialRoundSpec.Create(
                "tutorial_battle",
                "devil1",
                1000,
                3,
                3,
                15,
                10,
                21,
                21,
                Array.Empty<TutorialCardSpec>(),
                Array.Empty<TutorialCardSpec>(),
                new[]
                {
                    new TutorialPlayerActionSpec(TutorialPlayerActionType.HitRank, Rank.Four),
                    new TutorialPlayerActionSpec(TutorialPlayerActionType.PlayRank, Rank.Four),
                    new TutorialPlayerActionSpec(TutorialPlayerActionType.PlayRank, Rank.Four),
                    new TutorialPlayerActionSpec(TutorialPlayerActionType.PlayRank, Rank.Four),
                    new TutorialPlayerActionSpec(TutorialPlayerActionType.Stand)
                })
        };
    }
}

public enum TutorialStepKey
{
    SceneBoot,
    RuleIntro,
    R1PlayThree,
    R1HitNine,
    R1HitTen,
    R1BustExplanation,
    R1Stand,
    R1Settlement,
    R2HitFour,
    R2PlayPairFour,
    R2PairExplanation,
    R2PlayTripleFour,
    R2TripleExplanation,
    R2RefillExplanation,
    R2PlayFourOfAKind,
    R2FourOfAKindExplanation,
    R2Stand,
    R2Settlement,
    FinalResponse,
    SystemPreview,
    Complete
}

public enum TutorialOverlayMode
{
    Dialogue,
    Choice,
    GameplayGuide,
    Result
}

public enum TutorialGuideTarget
{
    None,
    PlayerHand,
    PlayerPlayPile,
    DrawPile,
    StandButton,
    PlayerScore,
    PokerResult
}

[Serializable]
public sealed class TutorialStepSpec
{
    [SerializeField] private TutorialStepKey key;
    [SerializeField] private TutorialOverlayMode mode;
    [SerializeField] private string title;
    [SerializeField, TextArea] private string body;
    [SerializeField] private string actionText;
    [SerializeField] private TutorialGuideTarget guideTarget;
    [SerializeField] private List<string> choices = new();

    public TutorialStepKey Key => key;
    public TutorialOverlayMode Mode => mode;
    public string Title => title;
    public string Body => body;
    public string ActionText => actionText;
    public TutorialGuideTarget GuideTarget => guideTarget;
    public IReadOnlyList<string> Choices => choices;

    public static TutorialStepSpec Create(
        TutorialStepKey key,
        TutorialOverlayMode mode,
        string title,
        string body,
        string actionText,
        TutorialGuideTarget guideTarget,
        IEnumerable<string> choices = null)
    {
        return new TutorialStepSpec
        {
            key = key,
            mode = mode,
            title = title,
            body = body,
            actionText = actionText,
            guideTarget = guideTarget,
            choices = choices != null ? new List<string>(choices) : new List<string>()
        };
    }
}

[Serializable]
public sealed class TutorialRoundSpec
{
    [SerializeField] private string encounterId;
    [SerializeField] private string devilId;
    [SerializeField] private int opponentStartingMoney = 1000;
    [SerializeField] private int playerStartingHandSize = 3;
    [SerializeField] private int opponentDrawValue = 3;
    [SerializeField] private int opponentStandScore = 17;
    [SerializeField] private int baseWager = 10;
    [SerializeField] private int targetScore = 21;
    [SerializeField] private int burstThreshold = 21;
    [SerializeField] private List<TutorialCardSpec> playerDrawOrder = new();
    [SerializeField] private List<TutorialCardSpec> opponentDrawOrder = new();
    [SerializeField] private List<TutorialPlayerActionSpec> playerActions = new();

    public string EncounterId => string.IsNullOrWhiteSpace(encounterId) ? "tutorial" : encounterId;
    public string DevilId => string.IsNullOrWhiteSpace(devilId) ? EncounterId : devilId;
    public int OpponentStartingMoney => opponentStartingMoney <= 0 ? 1000 : opponentStartingMoney;
    public int PlayerStartingHandSize => playerStartingHandSize <= 0 ? 3 : playerStartingHandSize;
    public int OpponentDrawValue => opponentDrawValue <= 0 ? 3 : opponentDrawValue;
    public int OpponentStandScore => opponentStandScore <= 0 ? 17 : opponentStandScore;
    public int BaseWager => Math.Max(1, baseWager);
    public int TargetScore => targetScore <= 0 ? 21 : targetScore;
    public int BurstThreshold => burstThreshold <= 0 ? 21 : burstThreshold;
    public IReadOnlyList<TutorialPlayerActionSpec> PlayerActions => playerActions;

    public List<Card> CreatePlayerDeck()
    {
        return CreateDeck(playerDrawOrder);
    }

    public List<Card> CreateOpponentDeck()
    {
        return CreateDeck(opponentDrawOrder);
    }

    public static TutorialRoundSpec Create(
        string encounterId,
        string devilId,
        int opponentStartingMoney,
        int playerStartingHandSize,
        int opponentDrawValue,
        int opponentStandScore,
        int baseWager,
        int targetScore,
        int burstThreshold,
        IEnumerable<TutorialCardSpec> playerDrawOrder,
        IEnumerable<TutorialCardSpec> opponentDrawOrder,
        IEnumerable<TutorialPlayerActionSpec> playerActions = null)
    {
        return new TutorialRoundSpec
        {
            encounterId = encounterId,
            devilId = devilId,
            opponentStartingMoney = opponentStartingMoney,
            playerStartingHandSize = playerStartingHandSize,
            opponentDrawValue = opponentDrawValue,
            opponentStandScore = opponentStandScore,
            baseWager = baseWager,
            targetScore = targetScore,
            burstThreshold = burstThreshold,
            playerDrawOrder = playerDrawOrder != null ? new List<TutorialCardSpec>(playerDrawOrder) : new List<TutorialCardSpec>(),
            opponentDrawOrder = opponentDrawOrder != null ? new List<TutorialCardSpec>(opponentDrawOrder) : new List<TutorialCardSpec>(),
            playerActions = playerActions != null ? new List<TutorialPlayerActionSpec>(playerActions) : new List<TutorialPlayerActionSpec>()
        };
    }

    private static List<Card> CreateDeck(IReadOnlyList<TutorialCardSpec> drawOrder)
    {
        var deck = new List<Card>();
        if (drawOrder == null)
            return deck;

        for (int i = 0; i < drawOrder.Count; i++)
            deck.Add(drawOrder[i].ToCard());
        return deck;
    }
}

public enum TutorialPlayerActionType
{
    PlayRank,
    HitRank,
    Stand
}

[Serializable]
public struct TutorialPlayerActionSpec
{
    [SerializeField] private TutorialPlayerActionType actionType;
    [SerializeField] private Rank rank;

    public TutorialPlayerActionSpec(TutorialPlayerActionType actionType, Rank rank = Rank.Ace)
    {
        this.actionType = actionType;
        this.rank = rank;
    }

    public TutorialPlayerActionType ActionType => actionType;
    public Rank Rank => rank;
    public bool RequiresRank => actionType == TutorialPlayerActionType.PlayRank || actionType == TutorialPlayerActionType.HitRank;
}

[Serializable]
public struct TutorialCardSpec
{
    [SerializeField] private Suit suit;
    [SerializeField] private Rank rank;
    [SerializeField] private string modifierId;

    public TutorialCardSpec(Suit suit, Rank rank, string modifierId = null)
    {
        this.suit = suit;
        this.rank = rank;
        this.modifierId = modifierId;
    }

    public Suit Suit => suit;
    public Rank Rank => rank;
    public string ModifierId => modifierId;

    public Card ToCard()
    {
        return new Card(suit, rank, modifierId);
    }
}
