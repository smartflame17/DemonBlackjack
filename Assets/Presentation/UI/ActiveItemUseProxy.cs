using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ActiveItemUseProxy : MonoBehaviour
{
    [SerializeField] private BattleController battleController;
    [SerializeField] private CardSelectionPanel cardSelectionPanel;
    [SerializeField] private MoneyInputPanel moneyInputPanel;

    private BattleState _pendingBattle;
    private CardSelectionPanel _activeCardPanel;
    private MoneyInputPanel _activeMoneyPanel;

    public bool HasPendingSelection => _activeCardPanel != null;
    public bool HasPendingUse => _pendingBattle != null;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (_pendingBattle == null)
            return;

        if (battleController == null
            || !ReferenceEquals(battleController.BattleState, _pendingBattle)
            || !_pendingBattle.HasPendingActiveItemUse)
        {
            CancelPendingUse();
        }
    }

    private void OnDisable()
    {
        CancelPendingUse();
    }

    public void Initialize(BattleController controller)
    {
        battleController ??= controller;
        ResolveReferences();
    }

    public bool TryUseActiveItem(string itemId)
    {
        ResolveReferences();
        int slotIndex = battleController?.BattleState?.RunState.FindActiveItemSlotIndex(itemId) ?? -1;
        return TryUseActiveItemAtSlot(slotIndex);
    }

    public bool TryUseActiveItemAtSlot(int slotIndex)
    {
        ResolveReferences();
        if (battleController == null || _pendingBattle != null)
            return false;

        ActiveItemUseStartResult result = battleController.TryBeginActiveItemUseAtSlot(
            slotIndex,
            out ActiveItemUseRequest request);
        if (result == ActiveItemUseStartResult.Applied)
            return true;
        if (result != ActiveItemUseStartResult.SelectionRequired
            && result != ActiveItemUseStartResult.MoneyInputRequired)
        {
            return false;
        }

        BattleState battle = battleController.BattleState;
        if (battle == null || !CanPresent(request))
        {
            battle?.CancelPendingActiveItemUse();
            return false;
        }

        AttachPendingBattle(battle);
        try
        {
            if (request.InputKind == ActiveItemUseInputKind.CardSelection)
            {
                _activeCardPanel = cardSelectionPanel;
                _activeCardPanel.SelectionCancelled += OnPanelCancelled;
                _activeCardPanel.Show(
                    request.CardSelection.Cards,
                    OnCardsChosen,
                    request.CardSelection.SelectionCount);
            }
            else
            {
                _activeMoneyPanel = moneyInputPanel;
                _activeMoneyPanel.Show(request.MaximumAmount, OnMoneyConfirmed, OnPanelCancelled);
            }
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            CancelPendingUse();
            return false;
        }
    }

    public void CancelSelection()
    {
        CancelPendingUse();
    }

    public void CancelPendingUse()
    {
        BattleState battle = _pendingBattle;
        CardSelectionPanel cardPanel = _activeCardPanel;
        MoneyInputPanel moneyPanel = _activeMoneyPanel;
        DetachPendingUse();

        battle?.CancelPendingActiveItemUse();
        if (cardPanel != null && cardPanel.IsOpen)
            cardPanel.Cancel();
        if (moneyPanel != null && moneyPanel.IsOpen)
            moneyPanel.Cancel();
    }

    private bool CanPresent(ActiveItemUseRequest request)
    {
        return request.InputKind switch
        {
            ActiveItemUseInputKind.CardSelection => cardSelectionPanel != null
                && !cardSelectionPanel.IsOpen
                && (moneyInputPanel == null || !moneyInputPanel.IsOpen),
            ActiveItemUseInputKind.MoneyInput => moneyInputPanel != null
                && !moneyInputPanel.IsOpen
                && (cardSelectionPanel == null || !cardSelectionPanel.IsOpen),
            _ => false
        };
    }

    private void AttachPendingBattle(BattleState battle)
    {
        _pendingBattle = battle;
        _pendingBattle.ActiveItemUseCancelled += OnBattleUseCancelled;
        _pendingBattle.EventBus.Subscribe<RoundEndedEvent>(OnRoundEnded);
    }

    private void OnCardsChosen(IReadOnlyList<int> selectedIndices)
    {
        BattleState battle = _pendingBattle;
        DetachPendingUse();

        bool completed = battleController != null
            && ReferenceEquals(battleController.BattleState, battle)
            && battleController.TryCompletePendingActiveItemUse(selectedIndices);
        if (!completed)
            battle?.CancelPendingActiveItemUse();
    }

    private void OnMoneyConfirmed(int amount)
    {
        BattleState battle = _pendingBattle;
        DetachPendingUse();

        bool completed = battleController != null
            && ReferenceEquals(battleController.BattleState, battle)
            && battleController.TryCompletePendingActiveItemMoneyUse(amount);
        if (!completed)
            battle?.CancelPendingActiveItemUse();
    }

    private void OnPanelCancelled()
    {
        BattleState battle = _pendingBattle;
        DetachPendingUse();
        battle?.CancelPendingActiveItemUse();
    }

    private void OnBattleUseCancelled()
    {
        CardSelectionPanel cardPanel = _activeCardPanel;
        MoneyInputPanel moneyPanel = _activeMoneyPanel;
        DetachPendingUse();
        if (cardPanel != null && cardPanel.IsOpen)
            cardPanel.Cancel();
        if (moneyPanel != null && moneyPanel.IsOpen)
            moneyPanel.Cancel();
    }

    private void OnRoundEnded(RoundEndedEvent eventData)
    {
        CancelPendingUse();
    }

    private void DetachPendingUse()
    {
        if (_activeCardPanel != null)
            _activeCardPanel.SelectionCancelled -= OnPanelCancelled;
        if (_pendingBattle != null)
        {
            _pendingBattle.ActiveItemUseCancelled -= OnBattleUseCancelled;
            _pendingBattle.EventBus.Unsubscribe<RoundEndedEvent>(OnRoundEnded);
        }

        _activeCardPanel = null;
        _activeMoneyPanel = null;
        _pendingBattle = null;
    }

    private void ResolveReferences()
    {
        battleController ??= FindFirstObjectByType<BattleController>();
        cardSelectionPanel ??= GetComponentInChildren<CardSelectionPanel>(true);
        moneyInputPanel ??= GetComponentInChildren<MoneyInputPanel>(true);
    }
}
