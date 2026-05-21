using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleUiPresenter : MonoBehaviour
{
    [SerializeField] private BattleController battleController;
    [SerializeField] private GameplayAssetRegistry assetRegistry;
    [SerializeField] private Image devilImage;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text playerHpText;
    [SerializeField] private TMP_Text opponentHpText;
    [SerializeField] private Image playerHpFill;
    [SerializeField] private Image opponentHpFill;
    [SerializeField] private Button endTurnButton;
    [SerializeField] private RectTransform playerHandRoot;
    [SerializeField] private RectTransform opponentHandRoot;
    [SerializeField] private RectTransform playPileRoot;

    private readonly List<BattleUiCardView> _playerCards = new();
    private readonly List<BattleUiCardView> _opponentCards = new();
    private readonly List<BattleUiCardView> _playPileCards = new();

    private void Awake()
    {
        if (battleController == null)
            battleController = FindFirstObjectByType<BattleController>();

        BuildIfNeeded();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);
        EventBus.Subscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);

        if (endTurnButton != null)
            endTurnButton.onClick.AddListener(EndTurn);

        Refresh();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<RunPhaseChangedEvent>(OnRunPhaseChanged);
        EventBus.Unsubscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Unsubscribe<BattleEndedEvent>(OnBattleEnded);

        if (endTurnButton != null)
            endTurnButton.onClick.RemoveListener(EndTurn);
    }

    public void Refresh()
    {
        BuildIfNeeded();

        BattleState battle = battleController != null ? battleController.BattleState : null;
        RoundState round = battle != null ? battle.CurrentRound : null;

        if (battle == null)
        {
            SetText(statusText, "No battle");
            SetText(scoreText, "Round Score\n-");
            SetHp(playerHpFill, playerHpText, 0, 1, "Player");
            SetHp(opponentHpFill, opponentHpText, 0, 1, "Devil");
            RenderCards(_playerCards, playerHandRoot, 0, null, false, false, false);
            RenderCards(_opponentCards, opponentHandRoot, 0, null, false, false, true);
            RenderCards(_playPileCards, playPileRoot, 0, null, false, false, false);
            SetEndTurnInteractable(false);
            return;
        }

        if (devilImage != null && assetRegistry != null)
            devilImage.sprite = assetRegistry.GetDevilSprite(battle.Config.EncounterId);

        SetText(statusText, $"Round {battle.RoundNumber} - {battle.Phase}");
        SetHp(playerHpFill, playerHpText, battle.PlayerHp, battle.PlayerMaxHp, "Player");
        SetHp(opponentHpFill, opponentHpText, battle.OpponentHp, battle.OpponentMaxHp, "Devil");
        SetScoreText(round);

        RenderCards(_playerCards, playerHandRoot, round?.PlayerHand.Count ?? 0, round?.PlayerHand, true, CanPlayCards(battle), false);
        RenderCards(_opponentCards, opponentHandRoot, round?.OpponentHand.Count ?? 0, round?.OpponentHand, false, false, true);
        RenderPlayPile(round, battle);
        SetEndTurnInteractable(battle.Phase == BattlePhase.PlayerPhase && !battleController.IsWaitingForVisuals);
    }

    private void EndTurn()
    {
        if (battleController == null)
            return;

        battleController.EndPlayerPhase();
        Refresh();
    }

    private void OnRunPhaseChanged(RunPhaseChangedEvent eventData)
    {
        Refresh();
    }

    private void OnBattleStarted(BattleStartedEvent eventData)
    {
        Refresh();
    }

    private void OnBattleEnded(BattleEndedEvent eventData)
    {
        Refresh();
    }

    private void RenderPlayPile(RoundState round, BattleState battle)
    {
        int count = (round?.PlayerPlayedCards.Count ?? 0) + (round?.OpponentVisibleCards.Count ?? 0) + (round?.SharedVisibleCards.Count ?? 0);
        EnsureCardViews(_playPileCards, playPileRoot, count, false);

        int viewIndex = 0;
        if (round != null)
        {
            for (int i = 0; i < round.PlayerPlayedCards.Count; i++)
                BindCard(_playPileCards[viewIndex++], round.PlayerPlayedCards[i], true, false, -1);

            for (int i = 0; i < round.OpponentVisibleCards.Count; i++)
                BindCard(_playPileCards[viewIndex++], round.OpponentVisibleCards[i], true, false, -1);

            for (int i = 0; i < round.SharedVisibleCards.Count; i++)
                BindCard(_playPileCards[viewIndex++], round.SharedVisibleCards[i], true, false, -1);
        }

        for (int i = viewIndex; i < _playPileCards.Count; i++)
            _playPileCards[i].gameObject.SetActive(false);
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
            {
                views[i].BindBack(assetRegistry != null ? assetRegistry.CardBack : null);
            }
            else if (cards != null && i < cards.Count)
            {
                BindCard(views[i], cards[i], faceUp, interactable, i);
            }
        }
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
            view.Button.onClick.AddListener(() =>
            {
                if (battleController != null && battleController.TryPlayCard(capturedIndex))
                    Refresh();
            });
        }
    }

    private void EnsureCardViews(List<BattleUiCardView> views, RectTransform root, int count, bool withButton)
    {
        if (root == null)
            return;

        while (views.Count < count)
            views.Add(CreateCardView(root, withButton));
    }

    private BattleUiCardView CreateCardView(RectTransform parent, bool withButton)
    {
        GameObject card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(parent, false);
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(90f, 126f);

        Image image = card.GetComponent<Image>();
        image.color = new Color(0.92f, 0.86f, 0.74f, 1f);
        image.preserveAspect = true;

        Button button = null;
        if (withButton)
            button = card.AddComponent<Button>();

        TMP_Text label = CreateText(card.transform, "Label", 18, TextAlignmentOptions.Center);
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

    private bool CanPlayCards(BattleState battle)
    {
        return battle.Phase == BattlePhase.PlayerPhase && !battleController.IsWaitingForVisuals && battle.CurrentRound != null && !battle.CurrentRound.PlayerHasPlayed;
    }

    private void SetScoreText(RoundState round)
    {
        if (round == null || (!round.PlayerHasPlayed && !round.OpponentHasPlayed))
        {
            SetText(scoreText, "Round Score\n-");
            return;
        }

        if (round.PlayerScore.FinalScore == 0 && round.OpponentScore.FinalScore == 0)
            SetText(scoreText, "Round Score\nWaiting");
        else
            SetText(scoreText, $"Round Score\nPlayer {round.PlayerScore.BlackjackScore} / Devil {round.OpponentScore.BlackjackScore}");
    }

    private void SetHp(Image fill, TMP_Text label, int current, int max, string name)
    {
        int safeMax = Mathf.Max(1, max);
        if (fill != null)
            fill.fillAmount = Mathf.Clamp01((float)current / safeMax);

        SetText(label, $"{name} HP {current}/{safeMax}");
    }

    private void SetEndTurnInteractable(bool interactable)
    {
        if (endTurnButton != null)
            endTurnButton.interactable = interactable;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }

    private void BuildIfNeeded()
    {
        if (playerHandRoot != null)
            return;

        RectTransform root = transform as RectTransform;
        if (root == null)
            return;

        Image background = gameObject.GetComponent<Image>();
        if (background == null)
            background = gameObject.AddComponent<Image>();
        background.color = new Color(0.06f, 0.05f, 0.07f, 0.92f);

        statusText = CreateAnchoredText(root, "Status", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(420f, 42f), 22, TextAlignmentOptions.Center);
        CreateHpBar(root, "PlayerHp", new Vector2(0.04f, 0.94f), new Vector2(0.43f, 0.99f), out playerHpFill, out playerHpText, "Player HP");
        CreateHpBar(root, "OpponentHp", new Vector2(0.57f, 0.94f), new Vector2(0.96f, 0.99f), out opponentHpFill, out opponentHpText, "Devil HP");

        devilImage = CreateImage(root, "Devil", new Vector2(0.5f, 0.53f), new Vector2(0.5f, 0.53f), new Vector2(0f, 0f), new Vector2(240f, 240f));
        devilImage.color = Color.white;
        devilImage.preserveAspect = true;

        Image scorePanel = CreateImage(root, "ScorePanel", new Vector2(0.67f, 0.49f), new Vector2(0.93f, 0.66f), Vector2.zero, Vector2.zero);
        scorePanel.color = new Color(0.12f, 0.11f, 0.13f, 0.86f);
        scoreText = CreateText(scorePanel.transform, "Text", 26, TextAlignmentOptions.Center);
        RectTransform scoreRect = scoreText.rectTransform;
        scoreRect.anchorMin = Vector2.zero;
        scoreRect.anchorMax = Vector2.one;
        scoreRect.offsetMin = new Vector2(8f, 4f);
        scoreRect.offsetMax = new Vector2(-8f, -4f);
        scoreText.color = Color.white;

        opponentHandRoot = CreateRow(root, "OpponentHand", new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.78f), new Vector2(0f, 0f), new Vector2(520f, 140f));
        playPileRoot = CreateRow(root, "PlayPile", new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f), new Vector2(0f, 0f), new Vector2(640f, 150f));
        playerHandRoot = CreateRow(root, "PlayerHand", new Vector2(0.5f, 0.08f), new Vector2(0.5f, 0.08f), new Vector2(0f, 0f), new Vector2(800f, 150f));

        endTurnButton = CreateButton(root, "EndTurnButton", "End Turn", new Vector2(0.84f, 0.1f), new Vector2(0.84f, 0.1f), new Vector2(0f, 0f), new Vector2(180f, 58f));
    }

    private static void CreateHpBar(RectTransform root, string name, Vector2 anchorMin, Vector2 anchorMax, out Image fill, out TMP_Text label, string labelText)
    {
        Image frame = CreateImage(root, name, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        frame.color = new Color(0.12f, 0.12f, 0.13f, 0.96f);

        Image fillImage = CreateImage(frame.rectTransform, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        fillImage.color = new Color(0.74f, 0.12f, 0.18f, 1f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;

        label = CreateText(frame.transform, "Label", 20, TextAlignmentOptions.Center);
        label.text = labelText;
        label.color = Color.white;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        fill = fillImage;
    }

    private static RectTransform CreateRow(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        row.transform.SetParent(parent, false);
        RectTransform rect = row.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 12f;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        return rect;
    }

    private static Button CreateButton(RectTransform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        Image image = CreateImage(parent, name, anchorMin, anchorMax, position, size);
        image.color = new Color(0.78f, 0.14f, 0.19f, 1f);
        Button button = image.gameObject.AddComponent<Button>();
        TMP_Text text = CreateText(image.transform, "Label", 24, TextAlignmentOptions.Center);
        text.text = label;
        text.color = Color.white;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return button;
    }

    private static TMP_Text CreateAnchoredText(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, int fontSize, TextAlignmentOptions alignment)
    {
        Image image = CreateImage(parent, name, anchorMin, anchorMax, position, size);
        image.color = new Color(0f, 0f, 0f, 0f);
        TMP_Text text = CreateText(image.transform, "Text", fontSize, alignment);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 4f);
        rect.offsetMax = new Vector2(-8f, -4f);
        return text;
    }

    private static Image CreateImage(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return go.GetComponent<Image>();
    }

    private static TMP_Text CreateText(Transform parent, string name, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        return text;
    }
}
