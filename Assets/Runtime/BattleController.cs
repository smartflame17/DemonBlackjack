using System.Collections.Generic;
using System;
using UnityEngine;

public class BattleController : MonoBehaviour
{
    public BattleState BattleState { get; private set; }
    public CommandQueue CommandQueue => BattleState?.CommandQueue;
    public bool HasActiveBattle => BattleState != null && !BattleState.IsBattleOver;
    public bool HasPendingActiveItemSelection => BattleState != null && BattleState.HasPendingActiveItemSelection;
    public bool HasPendingActiveItemUse => BattleState != null && BattleState.HasPendingActiveItemUse;
    public bool IsWaitingForVisuals { get; private set; }
    public IBattleInputGate InputGate { get; set; }

    public void InitializeBattle(RunState runState, BattleConfig config)
    {
        CleanupBattle();

        BattleState = new BattleState(runState, config);
        BattleState.Initialize();
    }

    public bool RestoreBattle(RunState runState, BattleStateData data)
    {
        CleanupBattle();

        BattleState restoredBattle;
        try
        {
            restoredBattle = BattleState.FromData(runState, data);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to restore battle checkpoint: {exception.Message}");
            return false;
        }
        if (restoredBattle == null)
            return false;

        BattleState = restoredBattle;
        IsWaitingForVisuals = true;
        return true;
    }

    public bool StartNextRound(int wager)
    {
        if (BattleState == null || BattleState.IsBattleOver || IsWaitingForVisuals)
            return false;

        if (InputGate != null && !InputGate.CanStartRound(BattleState, wager))
            return false;

        bool started = BattleState.StartRound(wager);
        if (started)
            Debug.Log($"<color=blue>[Round]</color> Wager: {wager}, Player phase started");
        return started;
    }

    public bool TryPlayCard(int handIndex)
    {
        return !IsWaitingForVisuals
            && BattleState != null
            && (InputGate == null || InputGate.CanPlayCard(BattleState, handIndex))
            && BattleState.TryPlayCard(handIndex);
    }

    public bool TryHit()
    {
        if (IsWaitingForVisuals || BattleState == null)
            return false;

        BattleState activeBattle = BattleState;
        if (InputGate != null && !InputGate.CanHit(activeBattle))
            return false;

        bool result = activeBattle.TryHit();
        MarkWaitingForCompletedRound(activeBattle);
        return result;
    }

    public bool TryHitWithoutEndingTurn()
    {
        return !IsWaitingForVisuals
            && BattleState != null
            && (InputGate == null || InputGate.CanHit(BattleState))
            && BattleState.TryHitWithoutEndingTurn();
    }

    public bool TryStand()
    {
        if (IsWaitingForVisuals || BattleState == null)
            return false;

        BattleState activeBattle = BattleState;
        if (InputGate != null && !InputGate.CanStand(activeBattle))
            return false;

        bool result = activeBattle.TryStand();
        if (result)
            InputGate?.NotifyStandAccepted(activeBattle);
        MarkWaitingForCompletedRound(activeBattle);
        return result;
    }

    public bool TryUseActiveItem(string itemId)
    {
        return !IsWaitingForVisuals
            && BattleState != null
            && (InputGate == null || InputGate.CanUseActiveItem(BattleState, itemId))
            && BattleState.TryUseActiveItem(itemId);
    }

    public bool TryUseActiveItemAtSlot(int slotIndex)
    {
        if (!CanUseActiveItemAtSlot(slotIndex))
            return false;

        return BattleState.TryUseActiveItemAtSlot(slotIndex);
    }

    public ActiveItemUseStartResult TryBeginActiveItemUse(
        string itemId,
        out ActiveItemSelectionRequest selectionRequest)
    {
        selectionRequest = default;
        if (IsWaitingForVisuals
            || BattleState == null
            || InputGate != null && !InputGate.CanUseActiveItem(BattleState, itemId))
        {
            return ActiveItemUseStartResult.Rejected;
        }

        return BattleState.TryBeginActiveItemUse(itemId, out selectionRequest);
    }

    public ActiveItemUseStartResult TryBeginActiveItemUseAtSlot(
        int slotIndex,
        out ActiveItemUseRequest request)
    {
        request = default;
        if (IsWaitingForVisuals
            || BattleState == null
            || !BattleState.RunState.TryGetActiveItemAt(slotIndex, out ActiveItemRuntimeState item)
            || InputGate != null && !InputGate.CanUseActiveItem(BattleState, item.ItemId))
        {
            return ActiveItemUseStartResult.Rejected;
        }

        return BattleState.TryBeginActiveItemUseAtSlot(slotIndex, out request);
    }

    public bool TryCompletePendingActiveItemUse(IReadOnlyList<int> selectedIndices)
    {
        return BattleState != null && BattleState.TryCompletePendingActiveItemUse(selectedIndices);
    }

    public bool TryCompletePendingActiveItemMoneyUse(int amount)
    {
        return BattleState != null && BattleState.TryCompletePendingActiveItemMoneyUse(amount);
    }

    public bool CancelPendingActiveItemUse()
    {
        return BattleState != null && BattleState.CancelPendingActiveItemUse();
    }

    public bool CanUseActiveItem(string itemId)
    {
        return !IsWaitingForVisuals
            && BattleState != null
            && (InputGate == null || InputGate.CanUseActiveItem(BattleState, itemId))
            && BattleState.CanUseActiveItem(itemId);
    }

    public bool CanUseActiveItemAtSlot(int slotIndex)
    {
        if (IsWaitingForVisuals
            || BattleState == null
            || !BattleState.RunState.TryGetActiveItemAt(slotIndex, out ActiveItemRuntimeState item))
        {
            return false;
        }

        return (InputGate == null || InputGate.CanUseActiveItem(BattleState, item.ItemId))
            && BattleState.CanUseActiveItemAtSlot(slotIndex);
    }

    public RoundResolution EndPlayerPhase()
    {
        if (BattleState == null || IsWaitingForVisuals)
            return default;

        BattleState activeBattle = BattleState;
        RoundResolution resolution = activeBattle.EndPlayerPhase();
        MarkWaitingForCompletedRound(activeBattle);

        return resolution;
    }

    public bool TryDequeueVisualCommand(out VisualCommand command)
    {
        if (CommandQueue == null)
        {
            command = default;
            return false;
        }

        return CommandQueue.TryDequeue(out command);
    }

    public void NotifyVisualsResolved(VisualCommandType commandType)
    {
        BattleState?.EventBus.Publish(new VisualsResolvedEvent(commandType));
    }

    public void CompletePendingVisualTransition()
    {
        if (!IsWaitingForVisuals || BattleState == null)
            return;

        BattleState activeBattle = BattleState;
        IsWaitingForVisuals = false;

        if (activeBattle.Phase == BattlePhase.PostRound)
        {
            activeBattle.CleanupRound();
            if (activeBattle.Phase == BattlePhase.BattleEnd)
                EventBus.Publish(new BattleEndedEvent(activeBattle.GetBattleResult()));
        }
        else if (activeBattle.Phase == BattlePhase.BattleEnd)
        {
            EventBus.Publish(new BattleEndedEvent(activeBattle.GetBattleResult()));
        }
        else if (activeBattle.Phase == BattlePhase.Cleanup)
        {
            activeBattle.CleanupRound();
        }
    }

    public void CleanupBattle()
    {
        if (BattleState != null)
        {
            BattleState.Dispose();
        }

        IsWaitingForVisuals = false;
        BattleState = null;
        InputGate = null;
    }

    private void OnDestroy()
    {
        CleanupBattle();
    }

    private void MarkWaitingForCompletedRound(BattleState activeBattle)
    {
        if (BattleState == activeBattle && (activeBattle.Phase == BattlePhase.Cleanup || activeBattle.Phase == BattlePhase.BattleEnd))
            IsWaitingForVisuals = true;
    }
}
