using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleUiCardView : MonoBehaviour
{
    private static readonly Vector2 SelectedOffset = new(0f, 18f);

    [SerializeField] private Image image;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Button button;
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private Image upgradeImage;
    [SerializeField] private TooltipTrigger tooltipTrigger;

    private Vector2 _baseAnchoredPosition;
    private bool _hasBasePosition;

    public Button Button => button;
    public RectTransform RectTransform => transform as RectTransform;

    public void Initialize(Image cardImage, TMP_Text cardLabel, Button cardButton)
    {
        image = cardImage;
        label = cardLabel;
        button = cardButton;
        visualRoot = cardImage != null ? cardImage.rectTransform : transform as RectTransform;
        if (button != null && button.targetGraphic == null)
            button.targetGraphic = image;

        CacheBasePosition();
    }

    public Button EnsureButton()
    {
        EnsureReferences();

        if (button == null && image != null)
            button = image.gameObject.AddComponent<Button>();

        if (button != null && button.targetGraphic == null)
            button.targetGraphic = image;

        return button;
    }

    public void Bind(Card card, Sprite sprite, bool faceUp, bool interactable)
    {
        Bind(card, sprite, faceUp, interactable, null);
    }

    public void Bind(Card card, Sprite sprite, bool faceUp, bool interactable, GameplayAssetRegistry assetRegistry)
    {
        EnsureReferences();
        SetSelected(false);

        if (image != null)
            image.sprite = sprite;

        if (label != null)
            label.text = faceUp ? FormatCard(card) : string.Empty;

        if (button != null)
            button.interactable = interactable;

        BindUpgrade(card, faceUp, assetRegistry);

        if (faceUp && card.HasModifier)
            tooltipTrigger.Bind(card.ModifierId);
        else
            tooltipTrigger.Clear();
    }

    public void BindBack(Sprite sprite)
    {
        EnsureReferences();
        SetSelected(false);

        if (image != null)
            image.sprite = sprite;

        if (label != null)
            label.text = string.Empty;

        if (button != null)
            button.interactable = false;

        ClearUpgrade();
        tooltipTrigger.Clear();
    }

    public void SetVisualColor(Color color)
    {
        EnsureReferences();

        if (image != null)
            image.color = color;

        if (label != null)
            label.color = color;

        if (upgradeImage != null)
            upgradeImage.color = color;
    }

    public void SetSelected(bool selected)
    {
        EnsureReferences();

        if (visualRoot == null)
            return;

        if (!_hasBasePosition)
            CacheBasePosition();

        visualRoot.anchoredPosition = _baseAnchoredPosition + (selected ? SelectedOffset : Vector2.zero);
    }

    private void EnsureReferences()
    {
        if (image == null)
            image = GetComponent<Image>();

        if (button == null)
            button = GetComponentInChildren<Button>(true);

        if (label == null)
            label = GetComponentInChildren<TMP_Text>();

        if (visualRoot == null)
            visualRoot = image != null ? image.rectTransform : transform as RectTransform;

        if (upgradeImage == null)
            upgradeImage = FindChildImage("Upgrade");

        tooltipTrigger ??= GetComponent<TooltipTrigger>() ?? gameObject.AddComponent<TooltipTrigger>();

        if (!_hasBasePosition)
            CacheBasePosition();
    }

    private void BindUpgrade(Card card, bool faceUp, GameplayAssetRegistry assetRegistry)
    {
        if (upgradeImage == null)
            return;

        if (!faceUp || !card.HasModifier || assetRegistry == null)
        {
            ClearUpgrade();
            return;
        }

        upgradeImage.sprite = assetRegistry.GetCardUpgradeSprite(card.ModifierId);
        upgradeImage.gameObject.SetActive(upgradeImage.sprite != null);
    }

    private void ClearUpgrade()
    {
        if (upgradeImage == null)
            return;

        upgradeImage.sprite = null;
        upgradeImage.gameObject.SetActive(false);
    }

    private Image FindChildImage(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName && children[i].TryGetComponent(out Image childImage))
                return childImage;
        }

        return null;
    }

    private void CacheBasePosition()
    {
        if (visualRoot == null)
            return;

        _baseAnchoredPosition = visualRoot.anchoredPosition;
        _hasBasePosition = true;
    }

    private static string FormatCard(Card card)
    {
        return $"{RankLabel(card.Rank)}\n{SuitLabel(card.Suit)}";
    }

    private static string RankLabel(Rank rank)
    {
        return rank switch
        {
            Rank.Ace => "A",
            Rank.Jack => "J",
            Rank.Queen => "Q",
            Rank.King => "K",
            _ => ((int)rank).ToString()
        };
    }

    private static string SuitLabel(Suit suit)
    {
        return suit switch
        {
            Suit.Hearts => "H",
            Suit.Diamonds => "D",
            Suit.Clubs => "C",
            Suit.Spades => "S",
            _ => "?"
        };
    }
}
