using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ActiveItemUseProxy : MonoBehaviour
{
    [SerializeField] private BattleController battleController;
    [SerializeField] private CardSelectionPanel cardSelectionPanel;

    private BattleState _selectionBattle;
    private CardSelectionPanel _selectionPanel;

    public bool HasPendingSelection => _selectionBattle != null;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (_selectionBattle == null)
            return;

        if (battleController == null
            || !ReferenceEquals(battleController.BattleState, _selectionBattle)
            || !_selectionBattle.HasPendingActiveItemSelection)
        {
            CancelSelection();
        }
    }

    private void OnDisable()
    {
        CancelSelection();
    }

    public void Initialize(BattleController controller)
    {
        battleController ??= controller;
        ResolveReferences();
    }

    public bool TryUseActiveItem(string itemId)
    {
        ResolveReferences();
        if (battleController == null || _selectionBattle != null)
            return false;

        if (ActiveItemResolver.RequiresCardSelection(itemId)
            && (cardSelectionPanel == null || cardSelectionPanel.IsOpen))
        {
            return false;
        }

        ActiveItemUseStartResult result = battleController.TryBeginActiveItemUse(itemId, out ActiveItemSelectionRequest request);
        if (result == ActiveItemUseStartResult.Applied)
            return true;
        if (result != ActiveItemUseStartResult.SelectionRequired)
            return false;

        BattleState battle = battleController.BattleState;
        if (battle == null || cardSelectionPanel == null)
        {
            battle?.CancelPendingActiveItemUse();
            return false;
        }

        _selectionBattle = battle;
        _selectionPanel = cardSelectionPanel;
        _selectionPanel.SelectionCancelled += OnPanelSelectionCancelled;
        _selectionBattle.ActiveItemSelectionCancelled += OnBattleSelectionCancelled;
        _selectionBattle.EventBus.Subscribe<RoundEndedEvent>(OnRoundEnded);

        try
        {
            _selectionPanel.Show(request.Cards, OnCardsChosen, request.SelectionCount);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            CancelSelection();
            return false;
        }
    }

    public void CancelSelection()
    {
        BattleState battle = _selectionBattle;
        CardSelectionPanel panel = _selectionPanel;
        DetachSelection();

        battle?.CancelPendingActiveItemUse();
        if (panel != null && panel.IsOpen)
            panel.Cancel();
    }

    private void OnCardsChosen(IReadOnlyList<int> selectedIndices)
    {
        BattleState battle = _selectionBattle;
        DetachSelection();

        bool completed = battleController != null
            && ReferenceEquals(battleController.BattleState, battle)
            && battleController.TryCompletePendingActiveItemUse(selectedIndices);
        if (!completed)
            battle?.CancelPendingActiveItemUse();
    }

    private void OnPanelSelectionCancelled()
    {
        BattleState battle = _selectionBattle;
        DetachSelection();
        battle?.CancelPendingActiveItemUse();
    }

    private void OnBattleSelectionCancelled()
    {
        CardSelectionPanel panel = _selectionPanel;
        DetachSelection();
        if (panel != null && panel.IsOpen)
            panel.Cancel();
    }

    private void OnRoundEnded(RoundEndedEvent eventData)
    {
        CancelSelection();
    }

    private void DetachSelection()
    {
        if (_selectionPanel != null)
            _selectionPanel.SelectionCancelled -= OnPanelSelectionCancelled;
        if (_selectionBattle != null)
        {
            _selectionBattle.ActiveItemSelectionCancelled -= OnBattleSelectionCancelled;
            _selectionBattle.EventBus.Unsubscribe<RoundEndedEvent>(OnRoundEnded);
        }

        _selectionPanel = null;
        _selectionBattle = null;
    }

    private void ResolveReferences()
    {
        battleController ??= FindFirstObjectByType<BattleController>();
        cardSelectionPanel ??= GetComponentInChildren<CardSelectionPanel>(true);
    }
}
