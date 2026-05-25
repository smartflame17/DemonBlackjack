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

    private Vector2 _baseAnchoredPosition;
    private bool _hasBasePosition;

    public Button Button => button;

    public void Initialize(Image cardImage, TMP_Text cardLabel, Button cardButton)
    {
        image = cardImage;
        label = cardLabel;
        button = cardButton;
        visualRoot = cardImage != null ? cardImage.rectTransform : transform as RectTransform;
        CacheBasePosition();
        Debug.Log("BattleUiCard initialized with image: " + (cardImage != null) + ", label: " + (cardLabel != null) + ", button: " + (cardButton != null));
    }

    public void Bind(Card card, Sprite sprite, bool faceUp, bool interactable)
    {
        EnsureReferences();
        SetSelected(false);

        if (image != null)
            image.sprite = sprite;

        if (label != null)
            label.text = faceUp ? FormatCard(card) : string.Empty;

        if (button != null)
            button.interactable = interactable;
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
            button = GetComponent<Button>();

        if (label == null)
            label = GetComponentInChildren<TMP_Text>();

        if (visualRoot == null)
            visualRoot = image != null ? image.rectTransform : transform as RectTransform;

        if (!_hasBasePosition)
            CacheBasePosition();
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
