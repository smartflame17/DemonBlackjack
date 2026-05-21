using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleUiCardView : MonoBehaviour
{
    [SerializeField] private Image image;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Button button;

    public Button Button => button;

    public void Initialize(Image cardImage, TMP_Text cardLabel, Button cardButton)
    {
        image = cardImage;
        label = cardLabel;
        button = cardButton;
    }

    public void Bind(Card card, Sprite sprite, bool faceUp, bool interactable)
    {
        EnsureReferences();

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

        if (image != null)
            image.sprite = sprite;

        if (label != null)
            label.text = string.Empty;

        if (button != null)
            button.interactable = false;
    }

    private void EnsureReferences()
    {
        if (image == null)
            image = GetComponent<Image>();

        if (button == null)
            button = GetComponent<Button>();

        if (label == null)
            label = GetComponentInChildren<TMP_Text>();
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
