using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class CardSelectionPanel : MonoBehaviour
{
    [SerializeField] private GameplayAssetRegistry assetRegistry;
    [SerializeField] private BattleUiCardView cardPrefab;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform cardContainer;
    [SerializeField] private GridLayoutGroup gridLayout;
    [SerializeField] private Button chooseButton;
    [SerializeField, Min(1)] private int cardsPerRow = 7;
    [SerializeField, Min(1f)] private float selectedScale = 1.15f;

    private readonly List<Card> _cards = new();
    private readonly List<BattleUiCardView> _cardViews = new();
    private readonly List<int> _selectedIndices = new();
    private readonly HashSet<int> _selectedIndexSet = new();

    private Action<IReadOnlyList<int>> _onChosen;
    private int _selectionCount = 1;
    private bool _requestActive;

    public bool IsOpen => _requestActive;

    private void Awake()
    {
        ResolveReferences();
        ConfigureGrid();
        RefreshSelectionState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ConfigureGrid();
        BindChooseButton();
        RefreshSelectionState();
    }

    private void OnDisable()
    {
        if (chooseButton != null)
            chooseButton.onClick.RemoveListener(ConfirmSelection);

        CleanupRequest();
    }

    private void OnValidate()
    {
        cardsPerRow = Mathf.Max(1, cardsPerRow);
        selectedScale = Mathf.Max(1f, selectedScale);
        ResolveReferences();
        ConfigureGrid();
    }

    public void Show(
        IReadOnlyList<Card> cards,
        Action<IReadOnlyList<int>> onChosen,
        int selectionCount = 1)
    {
        if (_requestActive)
            throw new InvalidOperationException("A card selection request is already active.");
        if (cards == null)
            throw new ArgumentNullException(nameof(cards));
        if (onChosen == null)
            throw new ArgumentNullException(nameof(onChosen));
        if (selectionCount < 1 || selectionCount > cards.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selectionCount),
                selectionCount,
                "Selection count must be at least one and cannot exceed the number of cards.");
        }

        ResolveReferences();
        ValidateConfiguration();
        ConfigureGrid();
        BindChooseButton();

        _cards.Clear();
        for (int i = 0; i < cards.Count; i++)
            _cards.Add(cards[i]);

        _selectedIndices.Clear();
        _selectedIndexSet.Clear();
        _selectionCount = selectionCount;
        _onChosen = onChosen;
        _requestActive = true;

        RenderCards();
        RefreshSelectionState();

        if (panelRoot != null && !panelRoot.activeSelf)
            panelRoot.SetActive(true);
    }

    private void ToggleSelection(int index)
    {
        if (!_requestActive || index < 0 || index >= _cardViews.Count)
            return;

        if (_selectedIndexSet.Remove(index))
        {
            _selectedIndices.Remove(index);
        }
        else
        {
            if (_selectedIndices.Count >= _selectionCount)
                return;

            _selectedIndexSet.Add(index);
            _selectedIndices.Add(index);
        }

        RefreshSelectionState();
    }

    private void ConfirmSelection()
    {
        if (!_requestActive || _selectedIndices.Count != _selectionCount)
            return;

        IReadOnlyList<int> result = Array.AsReadOnly(_selectedIndices.ToArray());
        Action<IReadOnlyList<int>> callback = _onChosen;

        _onChosen = null;
        _requestActive = false;

        if (panelRoot != null && panelRoot.activeSelf)
            panelRoot.SetActive(false);

        CleanupRequest();

        callback?.Invoke(result);
    }

    private void RenderCards()
    {
        DestroyCardViews();

        for (int i = 0; i < _cards.Count; i++)
        {
            int cardIndex = i;
            BattleUiCardView view = Instantiate(cardPrefab, cardContainer, false);
            view.name = $"Card_{cardIndex}";
            view.SetVisualScale(1f);

            Card card = _cards[cardIndex];
            Sprite sprite = assetRegistry != null ? assetRegistry.GetCardFront(card) : null;
            view.Bind(card, sprite, true, true, assetRegistry);

            Button button = view.EnsureButton();
            if (button == null)
            {
                DestroyGeneratedObject(view.gameObject);
                throw new InvalidOperationException("The configured card prefab must provide a Button.");
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ToggleSelection(cardIndex));
            _cardViews.Add(view);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(cardContainer);
    }

    private void RefreshSelectionState()
    {
        bool selectionComplete = _requestActive && _selectedIndices.Count == _selectionCount;
        bool atSelectionLimit = selectionComplete;

        if (chooseButton != null)
            chooseButton.interactable = selectionComplete;

        for (int i = 0; i < _cardViews.Count; i++)
        {
            BattleUiCardView view = _cardViews[i];
            if (view == null)
                continue;

            bool selected = _selectedIndexSet.Contains(i);
            view.SetVisualScale(selected ? selectedScale : 1f);

            Button button = view.Button;
            if (button != null)
                button.interactable = _requestActive && (selected || !atSelectionLimit);
        }
    }

    private void CleanupRequest()
    {
        if (chooseButton != null)
            chooseButton.onClick.RemoveListener(ConfirmSelection);

        DestroyCardViews();
        _cards.Clear();
        _selectedIndices.Clear();
        _selectedIndexSet.Clear();
        _onChosen = null;
        _selectionCount = 1;
        _requestActive = false;

        if (chooseButton != null)
            chooseButton.interactable = false;
    }

    private void DestroyCardViews()
    {
        for (int i = _cardViews.Count - 1; i >= 0; i--)
        {
            BattleUiCardView view = _cardViews[i];
            if (view != null)
                DestroyGeneratedObject(view.gameObject);
        }

        _cardViews.Clear();
    }

    private void ResolveReferences()
    {
        panelRoot ??= gameObject;
        cardContainer ??= FindChildRecursive(transform, "CardGridContent") as RectTransform;
        gridLayout ??= cardContainer != null ? cardContainer.GetComponent<GridLayoutGroup>() : null;
        chooseButton ??= FindChildRecursive(transform, "ChooseButton")?.GetComponent<Button>();
        assetRegistry ??= FindLoadedRegistry();
        cardPrefab ??= FindLoadedCardPrefab();
    }

    private void ValidateConfiguration()
    {
        if (panelRoot == null)
            throw new InvalidOperationException($"{nameof(CardSelectionPanel)} is missing its panel root.");
        if (cardContainer == null)
            throw new InvalidOperationException($"{nameof(CardSelectionPanel)} is missing its card container.");
        if (gridLayout == null)
            throw new InvalidOperationException($"{nameof(CardSelectionPanel)} is missing its grid layout.");
        if (chooseButton == null)
            throw new InvalidOperationException($"{nameof(CardSelectionPanel)} is missing its Choose button.");
        if (cardPrefab == null)
            throw new InvalidOperationException($"{nameof(CardSelectionPanel)} is missing its card prefab.");
    }

    private void ConfigureGrid()
    {
        if (gridLayout == null)
            return;

        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = Mathf.Max(1, cardsPerRow);
    }

    private void BindChooseButton()
    {
        if (chooseButton == null)
            return;

        chooseButton.onClick.RemoveListener(ConfirmSelection);
        chooseButton.onClick.AddListener(ConfirmSelection);
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

    private static BattleUiCardView FindLoadedCardPrefab()
    {
        BattleUiCardView[] candidates = Resources.FindObjectsOfTypeAll<BattleUiCardView>();
        for (int i = 0; i < candidates.Length; i++)
        {
            BattleUiCardView candidate = candidates[i];
            if (candidate != null && candidate.name == "CardPrefab" && !candidate.gameObject.scene.IsValid())
                return candidate;
        }

        return null;
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
