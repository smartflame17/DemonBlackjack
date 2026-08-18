using System;
using System.Collections.Generic;

public enum ActiveItemUseStartResult
{
    Rejected,
    Applied,
    SelectionRequired,
    MoneyInputRequired
}

public enum ActiveItemUseInputKind
{
    None,
    CardSelection,
    MoneyInput
}

public readonly struct ActiveItemUseRequest
{
    private ActiveItemUseRequest(
        int slotIndex,
        string itemId,
        ActiveItemUseInputKind inputKind,
        ActiveItemSelectionRequest cardSelection,
        int minimumAmount,
        int maximumAmount)
    {
        SlotIndex = slotIndex;
        ItemId = itemId;
        InputKind = inputKind;
        CardSelection = cardSelection;
        MinimumAmount = minimumAmount;
        MaximumAmount = maximumAmount;
    }

    public int SlotIndex { get; }
    public string ItemId { get; }
    public ActiveItemUseInputKind InputKind { get; }
    public ActiveItemSelectionRequest CardSelection { get; }
    public int MinimumAmount { get; }
    public int MaximumAmount { get; }

    public static ActiveItemUseRequest ForCardSelection(
        int slotIndex,
        ActiveItemSelectionRequest selection)
    {
        return new ActiveItemUseRequest(
            slotIndex,
            selection.ItemId,
            ActiveItemUseInputKind.CardSelection,
            selection,
            0,
            0);
    }

    public static ActiveItemUseRequest ForMoneyInput(int slotIndex, string itemId, int minimumAmount, int maximumAmount)
    {
        return new ActiveItemUseRequest(
            slotIndex,
            itemId,
            ActiveItemUseInputKind.MoneyInput,
            default,
            minimumAmount,
            maximumAmount);
    }
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
