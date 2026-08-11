using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Serialization;
using System;
using System.Globalization;

// This component is responsible for battle UI presentation - Runtime monobehaviour binding.
public sealed class BattleUiPresenter : MonoBehaviour
{
    [SerializeField] private BattleController battleController;
    [SerializeField] private RunManager runManager;
    [SerializeField] private GameplayAssetRegistry assetRegistry;

    [Header("Devil Ability")]
    [SerializeField] private TooltipTrigger devilAbilityTooltipTrigger;

    [Header("Main")]
    [SerializeField] private BattleUiCardView cardPrefab;
    [SerializeField] private TMP_Text playerScoreText;
    [SerializeField] private TMP_Text devilScoreText;
    [SerializeField] private TMP_Text playerBurstThresholdText;
    [SerializeField] private TMP_Text devilBurstThresholdText;
    [SerializeField] private RectTransform playerHandRoot;
    [SerializeField] private RectTransform opponentHandRoot;
    [SerializeField] private RectTransform devilPlayPileRoot;
    [SerializeField] private RectTransform sharedPlayPileRoot;
    [SerializeField] private RectTransform playerPlayPileRoot;
    [SerializeField] private RectTransform playPileRoot;
    [SerializeField] private Button standButton;

    [Header("Round Start")]
    [FormerlySerializedAs("wagerPanel")]
    [SerializeField] private GameObject roundStartPanel;
    [SerializeField, Min(0f)] public float roundStartDelaySeconds = 2f;

    [Header("Opponent Turn")]
    [SerializeField, Min(0f)] private float opponentTurnDelayMinSeconds = 0.75f;
    [SerializeField, Min(0f)] private float opponentTurnDelayMaxSeconds = 1.5f;

    [Header("Deck View")]
    [SerializeField] private DeckViewPanel deckViewPanel;
    [FormerlySerializedAs("viewDrawPileButton")]
    [SerializeField] private Button viewPileButton;

    [Header("Drag Interactions")]
    [SerializeField] private RectTransform playerHandImage;
    [SerializeField] private DevilStandIndicator devilStandIndicator;
    [SerializeField] private TMP_Text playerChoiceMoneyPreviewText;
    [SerializeField, Range(0f, 1f)] private float playerChoiceMoneyPreviewActiveAlpha = 0.3f;

    [Header("Results")]
    [SerializeField] private GameObject roundResultPanel;
    [SerializeField] private TMP_Text roundResultText;
    [FormerlySerializedAs("nextRoundButton")]
    [SerializeField] private Button toShopButton;
    [SerializeField] private Button nextRoundButton;
    [SerializeField] private GameObject battleResultPanel;
    [SerializeField] private RectTransform rewardViewRoot;
    [SerializeField] private Button backToMapButton;
    [SerializeField] private TMP_Text backToMapButtonText;

    [Header("Card Animation")]
    [SerializeField] private Vector2 cardDrawStartOffset = new(1200f, 0f);
    [SerializeField] private Vector2 cardDiscardEndOffset = new(-1200f, 0f);
    [SerializeField] private float cardDrawDurationSeconds = 0.28f;
    [SerializeField] private float cardDiscardDurationSeconds = 0.22f;
    [SerializeField] private float cardPlayDurationSeconds = 0.34f;
    [SerializeField] private float cardPlayJumpPower = 32f;
    [SerializeField] private Ease cardDrawEase = Ease.OutCubic;
    [SerializeField] private Ease cardDiscardEase = Ease.InCubic;
    [SerializeField] private Ease cardPlayEase = Ease.OutQuad;

    private readonly List<BattleUiCardView> _playerCards = new();
    private readonly List<BattleUiCardView> _opponentCards = new();
    private readonly List<BattleUiCardView> _devilPlayPileCards = new();
    private readonly List<BattleUiCardView> _sharedPlayPileCards = new();
    private readonly List<BattleUiCardView> _playerPlayPileCards = new();
    private readonly List<CardLayoutSnapshot> _previousPlayerHand = new();
    private readonly List<CardLayoutSnapshot> _previousOpponentHand = new();
    private readonly List<Card> _lastPlayerHandCards = new();
    private readonly List<Card> _lastOpponentHandCards = new();
    private readonly List<Card> _lastPlayerPileCards = new();
    private readonly List<Card> _lastOpponentPileCards = new();
    private readonly List<Card> _lastSharedPileCards = new();
    private readonly List<Tween> _cardTweens = new();

    private DeckViewLongPressDragStarter _deckViewDragStarter;
    private PlayerHandHitDragHandler _playerHandHitDragHandler;
    private Coroutine _roundStartRoutine;
    private Coroutine _turnHandoffRoutine;
    private BattleState _scheduledRoundBattle;
    private BattleState _scheduledTurnBattle;
    private RoundState _scheduledTurnRound;
    private int _pendingWager = 10;
    private bool _suppressRoundPilesUntilNextRound;
    private bool _turnHandoffPending;

    public bool CanPlayerAct
    {
        get
        {
            BattleState battle = battleController != null ? battleController.BattleState : null;
            return isActiveAndEnabled
                && battleController != null
                && battle != null
                && battle.Phase == BattlePhase.PlayerPhase
                && !battleController.IsWaitingForVisuals
                && !_turnHandoffPending
                && battle.CurrentRound != null;
        }
    }

    private enum CardAnimationContext
    {
        Normal,
        Draw,
        Play,
        Discard
    }

    private enum TurnHandoffAction
    {
        EndPlayerPhase,
        Stand
    }

    private readonly struct CardLayoutSnapshot
    {
        public CardLayoutSnapshot(Card card, Vector3 worldPosition)
        {
            Card = card;
            WorldPosition = worldPosition;
        }

        public Card Card { get; }
        public Vector3 WorldPosition { get; }
    }

    private void Awake()
    {
        if (battleController == null)
            battleController = FindFirstObjectByType<BattleController>();

        if (runManager == null)
            runManager = FindFirstObjectByType<RunManager>();

        AutoBindLayout();
        ConfigureCardLayout(playerHandRoot);
        ConfigureCardLayout(opponentHandRoot);
        ConfigureCardLayout(devilPlayPileRoot);
        ConfigureCardLayout(sharedPlayPileRoot);
        ConfigureCardLayout(playerPlayPileRoot);
        ConfigureDragInteractionComponents();
        HidePlayerChoiceMoneyPreview();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);
        EventBus.Subscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);
        EventBus.Subscribe<ItemUsedEvent>(OnItemUsed);

        AddListeners();
        Refresh();
    }

    private void OnDisable()
    {
        CancelPendingRoundStart();
        CancelPendingTurnHandoff();
        HidePlayerChoiceMoneyPreview();
        SetActive(roundStartPanel, false);
        ClearGeneratedBattleCards();
        EventBus.Unsubscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);
        EventBus.Unsubscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Unsubscribe<BattleEndedEvent>(OnBattleEnded);
        EventBus.Unsubscribe<ItemUsedEvent>(OnItemUsed);

        RemoveListeners();
    }

    public void Refresh()
    {
        Refresh(CardAnimationContext.Normal);
    }

    public void RefreshForVisualCommand(VisualCommand command)
    {
        Refresh(GetAnimationContext(command.Type));
    }

    public IEnumerator WaitForCardAnimations()
    {
        for (int i = _cardTweens.Count - 1; i >= 0; i--)
        {
            if (_cardTweens[i] == null || !_cardTweens[i].IsActive())
                _cardTweens.RemoveAt(i);
        }

        while (_cardTweens.Count > 0)
        {
            for (int i = _cardTweens.Count - 1; i >= 0; i--)
            {
                Tween tween = _cardTweens[i];
                if (tween == null || !tween.IsActive() || !tween.IsPlaying())
                    _cardTweens.RemoveAt(i);
            }

            if (_cardTweens.Count > 0)
                yield return null;
        }
    }

    private void Refresh(CardAnimationContext animationContext)
    {
        AutoBindLayout();

        BattleState battle = battleController != null ? battleController.BattleState : null;
        RoundState round = battle?.CurrentRound;
        RefreshDevilAbilityTooltip(battle);
        CaptureHandSnapshots();

        if (battle == null)
        {
            CancelPendingRoundStart();
            ClearBattleCardViews();
            SetText(playerScoreText, " ");
            SetText(devilScoreText, " ");
            SetBurstThresholdText(null);
            SetPanels(false, false, false);
            SetTurnButtons(false);
            RememberRenderedCards(null);
            return;
        }

        bool shouldStartRound = CanScheduleRoundStart(battle);
        if (shouldStartRound)
            ScheduleRoundStart(battle);
        else
            CancelPendingRoundStart();

        SetScoreText(round);
        SetBurstThresholdText(round);

        RenderCards(_playerCards, playerHandRoot, round?.PlayerHand.Count ?? 0, round?.PlayerHand, true, CanSelectCards(battle), false);
        RenderCards(_opponentCards, opponentHandRoot, round?.OpponentHand.Count ?? 0, round?.OpponentHand, false, false, true);
        RenderPlayPile(round, _suppressRoundPilesUntilNextRound && battle.Phase == BattlePhase.Cleanup);
        AnimateCardChanges(round, animationContext);

        bool suppressBlockingPanels = battleController != null && battleController.InputGate != null;
        bool roundFinished = battle.Phase == BattlePhase.Cleanup;
        bool battleFinished = battle.Phase == BattlePhase.BattleEnd && !suppressBlockingPanels;
        SetPanels(shouldStartRound && !suppressBlockingPanels, roundFinished, battleFinished);

        if (runManager != null && runManager.RunState.Money > 0)
            backToMapButtonText.text = "다음 단계로";
        else backToMapButtonText.text = "메인 화면으로";

        RefreshRoundResult(battle);
        RefreshBattleResult(battle);
        SetTurnButtons(
            battle.Phase == BattlePhase.PlayerPhase
            && !battleController.IsWaitingForVisuals
            && !_turnHandoffPending);
        RememberRenderedCards(round);
    }

    public void ClearGeneratedBattleCards()
    {
        ClearBattleCardViews();
    }

    public bool TryPlayDraggedHandCard(int handIndex)
    {
        if (!CanPlayerAct)
            return false;

        if (!battleController.TryPlayCard(handIndex))
        {
            Refresh();
            return false;
        }

        ScheduleTurnHandoff(TurnHandoffAction.EndPlayerPhase);
        Refresh();
        return true;
    }

    public bool TryHitFromDraggedDeck()
    {
        if (!CanPlayerAct)
            return false;

        if (!battleController.TryHitWithoutEndingTurn())
        {
            Refresh();
            return false;
        }

        bool hit = ScheduleTurnHandoff(TurnHandoffAction.EndPlayerPhase);
        Refresh();
        return hit;
    }

    public bool TryCalculateCardPlayPreview(
        int handIndex,
        out ScoreResult scoreResult,
        out MoneyDeltaPreview moneyPreview)
    {
        scoreResult = default;
        moneyPreview = default;

        if (!CanPlayerAct)
            return false;

        BattleState battle = battleController.BattleState;
        RoundState round = battle?.CurrentRound;
        if (round == null
            || !round.CanPlacePlayedCard(Combatant.Player)
            || handIndex < 0
            || handIndex >= round.PlayerHand.Count
            || battleController.InputGate != null && !battleController.InputGate.CanPlayCard(battle, handIndex))
        {
            return false;
        }

        var previewPlayedCards = new List<Card>(round.PlayerPlayedCards.Count + 1);
        previewPlayedCards.AddRange(round.PlayerPlayedCards);
        previewPlayedCards.Add(battle.PreviewPlayerCardForPlay(round.PlayerHand[handIndex]));

        scoreResult = ScoreResolver.Resolve(
            previewPlayedCards,
            round.ScoringModifiers,
            round.PlayerBurstThreshold);

        var previewPokerCards = new List<Card>(previewPlayedCards.Count + round.SharedVisibleCards.Count);
        previewPokerCards.AddRange(previewPlayedCards);
        previewPokerCards.AddRange(round.SharedVisibleCards);
        moneyPreview = MoneyResolver.CalculatePlayerRealtimeMoneyPreview(
            battle,
            round,
            previewPokerCards,
            scoreResult);
        return true;
    }

    public void ShowPlayerChoiceMoneyPreview(ScoreResult scoreResult, MoneyDeltaPreview moneyPreview)
    {
        if (playerChoiceMoneyPreviewText == null)
            return;

        playerChoiceMoneyPreviewText.text = NumberFormatter.Abbreviate(moneyPreview.FinalDelta) + "$";
        SetPlayerChoiceMoneyPreviewAlpha(playerChoiceMoneyPreviewActiveAlpha);
    }

    public void HidePlayerChoiceMoneyPreview()
    {
        SetPlayerChoiceMoneyPreviewAlpha(0f);
    }

    public bool IsPointerOverPlayerPlayPile(PointerEventData eventData)
    {
        if (playerPlayPileRoot == null || eventData == null)
            return false;

        Camera camera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(playerPlayPileRoot, eventData.position, camera);
    }

    private void AddListeners()
    {
        if (standButton != null)
            standButton.onClick.AddListener(Stand);
        if (toShopButton != null)
            toShopButton.onClick.AddListener(OpenShop);
        if (nextRoundButton != null)
            nextRoundButton.onClick.AddListener(ContinueToNextRound);
        if (backToMapButton != null)
            backToMapButton.onClick.AddListener(ReturnToMap);
    }

    private void RemoveListeners()
    {
        Remove(standButton, Stand);
        Remove(toShopButton, OpenShop);
        Remove(nextRoundButton, ContinueToNextRound);
        Remove(backToMapButton, ReturnToMap);
    }

    private static void Remove(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }

    private void Stand()
    {
        ScheduleTurnHandoff(TurnHandoffAction.Stand);
        Refresh();
    }

    private void Hit()
    {
        TryHitFromDraggedDeck();
    }

    private void OpenShop()
    {
        runManager?.OpenShop();
        Refresh();
    }

    private void ContinueToNextRound()
    {
        runManager.ContinueImmediatelyAfterRound();
        Refresh();
    }

    private void ReturnToMap()
    {
        ClearBattleCardViews();
        battleController?.CompletePendingVisualTransition();
        if (runManager != null && runManager.RunState.Money > 0)
        {
            backToMapButtonText.text = "다음 단계로";
            runManager?.ReturnToMap();
        }
        else
        {
            backToMapButtonText.text = "메인 화면으로";
            SceneTransitionManager.Instance?.LoadScene("MenuScene");
        }
        
        Refresh();
    }

    public void ShowPile()
    {
        BattleState battle = battleController?.BattleState;
        if (battle == null)
            return;

        var discardedCards = new List<Card>();
        discardedCards.AddRange(battle.PlayerDiscardPile);
        // if (battle.CurrentRound != null)
        //     discardedCards.AddRange(battle.CurrentRound.PlayerPlayedCards);  // played cards should be visible on the game view, not in the deck view

        DeckViewOptions options = DeckViewOptions.Default;
        options.Flags |= DeckViewFlags.ShowDiscardedCards;
        deckViewPanel?.Show(battle.PlayerDrawPile, discardedCards, options);
    }

    private void OnRunPhaseChanged(RunPhaseChangedEvent eventData)
    {
        if (eventData.Phase != RunPhase.Battle && eventData.Phase != RunPhase.Shop)
            ClearBattleCardViews();

        Refresh();
    }

    private void OnBattleStarted(BattleStartedEvent eventData)
    {
        CancelPendingTurnHandoff();
        _suppressRoundPilesUntilNextRound = false;
        ScheduleRoundStart(battleController?.BattleState);
        Refresh();
    }

    private void OnBattleEnded(BattleEndedEvent eventData)
    {
        CancelPendingRoundStart();
        CancelPendingTurnHandoff();
        ClearBattleCardViews();
        Refresh();
    }

    private bool ScheduleTurnHandoff(TurnHandoffAction action)
    {
        if (!CanPlayerAct || _turnHandoffPending)
            return false;

        BattleState battle = battleController.BattleState;
        RoundState round = battle.CurrentRound;
        if (round == null)
            return false;

        _turnHandoffPending = true;
        _scheduledTurnBattle = battle;
        _scheduledTurnRound = round;
        _turnHandoffRoutine = StartCoroutine(RunTurnHandoff(battle, round, action));
        return true;
    }

    private IEnumerator RunTurnHandoff(BattleState battle, RoundState round, TurnHandoffAction action)
    {
        do
        {
            float delaySeconds = SampleOpponentTurnDelay();
            if (delaySeconds > 0f)
                yield return new WaitForSeconds(delaySeconds);
            else
                yield return null;

            if (!CanExecuteTurnHandoff(battle, round))
                break;

            bool actionSucceeded = ExecuteTurnHandoff(action);
            Refresh();
            if (!actionSucceeded || action != TurnHandoffAction.Stand || !ShouldContinueAutoStand(battle, round))
                break;
        }
        while (true);

        CompleteTurnHandoff(battle, round);
    }

    private bool ExecuteTurnHandoff(TurnHandoffAction action)
    {
        if (battleController == null)
            return false;

        switch (action)
        {
            case TurnHandoffAction.EndPlayerPhase:
                battleController.EndPlayerPhase();
                return true;
            case TurnHandoffAction.Stand:
                return battleController.TryStand();
            default:
                return false;
        }
    }

    private bool CanExecuteTurnHandoff(BattleState battle, RoundState round)
    {
        return isActiveAndEnabled
            && battleController != null
            && ReferenceEquals(battleController.BattleState, battle)
            && battle != null
            && ReferenceEquals(battle.CurrentRound, round)
            && round != null
            && battle.Phase == BattlePhase.PlayerPhase
            && !battleController.IsWaitingForVisuals;
    }

    private bool ShouldContinueAutoStand(BattleState battle, RoundState round)
    {
        return CanExecuteTurnHandoff(battle, round)
            && round.PlayerStood
            && !round.OpponentStood;
    }

    private float SampleOpponentTurnDelay()
    {
        GetNormalizedOpponentTurnDelayRange(out float minimum, out float maximum);
        return minimum >= maximum ? minimum : UnityEngine.Random.Range(minimum, maximum);
    }

    private void GetNormalizedOpponentTurnDelayRange(out float minimum, out float maximum)
    {
        minimum = Mathf.Max(0f, Mathf.Min(opponentTurnDelayMinSeconds, opponentTurnDelayMaxSeconds));
        maximum = Mathf.Max(0f, Mathf.Max(opponentTurnDelayMinSeconds, opponentTurnDelayMaxSeconds));
    }

    private void CompleteTurnHandoff(BattleState battle, RoundState round)
    {
        if (!ReferenceEquals(_scheduledTurnBattle, battle) || !ReferenceEquals(_scheduledTurnRound, round))
            return;

        _turnHandoffRoutine = null;
        _scheduledTurnBattle = null;
        _scheduledTurnRound = null;
        _turnHandoffPending = false;
        Refresh();
    }

    private void CancelPendingTurnHandoff()
    {
        if (_turnHandoffRoutine != null)
            StopCoroutine(_turnHandoffRoutine);

        _turnHandoffRoutine = null;
        _scheduledTurnBattle = null;
        _scheduledTurnRound = null;
        _turnHandoffPending = false;
    }

    private void OnItemUsed(ItemUsedEvent eventData)
    {
        Refresh();
    }

    private bool CanScheduleRoundStart(BattleState battle)
    {
        return isActiveAndEnabled
            && battleController != null
            && ReferenceEquals(battleController.BattleState, battle)
            && battle != null
            && !battle.IsBattleOver
            && !battleController.IsWaitingForVisuals
            && battle.Phase == BattlePhase.PreRound
            && battle.CurrentRound == null;
    }

    private void ScheduleRoundStart(BattleState battle)
    {
        if (!CanScheduleRoundStart(battle))
            return;

        if (_roundStartRoutine != null && ReferenceEquals(_scheduledRoundBattle, battle))
            return;

        CancelPendingRoundStart();
        _scheduledRoundBattle = battle;
        _roundStartRoutine = StartCoroutine(StartRoundAfterDelay(battle));
    }

    private IEnumerator StartRoundAfterDelay(BattleState battle)
    {
        if (roundStartDelaySeconds > 0f)
            yield return new WaitForSeconds(roundStartDelaySeconds);
        else
            yield return null;

        if (!CanScheduleRoundStart(battle))
        {
            _roundStartRoutine = null;
            _scheduledRoundBattle = null;
            Refresh();
            yield break;
        }

        _pendingWager = battle.GetDefaultWager();
        _suppressRoundPilesUntilNextRound = false;

        _roundStartRoutine = null;
        _scheduledRoundBattle = null;
        battleController.StartNextRound(_pendingWager);
        Refresh();
    }

    private void CancelPendingRoundStart()
    {
        if (_roundStartRoutine != null)
            StopCoroutine(_roundStartRoutine);

        _roundStartRoutine = null;
        _scheduledRoundBattle = null;
    }

    private void RefreshRoundResult(BattleState battle)
    {
        if (roundResultText == null || battle == null || battle.CombatHistory.Count == 0)
            return;

        RoundResolution resolution = battle.CombatHistory[battle.CombatHistory.Count - 1];
        string result = resolution.Winner == Combatant.Player ? "라운드 승리" : resolution.Winner == Combatant.Opponent ? "라운드 패배" : "무승부";
        SetText(roundResultText, result);
    }

    private void RefreshBattleResult(BattleState battle)
    {
        if (battle == null || battle.Phase != BattlePhase.BattleEnd)
            return;

        ClearChildren(rewardViewRoot);
    }

    private void SetPanels(bool showRoundStart, bool showRoundResult, bool showBattleResult)
    {
        SetActive(roundStartPanel, showRoundStart);
        SetActive(roundResultPanel, showRoundResult);
        SetActive(battleResultPanel, showBattleResult);
    }

    private void SetTurnButtons(bool playerTurn)
    {
        if (standButton != null)
            standButton.interactable = playerTurn;
    }

    private static CardAnimationContext GetAnimationContext(VisualCommandType commandType)
    {
        return commandType switch
        {
            VisualCommandType.CardsDrawn => CardAnimationContext.Draw,
            VisualCommandType.CardsPlayed => CardAnimationContext.Play,
            VisualCommandType.RoundEnded => CardAnimationContext.Discard,
            _ => CardAnimationContext.Normal
        };
    }

    private void CaptureHandSnapshots()
    {
        _previousPlayerHand.Clear();
        _previousOpponentHand.Clear();
        CaptureHandSnapshot(_previousPlayerHand, _lastPlayerHandCards, _playerCards);
        CaptureHandSnapshot(_previousOpponentHand, _lastOpponentHandCards, _opponentCards);
    }

    private static void CaptureHandSnapshot(List<CardLayoutSnapshot> snapshots, IReadOnlyList<Card> cards, IReadOnlyList<BattleUiCardView> views)
    {
        int count = Mathf.Min(cards.Count, views.Count);
        for (int i = 0; i < count; i++)
        {
            BattleUiCardView view = views[i];
            if (view == null || !view.gameObject.activeSelf || view.RectTransform == null)
                continue;

            snapshots.Add(new CardLayoutSnapshot(cards[i], view.RectTransform.position));
        }
    }

    private void AnimateCardChanges(RoundState round, CardAnimationContext animationContext)
    {
        if (round == null)
            return;

        if (animationContext == CardAnimationContext.Discard)
        {
            AnimatePileCardsToDiscard(_devilPlayPileCards);
            AnimatePileCardsToDiscard(_sharedPlayPileCards);
            AnimatePileCardsToDiscard(_playerPlayPileCards);
            _suppressRoundPilesUntilNextRound = true;
            return;
        }

        bool animateDraws = animationContext == CardAnimationContext.Draw || animationContext == CardAnimationContext.Normal;
        bool animatePlays = animationContext == CardAnimationContext.Play || animationContext == CardAnimationContext.Normal;

        if (animateDraws)
        {
            AnimateDrawnCards(_playerCards, round.PlayerHand, _lastPlayerHandCards);
            AnimateDrawnCards(_opponentCards, round.OpponentHand, _lastOpponentHandCards);
        }

        if (animatePlays)
        {
            AnimatePlayedCards(_playerPlayPileCards, round.PlayerPlayedCards, _lastPlayerPileCards, _previousPlayerHand);
            AnimatePlayedCards(_devilPlayPileCards, round.OpponentVisibleCards, _lastOpponentPileCards, _previousOpponentHand);
            AnimateDrawnCards(_sharedPlayPileCards, round.SharedVisibleCards, _lastSharedPileCards);
        }
    }

    private void AnimateDrawnCards(IReadOnlyList<BattleUiCardView> views, IReadOnlyList<Card> currentCards, IReadOnlyList<Card> previousCards)
    {
        if (currentCards == null || currentCards.Count <= previousCards.Count)
            return;

        for (int i = previousCards.Count; i < currentCards.Count && i < views.Count; i++)
            AnimateCardFromOffset(views[i], cardDrawStartOffset, cardDrawDurationSeconds, cardDrawEase);
    }

    private void AnimatePlayedCards(IReadOnlyList<BattleUiCardView> views, IReadOnlyList<Card> currentCards, IReadOnlyList<Card> previousCards, List<CardLayoutSnapshot> sourceSnapshots)
    {
        if (currentCards == null || currentCards.Count <= previousCards.Count)
            return;

        for (int i = previousCards.Count; i < currentCards.Count && i < views.Count; i++)
        {
            BattleUiCardView view = views[i];
            if (TryTakeSourceSnapshot(sourceSnapshots, currentCards[i], out Vector3 sourceWorldPosition))
                AnimateCardFromWorldPosition(view, sourceWorldPosition);
            else
                AnimateCardFromOffset(view, cardDrawStartOffset, cardPlayDurationSeconds, cardPlayEase);
        }
    }

    private static bool TryTakeSourceSnapshot(List<CardLayoutSnapshot> snapshots, Card card, out Vector3 worldPosition)
    {
        for (int i = 0; i < snapshots.Count; i++)
        {
            if (!EqualityComparer<Card>.Default.Equals(snapshots[i].Card, card))
                continue;

            worldPosition = snapshots[i].WorldPosition;
            snapshots.RemoveAt(i);
            return true;
        }

        worldPosition = default;
        return false;
    }

    private void AnimateCardFromOffset(BattleUiCardView view, Vector2 startOffset, float durationSeconds, Ease ease)
    {
        RectTransform rect = GetCardRect(view);
        if (rect == null)
            return;

        Vector2 targetPosition = rect.anchoredPosition;
        rect.DOKill();
        rect.anchoredPosition = targetPosition + startOffset;
        TrackTween(rect.DOAnchorPos(targetPosition, Mathf.Max(0.01f, durationSeconds)).SetEase(ease));
    }

    private void AnimateCardFromWorldPosition(BattleUiCardView view, Vector3 sourceWorldPosition)
    {
        RectTransform rect = GetCardRect(view);
        RectTransform parent = rect != null ? rect.parent as RectTransform : null;
        if (rect == null || parent == null)
            return;

        Vector2 targetPosition = rect.anchoredPosition;
        rect.DOKill();
        rect.anchoredPosition = WorldToAnchoredPosition(parent, sourceWorldPosition);
        TrackTween(rect.DOJumpAnchorPos(targetPosition, cardPlayJumpPower, 1, Mathf.Max(0.01f, cardPlayDurationSeconds)).SetEase(cardPlayEase));
    }

    private void AnimatePileCardsToDiscard(IReadOnlyList<BattleUiCardView> views)
    {
        for (int i = 0; i < views.Count; i++)
        {
            RectTransform rect = GetCardRect(views[i]);
            if (rect == null || !views[i].gameObject.activeSelf)
                continue;

            rect.DOKill();
            TrackTween(rect.DOAnchorPos(rect.anchoredPosition + cardDiscardEndOffset, Mathf.Max(0.01f, cardDiscardDurationSeconds)).SetEase(cardDiscardEase));
        }
    }

    private static RectTransform GetCardRect(BattleUiCardView view)
    {
        return view != null && view.gameObject.activeSelf ? view.RectTransform : null;
    }

    private static Vector2 WorldToAnchoredPosition(RectTransform parent, Vector3 worldPosition)
    {
        Camera camera = null;
        Canvas canvas = parent.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            camera = canvas.worldCamera;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, camera, out Vector2 localPoint);
        return localPoint;
    }

    private void TrackTween(Tween tween)
    {
        if (tween == null)
            return;

        _cardTweens.Add(tween);
        tween.OnComplete(() => _cardTweens.Remove(tween));
        tween.OnKill(() => _cardTweens.Remove(tween));
    }

    private void RememberRenderedCards(RoundState round)
    {
        CopyCards(_lastPlayerHandCards, round?.PlayerHand);
        CopyCards(_lastOpponentHandCards, round?.OpponentHand);
        CopyCards(_lastPlayerPileCards, round?.PlayerPlayedCards);
        CopyCards(_lastOpponentPileCards, round?.OpponentVisibleCards);
        CopyCards(_lastSharedPileCards, round?.SharedVisibleCards);
    }

    private static void CopyCards(List<Card> target, IReadOnlyList<Card> source)
    {
        target.Clear();
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
            target.Add(source[i]);
    }

    private void RenderPlayPile(RoundState round, bool suppressVisibleCards)
    {
        RenderStaticPile(_devilPlayPileCards, devilPlayPileRoot, suppressVisibleCards ? null : round?.OpponentVisibleCards);
        RenderStaticPile(_sharedPlayPileCards, sharedPlayPileRoot, suppressVisibleCards ? null : round?.SharedVisibleCards);
        RenderStaticPile(_playerPlayPileCards, playerPlayPileRoot, suppressVisibleCards ? null : round?.PlayerPlayedCards);
    }

    private void RenderStaticPile(List<BattleUiCardView> views, RectTransform root, IReadOnlyList<Card> cards)
    {
        int count = cards?.Count ?? 0;
        EnsureCardViews(views, root, count, false);

        for (int i = 0; i < views.Count; i++)
        {
            bool active = i < count;
            views[i].gameObject.SetActive(active);

            if (active)
                BindCard(views[i], cards[i], true, false, -1);
        }

        if (root != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    private void RenderCards(List<BattleUiCardView> views, RectTransform root, int count, IReadOnlyList<Card> cards, bool faceUp, bool interactable, bool backOnly)
    {
        EnsureCardViews(views, root, count, interactable);

        for (int i = 0; i < views.Count; i++)
        {
            bool active = i < count;
            views[i].gameObject.SetActive(active);

            if (!active)
                continue;

            if (backOnly)
                views[i].BindBack(assetRegistry != null ? assetRegistry.CardBack : null);
            else if (cards != null && i < cards.Count)
                BindCard(views[i], cards[i], faceUp, interactable, i);

        }

        if (root != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    private void BindCard(BattleUiCardView view, Card card, bool faceUp, bool interactable, int handIndex)
    {
        Sprite sprite = faceUp && assetRegistry != null ? assetRegistry.GetCardFront(card) : assetRegistry != null ? assetRegistry.CardBack : null;
        view.Bind(card, sprite, faceUp, interactable, assetRegistry);

        if (view.Button == null)
            return;

        view.Button.onClick.RemoveAllListeners();
        ConfigureHandCardDragHandler(view, interactable, handIndex);
    }

    private void ConfigureHandCardDragHandler(BattleUiCardView view, bool interactable, int handIndex)
    {
        if (view == null)
            return;

        BattleHandCardDragHandler dragHandler = view.GetComponent<BattleHandCardDragHandler>();
        if (dragHandler == null)
            dragHandler = view.gameObject.AddComponent<BattleHandCardDragHandler>();

        dragHandler.Configure(interactable ? this : null, interactable ? handIndex : -1);
    }

    private void EnsureCardViews(List<BattleUiCardView> views, RectTransform root, int count, bool withButton)
    {
        if (root == null)
            return;

        int previousCount = views.Count;
        while (views.Count < count)
        {
            BattleUiCardView view = CreateCardView(root, withButton);
            if (view == null)
                break;

            views.Add(view);
        }

        if (withButton)
        {
            for (int i = 0; i < count && i < views.Count; i++)
                views[i].EnsureButton();
        }

        if (views.Count != previousCount)
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    private BattleUiCardView CreateCardView(RectTransform parent, bool withButton)
    {
        BattleUiCardView prefab = ResolveCardPrefab();
        if (prefab == null)
            return null;

        BattleUiCardView view = Instantiate(prefab, parent, false);
        view.name = "Card";
        ConfigureCardInstance(view);
        if (withButton)
            view.EnsureButton();

        return view;
    }

    private bool CanSelectCards(BattleState battle)
    {
        return battle.Phase == BattlePhase.PlayerPhase
            && !battleController.IsWaitingForVisuals
            && !_turnHandoffPending
            && battle.CurrentRound != null;
    }

    private void OnValidate()
    {
        GetNormalizedOpponentTurnDelayRange(out float minimum, out float maximum);
        opponentTurnDelayMinSeconds = minimum;
        opponentTurnDelayMaxSeconds = maximum;
        playerChoiceMoneyPreviewActiveAlpha = Mathf.Clamp01(playerChoiceMoneyPreviewActiveAlpha);
    }

    private void SetScoreText(RoundState round)
    {
        SetText(playerScoreText, round != null && round.PlayerHasPlayed ? round.PlayerScore.BlackjackScore.ToString() : "-");
        SetText(devilScoreText, round != null && round.OpponentHasPlayed ? round.OpponentScore.BlackjackScore.ToString() : "-");
    }

    private void SetBurstThresholdText(RoundState round)
    {
        SetText(playerBurstThresholdText, round != null ? round.PlayerBurstThreshold.ToString() : "-");
        SetText(devilBurstThresholdText, round != null ? round.OpponentBurstThreshold.ToString() : "-");
    }

    private void AutoBindLayout()
    {
        devilAbilityTooltipTrigger ??= FindDescendantComponent<TooltipTrigger>("DevilAbilityInfo");
        playerScoreText ??= FindDescendantComponent<TMP_Text>("PlayerScore");
        devilScoreText ??= FindDescendantComponent<TMP_Text>("DevilScore");
        playerBurstThresholdText ??= FindDescendantComponent<TMP_Text>("PlayerThreshold");
        devilBurstThresholdText ??= FindDescendantComponent<TMP_Text>("DevilThreshold");
        playerHandRoot ??= FindDescendantRect("PlayerHand");
        opponentHandRoot ??= FindDescendantRect("DevilHand");
        devilPlayPileRoot ??= FindDescendantRect("DevilPlayPile");
        sharedPlayPileRoot ??= FindDescendantRect("SharedPlayPile");
        playerPlayPileRoot ??= FindDescendantRect("PlayerPlayPile");
        playPileRoot ??= FindDescendantRect("PlayPile");
        devilPlayPileRoot ??= playPileRoot;
        sharedPlayPileRoot ??= playPileRoot;
        playerPlayPileRoot ??= playPileRoot;
        standButton ??= FindDescendantComponent<Button>("StandButton");
        roundStartPanel ??= FindDescendant("RoundStartPanel");
        deckViewPanel ??= FindFirstObjectByType<DeckViewPanel>(FindObjectsInactive.Include);
        viewPileButton ??= FindDescendantComponent<Button>("ViewPileButton");
        viewPileButton ??= FindDescendantComponent<Button>("ViewDrawPileButton");
        playerHandImage ??= FindDescendantRect("PlayerHandImage");
        playerChoiceMoneyPreviewText ??= FindDescendantComponent<TMP_Text>("PlayerChoiceMoneyPreview");
        devilStandIndicator ??= FindOrAddDescendantComponent<DevilStandIndicator>("DevilStandIndicator");
        ConfigureDragInteractionComponents();
        roundResultPanel ??= FindDescendant("RoundResultPanel");
        roundResultText ??= FindDescendantComponent<TMP_Text>("RoundResultText");
        toShopButton ??= FindDescendantComponent<Button>("ToShopButton");
        nextRoundButton ??= FindDescendantComponent<Button>("NextRoundButton");
        battleResultPanel ??= FindDescendant("BattleResultPanel");
        rewardViewRoot ??= FindDescendantRect("RewardViewRoot");
        backToMapButton ??= FindDescendantComponent<Button>("BackToMapButton");
    }

    private void ConfigureDragInteractionComponents()
    {
        if (playerHandImage != null)
        {
            _playerHandHitDragHandler ??= playerHandImage.GetComponent<PlayerHandHitDragHandler>();
            if (_playerHandHitDragHandler == null)
                _playerHandHitDragHandler = playerHandImage.gameObject.AddComponent<PlayerHandHitDragHandler>();

            _playerHandHitDragHandler.Configure(this);
        }

        if (viewPileButton != null)
        {
            _deckViewDragStarter ??= viewPileButton.GetComponent<DeckViewLongPressDragStarter>();
            if (_deckViewDragStarter == null)
                _deckViewDragStarter = viewPileButton.gameObject.AddComponent<DeckViewLongPressDragStarter>();

            _deckViewDragStarter.Configure(this, _playerHandHitDragHandler);
        }

        devilStandIndicator?.Configure(battleController);
    }

    private void RefreshDevilAbilityTooltip(BattleState battle)
    {
        if (devilAbilityTooltipTrigger == null)
            return;

        if (battle == null || battle.IsBattleOver || battle.Config == null)
        {
            devilAbilityTooltipTrigger.Clear();
            return;
        }

        devilAbilityTooltipTrigger.Bind(battle.Config.EncounterId, battle.Config.DevilId);
    }

    private GameObject FindDescendant(string objectName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == objectName)
                return children[i].gameObject;
        }

        return null;
    }

    private RectTransform FindDescendantRect(string objectName)
    {
        return FindDescendant(objectName)?.GetComponent<RectTransform>();
    }

    private T FindDescendantComponent<T>(string objectName) where T : Component
    {
        return FindDescendant(objectName)?.GetComponent<T>();
    }

    private T FindOrAddDescendantComponent<T>(string objectName) where T : Component
    {
        GameObject target = FindDescendant(objectName);
        if (target == null)
            return null;

        return target.GetComponent<T>() ?? target.AddComponent<T>();
    }

    private static TMP_Text FindFirstTextUnder(GameObject root, string preferredName)
    {
        if (root == null)
            return null;

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == preferredName)
                return texts[i];
        }

        return texts.Length > 0 ? texts[0] : null;
    }

    private BattleUiCardView ResolveCardPrefab()
    {
        if (cardPrefab != null)
            return cardPrefab;

        BattleUiCardView[] candidates = Resources.FindObjectsOfTypeAll<BattleUiCardView>();
        for (int i = 0; i < candidates.Length; i++)
        {
            BattleUiCardView candidate = candidates[i];
            if (candidate != null && candidate.name == "CardPrefab" && !candidate.gameObject.scene.IsValid())
            {
                cardPrefab = candidate;
                return cardPrefab;
            }
        }

        Debug.LogWarning($"{nameof(BattleUiPresenter)} is missing a card prefab reference.", this);
        return null;
    }

    private static void ConfigureCardInstance(BattleUiCardView view)
    {
        RectTransform rect = view.RectTransform;
        if (rect != null)
            rect.sizeDelta = new Vector2(90f, 126f);

        LayoutElement layout = view.GetComponent<LayoutElement>();
        if (layout == null)
            layout = view.gameObject.AddComponent<LayoutElement>();

        //layout.minWidth = 90f;
        //layout.minHeight = 126f;
        layout.preferredWidth = 90f;
        layout.preferredHeight = 126f;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;
    }

    private static void ConfigureCardLayout(RectTransform root)
    {
        if (root == null)
            return;

        // HorizontalLayoutGroup horizontal = root.GetComponent<HorizontalLayoutGroup>();
        // if (horizontal != null)
        // {
        //     horizontal.childControlWidth = true;
        //     horizontal.childControlHeight = true;
        //     horizontal.childForceExpandWidth = true;
        //     horizontal.childForceExpandHeight = true;
        //     horizontal.childScaleWidth = false;
        //     horizontal.childScaleHeight = false;
        // }

        GridLayoutGroup grid = root.GetComponent<GridLayoutGroup>();
        if (grid != null)
            grid.cellSize = new Vector2(90f, 126f);
    }

    private static void ConfigureCardLayoutGroups(RectTransform root)
    {
        if (root == null)
            return;

        ConfigureCardLayout(root);
        for (int i = 0; i < root.childCount; i++)
            ConfigureCardLayout(root.GetChild(i).GetComponent<RectTransform>());
    }

    private static void RebuildCardLayoutGroups(RectTransform root)
    {
        if (root == null)
            return;

        for (int i = 0; i < root.childCount; i++)
        {
            RectTransform child = root.GetChild(i).GetComponent<RectTransform>();
            if (child != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(child);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }

    private void SetPlayerChoiceMoneyPreviewAlpha(float alpha)
    {
        if (playerChoiceMoneyPreviewText == null)
            return;

        Color color = playerChoiceMoneyPreviewText.color;
        color.a = Mathf.Clamp01(alpha);
        playerChoiceMoneyPreviewText.color = color;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private static void ClearChildren(RectTransform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
            DestroyGeneratedObject(root.GetChild(i).gameObject);
    }

    private void ClearBattleCardViews()
    {
        KillCardTweens();
        DestroyCardViews(_playerCards);
        DestroyCardViews(_opponentCards);
        DestroyCardViews(_devilPlayPileCards);
        DestroyCardViews(_sharedPlayPileCards);
        DestroyCardViews(_playerPlayPileCards);
        ClearChildren(playerHandRoot);
        ClearChildren(opponentHandRoot);
        ClearChildren(devilPlayPileRoot);
        ClearChildren(sharedPlayPileRoot);
        ClearChildren(playerPlayPileRoot);
        _suppressRoundPilesUntilNextRound = false;
        RememberRenderedCards(null);
    }

    private static void DestroyCardViews(List<BattleUiCardView> views)
    {
        for (int i = views.Count - 1; i >= 0; i--)
        {
            if (views[i] != null)
                DestroyGeneratedObject(views[i].gameObject);
        }

        views.Clear();
    }

    private static void DestroyGeneratedObject(GameObject target)
    {
        if (target == null)
            return;

        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect != null)
            rect.DOKill();

        DestroyImmediate(target);
    }

    private void KillCardTweens()
    {
        for (int i = _cardTweens.Count - 1; i >= 0; i--)
            _cardTweens[i]?.Kill();

        _cardTweens.Clear();
    }
}
