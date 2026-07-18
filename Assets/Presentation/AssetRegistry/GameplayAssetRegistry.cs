using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameplayAssetRegistry", menuName = "Demon Blackjack/Gameplay Asset Registry")]
public sealed class GameplayAssetRegistry : ScriptableObject
{
    [Header("Card Fallbacks")]
    [SerializeField] private Sprite defaultCardFront;
    [SerializeField] private Sprite cardBack;

    [Header("Card Faces")]
    [SerializeField] private List<CardSpriteEntry> cardSprites = new();

    [Header("Devils")]
    [SerializeField] private Sprite defaultDevilSprite;
    [SerializeField] private List<DevilSpriteEntry> devilSprites = new();

    [Header("Shop Fallbacks")]
    [SerializeField] private Sprite defaultActiveItemSprite;
    [SerializeField] private Sprite defaultRelicSprite;
    [SerializeField] private Sprite defaultCardUpgradeSprite;
    [SerializeField] private List<IdSpriteEntry> activeItemSprites = new();
    [SerializeField] private List<IdSpriteEntry> relicSprites = new();
    [SerializeField] private List<IdSpriteEntry> cardUpgradeSprites = new();

    private Dictionary<CardKey, Sprite> _cardLookup;
    private Dictionary<string, Sprite> _devilLookup;
    private Dictionary<string, Sprite> _activeItemLookup;
    private Dictionary<string, Sprite> _relicLookup;
    private Dictionary<string, Sprite> _cardUpgradeLookup;

    public Sprite DefaultCardFront => defaultCardFront;
    public Sprite CardBack => cardBack != null ? cardBack : defaultCardFront;
    public Sprite DefaultDevilSprite => defaultDevilSprite;
    public IReadOnlyList<CardSpriteEntry> CardSprites => cardSprites;
    public IReadOnlyList<DevilSpriteEntry> DevilSprites => devilSprites;

    public Sprite GetCardFront(Card card)
    {
        return GetCardFront(card.Suit, card.Rank);
    }

    public Sprite GetCardFront(Suit suit, Rank rank)
    {
        EnsureCardLookup();
        return _cardLookup.TryGetValue(new CardKey(suit, rank), out Sprite sprite) && sprite != null
            ? sprite
            : defaultCardFront;
    }

    public Sprite GetDevilSprite(string devilId)
    {
        if (string.IsNullOrWhiteSpace(devilId))
            return defaultDevilSprite;

        EnsureDevilLookup();
        return _devilLookup.TryGetValue(devilId, out Sprite sprite) && sprite != null
            ? sprite
            : defaultDevilSprite;
    }

    // Queries devil sprite with additional string parameter for specific emotions
    public Sprite GetDevilSpriteByEmotion(string devilId, string emotion)
    {
        if (string.IsNullOrWhiteSpace(devilId))
            return defaultDevilSprite;

        EnsureDevilLookup();
        if (string.IsNullOrWhiteSpace(emotion))
        {
            Debug.LogWarning($"GetDevilSpriteByEmotion called with empty emotion for devilId: {devilId}. Returning default devil sprite.");
            return _devilLookup.TryGetValue(devilId, out Sprite defaultEmotionSprite) && defaultEmotionSprite != null
                ? defaultEmotionSprite
                : defaultDevilSprite;
        }
        return _devilLookup.TryGetValue(devilId + "_" + emotion, out Sprite sprite) && sprite != null
            ? sprite
            : defaultDevilSprite;
    }

    public Sprite GetActiveItemSprite(string id) => GetIdSprite(id, activeItemSprites, ref _activeItemLookup, defaultActiveItemSprite != null ? defaultActiveItemSprite : defaultCardFront);
    public Sprite GetRelicSprite(string id) => GetIdSprite(id, relicSprites, ref _relicLookup, defaultRelicSprite != null ? defaultRelicSprite : defaultCardFront);
    public Sprite GetCardUpgradeSprite(string id) => GetIdSprite(id, cardUpgradeSprites, ref _cardUpgradeLookup, defaultCardUpgradeSprite != null ? defaultCardUpgradeSprite : defaultCardFront);

    private void OnValidate()
    {
        _cardLookup = null;
        _devilLookup = null;
        _activeItemLookup = null;
        _relicLookup = null;
        _cardUpgradeLookup = null;
    }

    private static Sprite GetIdSprite(string id, List<IdSpriteEntry> entries, ref Dictionary<string, Sprite> lookup, Sprite fallback)
    {
        if (string.IsNullOrWhiteSpace(id))
            return fallback;

        if (lookup == null)
        {
            lookup = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(entries[i].Id))
                    lookup[entries[i].Id] = entries[i].Sprite;
            }
        }

        return lookup.TryGetValue(id, out Sprite sprite) && sprite != null ? sprite : fallback;
    }

    private void EnsureCardLookup()
    {
        if (_cardLookup != null)
            return;

        _cardLookup = new Dictionary<CardKey, Sprite>();

        for (int i = 0; i < cardSprites.Count; i++)
        {
            CardSpriteEntry entry = cardSprites[i];
            _cardLookup[new CardKey(entry.Suit, entry.Rank)] = entry.Sprite;
        }
    }

    private void EnsureDevilLookup()
    {
        if (_devilLookup != null)
            return;

        _devilLookup = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < devilSprites.Count; i++)
        {
            DevilSpriteEntry entry = devilSprites[i];
            if (!string.IsNullOrWhiteSpace(entry.DevilId))
                _devilLookup[entry.DevilId] = entry.Sprite;
        }
    }

    private readonly struct CardKey : IEquatable<CardKey>
    {
        public CardKey(Suit suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        private Suit Suit { get; }
        private Rank Rank { get; }

        public bool Equals(CardKey other)
        {
            return Suit == other.Suit && Rank == other.Rank;
        }

        public override bool Equals(object obj)
        {
            return obj is CardKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Suit * 397) ^ (int)Rank;
            }
        }
    }
}

[Serializable]
public struct IdSpriteEntry
{
    [SerializeField] private string id;
    [SerializeField] private Sprite sprite;

    public string Id => id;
    public Sprite Sprite => sprite;
}

[Serializable]
public struct CardSpriteEntry
{
    [SerializeField] private Suit suit;
    [SerializeField] private Rank rank;
    [SerializeField] private Sprite sprite;

    public CardSpriteEntry(Suit suit, Rank rank, Sprite sprite)
    {
        this.suit = suit;
        this.rank = rank;
        this.sprite = sprite;
    }

    public Suit Suit => suit;
    public Rank Rank => rank;
    public Sprite Sprite => sprite;
}

[Serializable]
public struct DevilSpriteEntry
{
    [SerializeField] private string devilId;
    [SerializeField] private Sprite sprite;

    public DevilSpriteEntry(string devilId, Sprite sprite)
    {
        this.devilId = devilId;
        this.sprite = sprite;
    }

    public string DevilId => devilId;
    public Sprite Sprite => sprite;
}
