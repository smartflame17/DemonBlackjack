using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Flags]
public enum DeckViewFlags
{
    None = 0,
    AllowSortModeSwitching = 1 << 0,
    AllowCardSelection = 1 << 1
}

public enum DeckViewSortMode
{
    Rank,
    Suit
}

[Serializable]
public struct DeckViewOptions
{
    public DeckViewFlags Flags;
    public DeckViewSortMode InitialSortMode;
    public int SelectionCount;

    public static DeckViewOptions Default => new()
    {
        Flags = DeckViewFlags.AllowSortModeSwitching,
        InitialSortMode = DeckViewSortMode.Rank,
        SelectionCount = 0
    };
}

public sealed class DeckViewPanel : MonoBehaviour
{
    [SerializeField] private GameplayAssetRegistry assetRegistry;
    [SerializeField] private BattleUiCardView cardPrefab;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform rankGridViewRoot;
    [SerializeField] private RectTransform suitGridViewRoot;
    [SerializeField] private Button viewByRankButton;
    [SerializeField] private Button viewBySuitButton;
    [SerializeField] private Button closeButton;

    private readonly List<BattleUiCardView> _cardViews = new();
    private readonly List<Card> _cards = new();

    private DeckViewOptions _options;
    private DeckViewSortMode _sortMode;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        AddListeners();
    }

    private void OnDisable()
    {
        RemoveListeners();
        DestroyCardViews(_cardViews);
    }

    public void Show(IReadOnlyList<Card> cards, DeckViewOptions options = default)
    {
        ResolveReferences();

        _options = NormalizeOptions(options);
        _sortMode = _options.InitialSortMode;
        _cards.Clear();
        if (cards != null)
            _cards.AddRange(cards);

        if (panelRoot != null)
            panelRoot.SetActive(true);

        Render();
    }

    public void Close()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        DestroyCardViews(_cardViews);
    }

    private static DeckViewOptions NormalizeOptions(DeckViewOptions options)
    {
        if (options.Flags == DeckViewFlags.None
            && options.InitialSortMode == default
            && options.SelectionCount == default)
        {
            return DeckViewOptions.Default;
        }

        return options;
    }

    private void AddListeners()
    {
        if (viewByRankButton != null)
            viewByRankButton.onClick.AddListener(ShowByRank);
        if (viewBySuitButton != null)
            viewBySuitButton.onClick.AddListener(ShowBySuit);
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    private void RemoveListeners()
    {
        Remove(viewByRankButton, ShowByRank);
        Remove(viewBySuitButton, ShowBySuit);
        Remove(closeButton, Close);
    }

    private static void Remove(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }

    private void ShowByRank()
    {
        _sortMode = DeckViewSortMode.Rank;
        Render();
    }

    private void ShowBySuit()
    {
        _sortMode = DeckViewSortMode.Suit;
        Render();
    }

    private void Render()
    {
        DestroyCardViews(_cardViews);

        bool allowSortSwitching = _options.Flags.HasFlag(DeckViewFlags.AllowSortModeSwitching);
        bool showRank = _sortMode == DeckViewSortMode.Rank;
        SetActive(rankGridViewRoot != null ? rankGridViewRoot.gameObject : null, showRank);
        SetActive(suitGridViewRoot != null ? suitGridViewRoot.gameObject : null, !showRank);

        if (viewByRankButton != null)
            viewByRankButton.interactable = allowSortSwitching && !showRank;
        if (viewBySuitButton != null)
            viewBySuitButton.interactable = allowSortSwitching && showRank;

        if (showRank)
            RenderCardsByRank();
        else
            RenderCardsBySuit();
    }

    private void RenderCardsByRank()
    {
        if (rankGridViewRoot == null)
        {
            RenderCardsFallback(CompareCardsByRank);
            return;
        }

        var sorted = new List<Card>(_cards);
        sorted.Sort(CompareCardsByRank);

        for (int i = 0; i < sorted.Count; i++)
        {
            Card card = sorted[i];
            RectTransform group = GetRankGroup(card.Rank);
            if (group == null)
                continue;

            BattleUiCardView view = CreateCardView(group);
            if (view == null)
                continue;

            _cardViews.Add(view);
            BindCard(view, card);
        }

        RebuildCardLayoutGroups(rankGridViewRoot);
    }

    private void RenderCardsBySuit()
    {
        if (suitGridViewRoot == null)
        {
            RenderCardsFallback(CompareCardsBySuit);
            return;
        }

        var sorted = new List<Card>(_cards);
        sorted.Sort(CompareCardsBySuit);

        for (int i = 0; i < sorted.Count; i++)
        {
            Card card = sorted[i];
            RectTransform group = GetSuitGroup(card.Suit);
            if (group == null)
                continue;

            BattleUiCardView view = CreateCardView(group);
            if (view == null)
                continue;

            _cardViews.Add(view);
            BindCard(view, card);
        }

        RebuildCardLayoutGroups(suitGridViewRoot);
    }

    private void RenderCardsFallback(Comparison<Card> comparison)
    {
        var sorted = new List<Card>(_cards);
        sorted.Sort(comparison);

        RectTransform fallbackRoot = _sortMode == DeckViewSortMode.Rank ? rankGridViewRoot : suitGridViewRoot;
        fallbackRoot ??= rankGridViewRoot != null ? rankGridViewRoot : suitGridViewRoot;
        if (fallbackRoot == null)
            return;

        SetActive(rankGridViewRoot != null ? rankGridViewRoot.gameObject : null, fallbackRoot == rankGridViewRoot);
        SetActive(suitGridViewRoot != null ? suitGridViewRoot.gameObject : null, fallbackRoot == suitGridViewRoot);

        for (int i = 0; i < sorted.Count; i++)
        {
            BattleUiCardView view = CreateCardView(fallbackRoot);
            if (view == null)
                continue;

            _cardViews.Add(view);
            BindCard(view, sorted[i]);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(fallbackRoot);
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

    private void BindCard(BattleUiCardView view, Card card)
    {
        Sprite sprite = assetRegistry != null ? assetRegistry.GetCardFront(card) : null;
        view.Bind(card, sprite, true, false, assetRegistry);

        if (view.Button != null)
            view.Button.onClick.RemoveAllListeners();
    }

    private BattleUiCardView CreateCardView(RectTransform parent)
    {
        BattleUiCardView prefab = ResolveCardPrefab();
        if (prefab == null)
            return null;

        BattleUiCardView view = Instantiate(prefab, parent, false);
        view.name = "Card";
        ConfigureCardInstance(view);
        return view;
    }

    private void ResolveReferences()
    {
        assetRegistry ??= FindLoadedRegistry();
        panelRoot ??= gameObject;
        rankGridViewRoot ??= FindChildRecursive(transform, "RankGridViewRoot") as RectTransform;
        suitGridViewRoot ??= FindChildRecursive(transform, "SuitGridViewRoot") as RectTransform;
        viewByRankButton ??= FindChildRecursive(transform, "ViewByRankButton")?.GetComponent<Button>();
        viewBySuitButton ??= FindChildRecursive(transform, "ViewBySuitButton")?.GetComponent<Button>();
        closeButton ??= FindChildRecursive(transform, "CloseButton")?.GetComponent<Button>();
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;
        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildRecursive(root.GetChild(i), childName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static GameplayAssetRegistry FindLoadedRegistry()
    {
        GameplayAssetRegistry[] registries = Resources.FindObjectsOfTypeAll<GameplayAssetRegistry>();
        return registries.Length > 0 ? registries[0] : null;
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

        Debug.LogWarning($"{nameof(DeckViewPanel)} is missing a card prefab reference.", this);
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

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
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

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
