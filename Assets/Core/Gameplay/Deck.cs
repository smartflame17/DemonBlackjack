using System;
using System.Collections.Generic;

public sealed class Deck
{
    private readonly List<Card> _drawPile;
    private readonly List<Card> _discardPile;
    private readonly ReplayableRandom _random;

    public Deck(IEnumerable<Card> cards, int seed, bool shuffle = true, bool cardsAreDrawOrder = false)
    {
        if (cards == null)
            throw new ArgumentNullException(nameof(cards));

        _drawPile = new List<Card>(cards);
        _discardPile = new List<Card>();
        _random = new ReplayableRandom(seed);
        if (cardsAreDrawOrder)
            _drawPile.Reverse();
        if (shuffle)
            ShuffleDrawPile();
    }

    private Deck(IEnumerable<Card> drawPile, IEnumerable<Card> discardPile, RandomStateData randomState, int fallbackSeed)
    {
        _drawPile = drawPile != null ? new List<Card>(drawPile) : new List<Card>();
        _discardPile = discardPile != null ? new List<Card>(discardPile) : new List<Card>();
        _random = ReplayableRandom.FromData(randomState, fallbackSeed);
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

    public bool TryDrawWhere(Predicate<Card> predicate, ReplayableRandom random, out Card card)
    {
        if (predicate == null)
        {
            card = default;
            return false;
        }

        var matchingIndices = new List<int>();
        for (int i = 0; i < _drawPile.Count; i++)
        {
            if (predicate(_drawPile[i]))
                matchingIndices.Add(i);
        }

        if (matchingIndices.Count == 0)
        {
            card = default;
            return false;
        }

        random ??= new ReplayableRandom(0);
        int drawIndex = matchingIndices[random.Next(matchingIndices.Count)];
        card = _drawPile[drawIndex];
        _drawPile.RemoveAt(drawIndex);
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

    public void PlaceAtBottom(Card card)
    {
        _drawPile.Insert(0, card);
    }

    public void TransformCards(Func<Card, Card> transform)
    {
        if (transform == null)
            return;

        TransformCards(_drawPile, transform);
        TransformCards(_discardPile, transform);
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

    public DeckData ToData()
    {
        var data = new DeckData { randomState = _random.ToData() };
        AddCards(data.drawPile, _drawPile);
        AddCards(data.discardPile, _discardPile);
        return data;
    }

    public static Deck FromData(DeckData data, int fallbackSeed)
    {
        if (data == null)
            return null;

        return new Deck(
            ConvertCards(data.drawPile),
            ConvertCards(data.discardPile),
            data.randomState,
            fallbackSeed);
    }

    private static void AddCards(List<CardData> target, IReadOnlyList<Card> source)
    {
        for (int i = 0; i < source.Count; i++)
            target.Add(CardData.FromCard(source[i]));
    }

    private static List<Card> ConvertCards(List<CardData> source)
    {
        var cards = new List<Card>();
        if (source == null)
            return cards;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
                cards.Add(source[i].ToCard());
        }

        return cards;
    }

    private static void TransformCards(List<Card> cards, Func<Card, Card> transform)
    {
        for (int i = 0; i < cards.Count; i++)
            cards[i] = transform(cards[i]);
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

    public static List<Card> CreateDeckOfSuits(Suit suit, int duplicateCount = 4)
    {
        var cards = new List<Card>(13 * duplicateCount);

        for (int i = 0; i < duplicateCount; i++)
        {
            foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                cards.Add(new Card(suit, rank));
        }

        return cards;
    }

}
