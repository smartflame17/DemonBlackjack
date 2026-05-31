using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// This component is responsible for battle UI presentation - Runtime monobehaviour binding.
public sealed class BattleUiPresenter : MonoBehaviour
{
    [SerializeField] private BattleController battleController;
    [SerializeField] private RunManager runManager;
    [SerializeField] private GameplayAssetRegistry assetRegistry;

    [Header("Main")]
    [SerializeField] private Image devilImage;
    [SerializeField] private TMP_Text playerScoreText;
    [SerializeField] private TMP_Text devilScoreText;
    [SerializeField] private TMP_Text roundWagerText;
    [SerializeField] private TMP_Text playerMoneyText;
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
    [SerializeField] private Button viewFullDeckButton;
    [SerializeField] private Button viewDrawPileButton;
    [SerializeField] private Button viewPlayedPileButton;
    [SerializeField] private GameObject deckViewPanel;
    //[SerializeField] private RectTransform cardGridViewRoot;
    [SerializeField] private RectTransform rankGridViewRoot;
    [SerializeField] private RectTransform suitGridViewRoot;
    [SerializeField] private Button viewByRankButton;
    [SerializeField] private Button viewBySuitButton;
    [SerializeField] private Button closeDeckViewButton;

    [Header("Results")]
    [SerializeField] private GameObject roundResultPanel;
    [SerializeField] private TMP_Text roundResultText;
    [SerializeField] private Button nextRoundButton;
    [SerializeField] private GameObject battleResultPanel;
    [SerializeField] private RectTransform rewardViewRoot;
    [SerializeField] private Button backToMapButton;

    private readonly List<BattleUiCardView> _playerCards = new();
    private readonly List<BattleUiCardView> _opponentCards = new();
    private readonly List<BattleUiCardView> _devilPlayPileCards = new();
    private readonly List<BattleUiCardView> _sharedPlayPileCards = new();
    private readonly List<BattleUiCardView> _playerPlayPileCards = new();
    private readonly List<BattleUiCardView> _deckCards = new();
    private readonly HashSet<int> _selectedHandIndices = new();
    private readonly List<Card> _deckViewCards = new();

    private int _pendingWager = 10;
    private bool _wagerOpen;
    private DeckViewSortMode _deckViewSortMode = DeckViewSortMode.Rank;

    private enum DeckViewSortMode
    {
        Rank,
        Suit
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
        //ConfigureCardLayout(cardGridViewRoot);
        //ConfigureCardLayoutGroups(rankGridViewRoot);
        //ConfigureCardLayoutGroups(suitGridViewRoot);
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
        AutoBindLayout();

        BattleState battle = battleController != null ? battleController.BattleState : null;
        RoundState round = battle?.CurrentRound;

        if (battle == null)
        {
            ClearBattleCardViews();
            SetText(playerScoreText, "-");
            SetText(devilScoreText, "-");
            SetText(roundWagerText, "0");
            SetText(playerMoneyText, "Player $0");
            SetText(devilMoneyText, "Devil $0");
            SetPanels(false, false, false);
            SetTurnButtons(false, false);
            return;
        }

        if (devilImage != null && assetRegistry != null)
            devilImage.sprite = assetRegistry.GetDevilSprite(battle.Config.DevilId);

        if (battle.Phase == BattlePhase.PreRound && battle.CurrentRound == null && battle.CombatHistory.Count == 0 && !_wagerOpen)
            OpenWagerPanel();

        SetText(playerMoneyText, $"Player ${battle.PlayerMoney}");
        SetText(devilMoneyText, $"Devil ${battle.OpponentMoney}");
        SetScoreText(round);
        SetText(roundWagerText, round != null ? round.Pot.ToString() : "0");

        RenderCards(_playerCards, playerHandRoot, round?.PlayerHand.Count ?? 0, round?.PlayerHand, true, CanSelectCards(battle), false);
        RenderCards(_opponentCards, opponentHandRoot, round?.OpponentHand.Count ?? 0, round?.OpponentHand, false, false, true);
        RenderPlayPile(round);

        bool roundFinished = battle.Phase == BattlePhase.Cleanup;
        bool battleFinished = battle.Phase == BattlePhase.BattleEnd;
        bool showWager = _wagerOpen && battle.Phase == BattlePhase.PreRound && !battleFinished;
        SetPanels(showWager, roundFinished, battleFinished);
        RefreshWagerPanel(battle);
        RefreshRoundResult(battle);
        RefreshBattleResult(battle);
        SetTurnButtons(battle.Phase == BattlePhase.PlayerPhase && !battleController.IsWaitingForVisuals, _selectedHandIndices.Count > 0);
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
        if (viewFullDeckButton != null)
            viewFullDeckButton.onClick.AddListener(ShowFullDeck);
        if (viewDrawPileButton != null)
            viewDrawPileButton.onClick.AddListener(ShowDrawPile);
        if (viewPlayedPileButton != null)
            viewPlayedPileButton.onClick.AddListener(ShowPlayedPile);
        if (viewByRankButton != null)
            viewByRankButton.onClick.AddListener(ShowDeckViewByRank);
        if (viewBySuitButton != null)
            viewBySuitButton.onClick.AddListener(ShowDeckViewBySuit);
        if (closeDeckViewButton != null)
            closeDeckViewButton.onClick.AddListener(CloseDeckView);
        if (nextRoundButton != null)
            nextRoundButton.onClick.AddListener(ContinueToNextRound);
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
        Remove(viewFullDeckButton, ShowFullDeck);
        Remove(viewDrawPileButton, ShowDrawPile);
        Remove(viewPlayedPileButton, ShowPlayedPile);
        Remove(viewByRankButton, ShowDeckViewByRank);
        Remove(viewBySuitButton, ShowDeckViewBySuit);
        Remove(closeDeckViewButton, CloseDeckView);
        Remove(nextRoundButton, ContinueToNextRound);
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
            _selectedHandIndices.Remove(handIndex);

        Refresh();
    }

    private void PlaySelectedCards()
    {
        if (battleController == null || _selectedHandIndices.Count == 0)
            return;

        var selected = new List<int>(_selectedHandIndices);
        selected.Sort();

        int playedCount = 0;
        for (int i = 0; i < selected.Count; i++)
        {
            int adjustedIndex = selected[i] - playedCount;
            if (battleController.TryPlayCard(adjustedIndex))
                playedCount++;
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

        _pendingWager = Mathf.Min(GetMaxProposal(battle), _pendingWager + battle.Config.WagerStep);
        Refresh();
    }

    private void DecrementWager()
    {
        BattleState battle = battleController?.BattleState;
        if (battle == null)
            return;

        int minimum = battle.PlayerMoney < battle.Config.MinWager ? battle.PlayerMoney : battle.Config.MinWager;
        _pendingWager = Mathf.Max(minimum, _pendingWager - battle.Config.WagerStep);
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
        battleController.StartNextRound(wager, acceptDevilOffer);
        Refresh();
    }

    private void ContinueToNextRound()
    {
        battleController?.CompletePendingVisualTransition();
        OpenWagerPanel();
        Refresh();
    }

    private void ReturnToMap()
    {
        ClearBattleCardViews();
        battleController?.CompletePendingVisualTransition();
        runManager?.ReturnToMap();
        Refresh();
    }

    private void ShowFullDeck()
    {
        BattleState battle = battleController?.BattleState;
        if (battle == null)
            return;

        var cards = new List<Card>(battle.RunState.Deck);
        ShowDeckView(cards);
    }

    private void ShowDrawPile()
    {
        BattleState battle = battleController?.BattleState;
        if (battle == null)
            return;

        ShowDeckView(battle.PlayerDrawPile);
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

        ShowDeckView(cards);
    }

    private void ShowDeckView(IReadOnlyList<Card> cards)
    {
        if (deckViewPanel != null)
            deckViewPanel.SetActive(true);

        _deckViewSortMode = DeckViewSortMode.Rank;
        _deckViewCards.Clear();
        if (cards != null)
            _deckViewCards.AddRange(cards);

        RenderDeckView();
    }

    private void ShowDeckViewByRank()
    {
        _deckViewSortMode = DeckViewSortMode.Rank;
        RenderDeckView();
    }

    private void ShowDeckViewBySuit()
    {
        _deckViewSortMode = DeckViewSortMode.Suit;
        RenderDeckView();
    }

    private void CloseDeckView()
    {
        if (deckViewPanel != null)
            deckViewPanel.SetActive(false);

        DestroyCardViews(_deckCards);
    }

    private void OnRunPhaseChanged(RunPhaseChangedEvent eventData)
    {
        if (eventData.Phase != RunPhase.Battle)
            ClearBattleCardViews();

        Refresh();
    }

    private void OnBattleStarted(BattleStartedEvent eventData)
    {
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

        _wagerOpen = true;
        _pendingWager = Mathf.Clamp(battle.Config.BaseWager, battle.Config.MinWager, GetMaxProposal(battle));
    }

    private void RefreshWagerPanel(BattleState battle)
    {
        if (wagerPanel == null || !wagerPanel.activeSelf || battle == null)
            return;

        bool playerActsFirst = battle.RoundNumber % 2 == 0;
        SetActive(playerProposalRoot, playerActsFirst);
        SetActive(devilOfferRoot, !playerActsFirst);

        int maxProposal = GetMaxProposal(battle);
        int minProposal = battle.PlayerMoney < battle.Config.MinWager ? battle.PlayerMoney : battle.Config.MinWager;
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
        return Mathf.Max(1, battle.Config.MaxWager);
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

    private void RenderPlayPile(RoundState round)
    {
        RenderStaticPile(_devilPlayPileCards, devilPlayPileRoot, round?.OpponentVisibleCards);
        RenderStaticPile(_sharedPlayPileCards, sharedPlayPileRoot, round?.SharedVisibleCards);
        RenderStaticPile(_playerPlayPileCards, playerPlayPileRoot, round?.PlayerPlayedCards);
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

    private void RenderDeckView()
    {
        DestroyCardViews(_deckCards);

        bool showRank = _deckViewSortMode == DeckViewSortMode.Rank;
        SetActive(rankGridViewRoot != null ? rankGridViewRoot.gameObject : null, showRank);
        SetActive(suitGridViewRoot != null ? suitGridViewRoot.gameObject : null, !showRank);

        if (viewByRankButton != null)
            viewByRankButton.interactable = !showRank;
        if (viewBySuitButton != null)
            viewBySuitButton.interactable = showRank;

        if (showRank)
            RenderDeckCardsByRank();
        else
            RenderDeckCardsBySuit();
    }

    private void RenderDeckCardsByRank()
    {
        if (rankGridViewRoot == null)
        {
            RenderDeckCardsFallback(CompareCardsByRank);
            return;
        }

        //ConfigureCardLayoutGroups(rankGridViewRoot); - Don't configure group layouts. Already configured in editor
        var sorted = new List<Card>(_deckViewCards);
        sorted.Sort(CompareCardsByRank);

        for (int i = 0; i < sorted.Count; i++)
        {
            Card card = sorted[i];
            RectTransform group = GetRankGroup(card.Rank);
            if (group == null)
                continue;

            BattleUiCardView view = CreateCardView(group, false);
            _deckCards.Add(view);
            BindCard(view, card, true, false, -1);
        }

        RebuildCardLayoutGroups(rankGridViewRoot);
    }

    private void RenderDeckCardsBySuit()
    {
        if (suitGridViewRoot == null)
        {
            RenderDeckCardsFallback(CompareCardsBySuit);
            return;
        }

        //ConfigureCardLayoutGroups(suitGridViewRoot); - Don't configure group layouts. Already configured in editor
        var sorted = new List<Card>(_deckViewCards);
        sorted.Sort(CompareCardsBySuit);

        for (int i = 0; i < sorted.Count; i++)
        {
            Card card = sorted[i];
            RectTransform group = GetSuitGroup(card.Suit);
            if (group == null)
                continue;

            BattleUiCardView view = CreateCardView(group, false);
            _deckCards.Add(view);
            BindCard(view, card, true, false, -1);
        }

        RebuildCardLayoutGroups(suitGridViewRoot);
    }

    private void RenderDeckCardsFallback(System.Comparison<Card> comparison)
    {
        var sorted = new List<Card>(_deckViewCards);
        sorted.Sort(comparison);

        RectTransform fallbackRoot = _deckViewSortMode == DeckViewSortMode.Rank ? rankGridViewRoot : suitGridViewRoot;
        fallbackRoot ??= rankGridViewRoot != null ? rankGridViewRoot : suitGridViewRoot;
        if (fallbackRoot == null)
            return;

        // Ensure the root we are rendering into is visible.
        SetActive(rankGridViewRoot != null ? rankGridViewRoot.gameObject : null, fallbackRoot == rankGridViewRoot);
        SetActive(suitGridViewRoot != null ? suitGridViewRoot.gameObject : null, fallbackRoot == suitGridViewRoot);

        RenderCards(_deckCards, fallbackRoot, sorted.Count, sorted, true, false, false);
    }

    private RectTransform GetRankGroup(Rank rank)
    {
        int index = (int)rank - 1;
        return GetChildRect(rankGridViewRoot, index);
    }

    private RectTransform GetSuitGroup(Suit suit)
    {
        return GetChildRect(suitGridViewRoot, (int)suit);
    }

    private static RectTransform GetChildRect(RectTransform root, int index)
    {
        if (root == null || index < 0 || index >= root.childCount)
            return null;

        return root.GetChild(index).GetComponent<RectTransform>();
    }

    private static int CompareCardsByRank(Card x, Card y)
    {
        int rank = x.Rank.CompareTo(y.Rank);
        if (rank != 0)
            return rank;

        int suit = x.Suit.CompareTo(y.Suit);
        if (suit != 0)
            return suit;

        return string.CompareOrdinal(x.ModifierId, y.ModifierId);
    }

    private static int CompareCardsBySuit(Card x, Card y)
    {
        int suit = x.Suit.CompareTo(y.Suit);
        if (suit != 0)
            return suit;

        int rank = x.Rank.CompareTo(y.Rank);
        if (rank != 0)
            return rank;

        return string.CompareOrdinal(x.ModifierId, y.ModifierId);
    }

    private void BindCard(BattleUiCardView view, Card card, bool faceUp, bool interactable, int handIndex)
    {
        Sprite sprite = faceUp && assetRegistry != null ? assetRegistry.GetCardFront(card) : assetRegistry != null ? assetRegistry.CardBack : null;
        view.Bind(card, sprite, faceUp, interactable);

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
            views.Add(CreateCardView(root, withButton));

        if (views.Count != previousCount)
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    private BattleUiCardView CreateCardView(RectTransform parent, bool withButton)
    {
        GameObject card = new("Card", typeof(RectTransform), typeof(LayoutElement));
        card.transform.SetParent(parent, false);
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(90f, 126f);

        LayoutElement layout = card.GetComponent<LayoutElement>();
        layout.minWidth = 90f;
        layout.minHeight = 126f;
        layout.preferredWidth = 90f;
        layout.preferredHeight = 126f;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;

        GameObject visual = new("Visual", typeof(RectTransform), typeof(Image));
        visual.transform.SetParent(card.transform, false);
        RectTransform visualRect = visual.GetComponent<RectTransform>();
        visualRect.anchorMin = Vector2.zero;
        visualRect.anchorMax = Vector2.one;
        visualRect.offsetMin = Vector2.zero;
        visualRect.offsetMax = Vector2.zero;

        Image image = visual.GetComponent<Image>();
        image.color = Color.white;
        image.preserveAspect = true;

        Button button = withButton ? visual.AddComponent<Button>() : null;
        TMP_Text label = CreateText(visual.transform, "Label", 18, TextAlignmentOptions.Center);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(6f, 6f);
        labelRect.offsetMax = new Vector2(-6f, -6f);
        label.color = Color.black;

        BattleUiCardView view = card.AddComponent<BattleUiCardView>();
        view.Initialize(image, label, button);
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
        playerMoneyText ??= FindDescendantComponent<TMP_Text>("PlayerMoney");
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
        viewFullDeckButton ??= FindDescendantComponent<Button>("ViewFullDeckButton");
        viewDrawPileButton ??= FindDescendantComponent<Button>("ViewDrawPileButton");
        viewPlayedPileButton ??= FindDescendantComponent<Button>("ViewPlayedPileButton");
        deckViewPanel ??= FindDescendant("DeckViewPanel");
        //cardGridViewRoot ??= FindDescendantRect("CardGridViewRoot");
        rankGridViewRoot ??= FindDescendantRect("RankGridViewRoot");
        suitGridViewRoot ??= FindDescendantRect("SuitGridViewRoot");
        viewByRankButton ??= FindDescendantComponent<Button>("ViewByRankButton");
        viewBySuitButton ??= FindDescendantComponent<Button>("ViewBySuitButton");
        closeDeckViewButton ??= FindDescendantComponent<Button>("CloseButton");
        roundResultPanel ??= FindDescendant("RoundResultPanel");
        roundResultText ??= FindDescendantComponent<TMP_Text>("RoundResultText");
        nextRoundButton ??= FindDescendantComponent<Button>("NextRoundButton");
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

    private static TMP_Text CreateText(Transform parent, string name, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
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
        DestroyCardViews(_playerCards);
        DestroyCardViews(_opponentCards);
        DestroyCardViews(_devilPlayPileCards);
        DestroyCardViews(_sharedPlayPileCards);
        DestroyCardViews(_playerPlayPileCards);
        DestroyCardViews(_deckCards);
        ClearChildren(playerHandRoot);
        ClearChildren(opponentHandRoot);
        ClearChildren(devilPlayPileRoot);
        ClearChildren(sharedPlayPileRoot);
        ClearChildren(playerPlayPileRoot);
        _selectedHandIndices.Clear();
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

        DestroyImmediate(target);
    }
}
