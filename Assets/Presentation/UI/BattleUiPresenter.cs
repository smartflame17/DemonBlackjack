using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

// This component is responsible for battle UI presentation - Runtime monobehaviour binding.
public sealed class BattleUiPresenter : MonoBehaviour
{
    [SerializeField] private BattleController battleController;
    [SerializeField] private RunManager runManager;
    [SerializeField] private GameplayAssetRegistry assetRegistry;

    [Header("Main")]
    [SerializeField] private BattleUiCardView cardPrefab;
    [SerializeField] private Image devilImage;
    [SerializeField] private TMP_Text playerScoreText;
    [SerializeField] private TMP_Text devilScoreText;
    [SerializeField] private TMP_Text roundWagerText;
    [SerializeField] private TMP_Text devilMoneyText;
    [SerializeField] private RectTransform playerHandRoot;
    [SerializeField] private RectTransform opponentHandRoot;
    [SerializeField] private RectTransform devilPlayPileRoot;
    [SerializeField] private RectTransform sharedPlayPileRoot;
    [SerializeField] private RectTransform playerPlayPileRoot;
    [SerializeField] private RectTransform playPileRoot;
    [SerializeField] private Button playButton;
    [SerializeField] private Button standButton;
    [SerializeField] private Button hitButton;

    [Header("Wager")]
    [SerializeField] private GameObject wagerPanel;
    [SerializeField] private GameObject playerProposalRoot;
    [SerializeField] private GameObject devilOfferRoot;
    [SerializeField] private Button incrementWagerButton;
    [SerializeField] private Button decrementWagerButton;
    [SerializeField] private Button proposalButton;
    [SerializeField] private Button acceptOfferButton;
    [SerializeField] private Button declineOfferButton;
    [SerializeField] private TMP_Text proposalAmountText;
    [SerializeField] private TMP_Text devilOfferAmountText;

    [Header("Deck View")]
    [SerializeField] private DeckViewPanel deckViewPanel;
    [SerializeField] private Button viewDrawPileButton;
    [SerializeField] private Button viewPlayedPileButton;

    [Header("Results")]
    [SerializeField] private GameObject roundResultPanel;
    [SerializeField] private TMP_Text roundResultText;
    [FormerlySerializedAs("nextRoundButton")]
    [SerializeField] private Button toShopButton;
    [SerializeField] private GameObject battleResultPanel;
    [SerializeField] private RectTransform rewardViewRoot;
    [SerializeField] private Button backToMapButton;

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
    private readonly HashSet<int> _selectedHandIndices = new();
    private readonly List<CardLayoutSnapshot> _previousPlayerHand = new();
    private readonly List<CardLayoutSnapshot> _previousOpponentHand = new();
    private readonly List<Card> _lastPlayerHandCards = new();
    private readonly List<Card> _lastOpponentHandCards = new();
    private readonly List<Card> _lastPlayerPileCards = new();
    private readonly List<Card> _lastOpponentPileCards = new();
    private readonly List<Card> _lastSharedPileCards = new();
    private readonly List<Tween> _cardTweens = new();

    private int _pendingWager = 10;
    private bool _wagerOpen;
    private bool _suppressRoundPilesUntilNextRound;

    private enum CardAnimationContext
    {
        Normal,
        Draw,
        Play,
        Discard
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
    }

    private void OnEnable()
    {
        EventBus.Subscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);
        EventBus.Subscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);

        AddListeners();
        Refresh();
    }

    private void OnDisable()
    {
        ClearGeneratedBattleCards();
        EventBus.Unsubscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);
        EventBus.Unsubscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Unsubscribe<BattleEndedEvent>(OnBattleEnded);

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
        CaptureHandSnapshots();

        if (battle == null)
        {
            ClearBattleCardViews();
            SetText(playerScoreText, "-");
            SetText(devilScoreText, "-");
            SetText(roundWagerText, "0");
            SetText(devilMoneyText, "Devil $0");
            SetPanels(false, false, false);
            SetTurnButtons(false, false);
            RememberRenderedCards(null);
            return;
        }

        if (devilImage != null && assetRegistry != null)
            devilImage.sprite = assetRegistry.GetDevilSprite(battle.Config.DevilId);

        if (battle.Phase == BattlePhase.PreRound && (battle.CurrentRound == null || !battle.CurrentRound.WagerCommitted) && !_wagerOpen)
        {
            OpenWagerPanel();
            round = battle.CurrentRound;
        }

        SetText(devilMoneyText, $"Devil ${battle.OpponentMoney}");
        SetScoreText(round);
        SetText(roundWagerText, round != null ? round.Pot.ToString() : "0");

        RenderCards(_playerCards, playerHandRoot, round?.PlayerHand.Count ?? 0, round?.PlayerHand, true, CanSelectCards(battle), false);
        RenderCards(_opponentCards, opponentHandRoot, round?.OpponentHand.Count ?? 0, round?.OpponentHand, false, false, true);
        RenderPlayPile(round, _suppressRoundPilesUntilNextRound && battle.Phase == BattlePhase.Cleanup);
        AnimateCardChanges(round, animationContext);

        bool roundFinished = battle.Phase == BattlePhase.Cleanup;
        bool battleFinished = battle.Phase == BattlePhase.BattleEnd;
        bool showWager = _wagerOpen && battle.Phase == BattlePhase.PreRound && round != null && !round.WagerCommitted && !battleFinished;
        SetPanels(showWager, roundFinished, battleFinished);
        RefreshWagerPanel(battle);
        RefreshRoundResult(battle);
        RefreshBattleResult(battle);
        SetTurnButtons(battle.Phase == BattlePhase.PlayerPhase && !battleController.IsWaitingForVisuals, _selectedHandIndices.Count > 0);
        RememberRenderedCards(round);
    }

    public void ClearGeneratedBattleCards()
    {
        ClearBattleCardViews();
    }

    private void AddListeners()
    {
        if (playButton != null)
            playButton.onClick.AddListener(PlaySelectedCards);
        if (standButton != null)
            standButton.onClick.AddListener(Stand);
        if (hitButton != null)
            hitButton.onClick.AddListener(Hit);
        if (incrementWagerButton != null)
            incrementWagerButton.onClick.AddListener(IncrementWager);
        if (decrementWagerButton != null)
            decrementWagerButton.onClick.AddListener(DecrementWager);
        if (proposalButton != null)
            proposalButton.onClick.AddListener(ProposeWager);
        if (acceptOfferButton != null)
            acceptOfferButton.onClick.AddListener(AcceptDevilOffer);
        if (declineOfferButton != null)
            declineOfferButton.onClick.AddListener(DeclineDevilOffer);
        if (viewDrawPileButton != null)
            viewDrawPileButton.onClick.AddListener(ShowDrawPile);
        if (viewPlayedPileButton != null)
            viewPlayedPileButton.onClick.AddListener(ShowPlayedPile);
        if (toShopButton != null)
            toShopButton.onClick.AddListener(OpenShop);
        if (backToMapButton != null)
            backToMapButton.onClick.AddListener(ReturnToMap);
    }

    private void RemoveListeners()
    {
        Remove(playButton, PlaySelectedCards);
        Remove(standButton, Stand);
        Remove(hitButton, Hit);
        Remove(incrementWagerButton, IncrementWager);
        Remove(decrementWagerButton, DecrementWager);
        Remove(proposalButton, ProposeWager);
        Remove(acceptOfferButton, AcceptDevilOffer);
        Remove(declineOfferButton, DeclineDevilOffer);
        Remove(viewDrawPileButton, ShowDrawPile);
        Remove(viewPlayedPileButton, ShowPlayedPile);
        Remove(toShopButton, OpenShop);
        Remove(backToMapButton, ReturnToMap);
    }

    private static void Remove(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }

    private void ToggleCardSelection(int handIndex)
    {
        if (!_selectedHandIndices.Add(handIndex))
        {
            _selectedHandIndices.Remove(handIndex);
        }
        else
        {
            _selectedHandIndices.Clear();
            _selectedHandIndices.Add(handIndex);
        }

        Refresh();
    }

    private void PlaySelectedCards()
    {
        if (battleController == null || _selectedHandIndices.Count == 0)
            return;

        int selectedIndex = -1;
        foreach (int handIndex in _selectedHandIndices)
        {
            selectedIndex = handIndex;
            break;
        }

        if (selectedIndex < 0 || !battleController.TryPlayCard(selectedIndex))
        {
            _selectedHandIndices.Clear();
            Refresh();
            return;
        }

        _selectedHandIndices.Clear();
        battleController.EndPlayerPhase();
        Refresh();
    }

    private void Stand()
    {
        _selectedHandIndices.Clear();
        battleController?.TryStand();
        Refresh();
    }

    private void Hit()
    {
        _selectedHandIndices.Clear();
        battleController?.TryHit();
        Refresh();
    }

    private void IncrementWager()
    {
        BattleState battle = battleController?.BattleState;
        if (battle == null)
            return;

        _pendingWager = Mathf.Min(GetMaxProposal(battle), _pendingWager + GetWagerStep(battle));
        Refresh();
    }

    private void DecrementWager()
    {
        BattleState battle = battleController?.BattleState;
        if (battle == null)
            return;

        int minimum = GetMinProposal(battle);
        _pendingWager = Mathf.Max(minimum, _pendingWager - GetWagerStep(battle));
        Refresh();
    }

    private void ProposeWager()
    {
        StartRound(_pendingWager, true);
    }

    private void AcceptDevilOffer()
    {
        StartRound(-1, true);
    }

    private void DeclineDevilOffer()
    {
        StartRound(-1, false);
    }

    private void StartRound(int wager, bool acceptDevilOffer)
    {
        if (battleController == null)
            return;

        _selectedHandIndices.Clear();
        _wagerOpen = false;
        _suppressRoundPilesUntilNextRound = false;
        battleController.DecideRoundWager(wager, acceptDevilOffer);
        Refresh();
    }

    private void OpenShop()
    {
        runManager?.OpenShop();
        Refresh();
    }

    private void ReturnToMap()
    {
        ClearBattleCardViews();
        battleController?.CompletePendingVisualTransition();
        runManager?.ReturnToMap();
        Refresh();
    }

    private void ShowDrawPile()
    {
        BattleState battle = battleController?.BattleState;
        if (battle == null)
            return;

        deckViewPanel?.Show(battle.PlayerDrawPile, DeckViewOptions.Default);
    }

    private void ShowPlayedPile()
    {
        BattleState battle = battleController?.BattleState;
        if (battle == null)
            return;

        var cards = new List<Card>();
        cards.AddRange(battle.PlayerDiscardPile);
        if (battle.CurrentRound != null)
            cards.AddRange(battle.CurrentRound.PlayerPlayedCards);

        deckViewPanel?.Show(cards, DeckViewOptions.Default);
    }

    private void OnRunPhaseChanged(RunPhaseChangedEvent eventData)
    {
        if (eventData.Phase != RunPhase.Battle && eventData.Phase != RunPhase.Shop)
            ClearBattleCardViews();

        Refresh();
    }

    private void OnBattleStarted(BattleStartedEvent eventData)
    {
        _suppressRoundPilesUntilNextRound = false;
        OpenWagerPanel();
        Refresh();
    }

    private void OnBattleEnded(BattleEndedEvent eventData)
    {
        ClearBattleCardViews();
        Refresh();
    }

    private void OpenWagerPanel()
    {
        BattleState battle = battleController?.BattleState;
        if (battle == null || battle.IsBattleOver)
            return;

        if (battle.CurrentRound == null)
            battleController.StartNextRound();

        battle = battleController?.BattleState;
        if (battle == null || battle.CurrentRound == null || battle.CurrentRound.WagerCommitted)
            return;

        _wagerOpen = true;
        _pendingWager = Mathf.Clamp(battle.GetDefaultWager(), GetMinProposal(battle), GetMaxProposal(battle));
    }

    private void RefreshWagerPanel(BattleState battle)
    {
        if (wagerPanel == null || !wagerPanel.activeSelf || battle == null)
            return;

        bool playerActsFirst = battle.CurrentRound == null || battle.CurrentRound.PlayerActsFirst;
        SetActive(playerProposalRoot, playerActsFirst);
        SetActive(devilOfferRoot, !playerActsFirst);

        int maxProposal = GetMaxProposal(battle);
        int minProposal = GetMinProposal(battle);
        _pendingWager = Mathf.Clamp(_pendingWager, minProposal, maxProposal);
        SetText(proposalAmountText, _pendingWager.ToString());
        //SetText(devilOfferAmountText, "Devil offer");
        if (!playerActsFirst)
            SetText(devilOfferAmountText, battle.GetOpponentWagerOffer().ToString());

        if (incrementWagerButton != null)
            incrementWagerButton.interactable = _pendingWager < maxProposal;
        if (decrementWagerButton != null)
            decrementWagerButton.interactable = _pendingWager > minProposal;
    }

    private void RefreshRoundResult(BattleState battle)
    {
        if (roundResultText == null || battle == null || battle.CombatHistory.Count == 0)
            return;

        RoundResolution resolution = battle.CombatHistory[battle.CombatHistory.Count - 1];
        string result = resolution.Winner == Combatant.Player ? "Round Won" : resolution.Winner == Combatant.Opponent ? "Round Lost" : "Round Draw";
        SetText(roundResultText, result);
    }

    private void RefreshBattleResult(BattleState battle)
    {
        if (battle == null || battle.Phase != BattlePhase.BattleEnd)
            return;

        ClearChildren(rewardViewRoot);
    }

    private int GetMaxProposal(BattleState battle)
    {
        //return Mathf.Max(GetMinProposal(battle), battle.GetDefaultWager() * (int)DevilHandLevel.VeryHigh);
        return Mathf.Max(GetMinProposal(battle), battle.PlayerMoney);
    }

    private int GetMinProposal(BattleState battle)
    {
        return Mathf.Max(1, battle.GetDefaultWager());
    }

    private int GetWagerStep(BattleState battle)
    {
        return Mathf.Max(1, battle.GetDefaultWager());
    }

    private void SetPanels(bool showWager, bool showRoundResult, bool showBattleResult)
    {
        SetActive(wagerPanel, showWager);
        SetActive(roundResultPanel, showRoundResult);
        SetActive(battleResultPanel, showBattleResult);
    }

    private void SetTurnButtons(bool playerTurn, bool hasSelectedCard)
    {
        if (playButton != null)
            playButton.interactable = playerTurn && hasSelectedCard;
        if (standButton != null)
            standButton.interactable = playerTurn;
        if (hitButton != null)
            hitButton.interactable = playerTurn;
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

            views[i].SetSelected(interactable && _selectedHandIndices.Contains(i));
        }

        if (root != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);

        for (int i = 0; i < views.Count && i < count; i++)
            views[i].SetSelected(interactable && _selectedHandIndices.Contains(i));
    }

    private void BindCard(BattleUiCardView view, Card card, bool faceUp, bool interactable, int handIndex)
    {
        Sprite sprite = faceUp && assetRegistry != null ? assetRegistry.GetCardFront(card) : assetRegistry != null ? assetRegistry.CardBack : null;
        view.Bind(card, sprite, faceUp, interactable, assetRegistry);

        if (view.Button == null)
            return;

        view.Button.onClick.RemoveAllListeners();
        if (interactable && handIndex >= 0)
        {
            int capturedIndex = handIndex;
            view.Button.onClick.AddListener(() => ToggleCardSelection(capturedIndex));
        }
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
        return battle.Phase == BattlePhase.PlayerPhase && !battleController.IsWaitingForVisuals && battle.CurrentRound != null;
    }

    private void SetScoreText(RoundState round)
    {
        SetText(playerScoreText, round != null && round.PlayerHasPlayed ? round.PlayerScore.BlackjackScore.ToString() : "-");
        SetText(devilScoreText, round != null && round.OpponentHasPlayed ? round.OpponentScore.BlackjackScore.ToString() : "-");
    }

    private void AutoBindLayout()
    {
        devilImage ??= FindDescendantComponent<Image>("DevilImage");
        playerScoreText ??= FindDescendantComponent<TMP_Text>("PlayerScore");
        devilScoreText ??= FindDescendantComponent<TMP_Text>("DevilScore");
        roundWagerText ??= FindDescendantComponent<TMP_Text>("RoundWagerAmount");
        devilMoneyText ??= FindDescendantComponent<TMP_Text>("DevilMoney");
        playerHandRoot ??= FindDescendantRect("PlayerHand");
        opponentHandRoot ??= FindDescendantRect("DevilHand");
        devilPlayPileRoot ??= FindDescendantRect("DevilPlayPile");
        sharedPlayPileRoot ??= FindDescendantRect("SharedPlayPile");
        playerPlayPileRoot ??= FindDescendantRect("PlayerPlayPile");
        playPileRoot ??= FindDescendantRect("PlayPile");
        devilPlayPileRoot ??= playPileRoot;
        sharedPlayPileRoot ??= playPileRoot;
        playerPlayPileRoot ??= playPileRoot;
        playButton ??= FindDescendantComponent<Button>("PlayButton");
        standButton ??= FindDescendantComponent<Button>("StandButton");
        hitButton ??= FindDescendantComponent<Button>("HitButton");
        wagerPanel ??= FindDescendant("WagerPanel");
        playerProposalRoot ??= FindDescendant("PlayerProposal");
        devilOfferRoot ??= FindDescendant("DevilOffer");
        incrementWagerButton ??= FindDescendantComponent<Button>("IncrementButton");
        decrementWagerButton ??= FindDescendantComponent<Button>("DecrementButton");
        proposalButton ??= FindDescendantComponent<Button>("ProposalButton");
        acceptOfferButton ??= FindDescendantComponent<Button>("AcceptButton");
        declineOfferButton ??= FindDescendantComponent<Button>("DeclineButton");
        proposalAmountText ??= FindFirstTextUnder(playerProposalRoot, "RoundWagerAmount");
        devilOfferAmountText ??= FindFirstTextUnder(devilOfferRoot, "RoundWagerAmount");
        deckViewPanel ??= FindFirstObjectByType<DeckViewPanel>(FindObjectsInactive.Include);
        viewDrawPileButton ??= FindDescendantComponent<Button>("ViewDrawPileButton");
        viewPlayedPileButton ??= FindDescendantComponent<Button>("ViewPlayedPileButton");
        roundResultPanel ??= FindDescendant("RoundResultPanel");
        roundResultText ??= FindDescendantComponent<TMP_Text>("RoundResultText");
        toShopButton ??= FindDescendantComponent<Button>("ToShopButton");
        toShopButton ??= FindDescendantComponent<Button>("NextRoundButton");
        battleResultPanel ??= FindDescendant("BattleResultPanel");
        rewardViewRoot ??= FindDescendantRect("RewardViewRoot");
        backToMapButton ??= FindDescendantComponent<Button>("BackToMapButton");
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

        layout.minWidth = 90f;
        layout.minHeight = 126f;
        layout.preferredWidth = 90f;
        layout.preferredHeight = 126f;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;
    }

    private static void ConfigureCardLayout(RectTransform root)
    {
        if (root == null)
            return;

        HorizontalLayoutGroup horizontal = root.GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null)
        {
            horizontal.childControlWidth = false;
            horizontal.childControlHeight = false;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = false;
            horizontal.childScaleWidth = false;
            horizontal.childScaleHeight = false;
        }

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
        _selectedHandIndices.Clear();
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
