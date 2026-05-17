using System;
using System.Collections.Generic;

public sealed class Deck
{
    private readonly List<Card> _drawPile;
    private readonly List<Card> _discardPile;
    private readonly Random _random;

    public Deck(IEnumerable<Card> cards, int seed)
    {
        if (cards == null)
            throw new ArgumentNullException(nameof(cards));

        _drawPile = new List<Card>(cards);
        _discardPile = new List<Card>();
        _random = new Random(seed);
        ShuffleDrawPile();
    }

    public IReadOnlyList<Card> DrawPile => _drawPile;
    public IReadOnlyList<Card> DiscardPile => _discardPile;
    public int RemainingCards => _drawPile.Count;
    public int DiscardedCards => _discardPile.Count;

    public bool TryDraw(out Card card)
    {
        if (_drawPile.Count == 0 && _discardPile.Count > 0)
            ReshuffleDiscardIntoDraw();

        if (_drawPile.Count == 0)
        {
            card = default;
            return false;
        }

        int lastIndex = _drawPile.Count - 1;
        card = _drawPile[lastIndex];
        _drawPile.RemoveAt(lastIndex);
        return true;
    }

    public void Discard(Card card)
    {
        _discardPile.Add(card);
    }

    public void DiscardRange(IEnumerable<Card> cards)
    {
        if (cards == null)
            return;

        _discardPile.AddRange(cards);
    }

    public bool ReshuffleDiscardIntoDraw()
    {
        if (_discardPile.Count == 0)
            return false;

        _drawPile.AddRange(_discardPile);
        _discardPile.Clear();
        ShuffleDrawPile();
        return true;
    }

    private void ShuffleDrawPile()
    {
        for (int i = _drawPile.Count - 1; i > 0; i--)
        {
            int swapIndex = _random.Next(i + 1);
            (_drawPile[i], _drawPile[swapIndex]) = (_drawPile[swapIndex], _drawPile[i]);
        }
    }

    public static List<Card> CreateStandardDeck()
    {
        var cards = new List<Card>(52);

        foreach (Suit suit in Enum.GetValues(typeof(Suit)))
        {
            foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                cards.Add(new Card(suit, rank));
        }

        return cards;
    }
}
