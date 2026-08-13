using System;
using System.Collections.Generic;

public enum ActiveItemUseStartResult
{
    Rejected,
    Applied,
    SelectionRequired
}

public readonly struct ActiveItemSelectionRequest
{
    public ActiveItemSelectionRequest(string itemId, IReadOnlyList<Card> cards, int selectionCount = 1)
    {
        ItemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
        if (cards == null)
            throw new ArgumentNullException(nameof(cards));
        if (selectionCount < 1 || selectionCount > cards.Count)
            throw new ArgumentOutOfRangeException(nameof(selectionCount));

        var snapshot = new Card[cards.Count];
        for (int i = 0; i < cards.Count; i++)
            snapshot[i] = cards[i];

        Cards = Array.AsReadOnly(snapshot);
        SelectionCount = selectionCount;
    }

    public string ItemId { get; }
    public IReadOnlyList<Card> Cards { get; }
    public int SelectionCount { get; }
}
