using System;
using UnityEngine;

public class EventLogger : MonoBehaviour
{
    [SerializeField] private bool logEvents = true;

    [SerializeField] private BattleController battleController;
    private BattleState battleState = null;
    // This component is for debugging purposes. It subscribes to all c# events and logs them to the editor console.


    void OnEnable()
    {
        // Global events
        EventBus.Subscribe<BattleStartedEvent>(eventData =>
        {
            if (!logEvents) return;
            Debug.Log($"<color=green>[Global]</color>Battle Started: EncounterId={eventData.EncounterId}, Seed={eventData.Seed}, PlayerMoney={eventData.PlayerMoney}, OpponentMoney={eventData.OpponentMoney}");
            battleState = battleController.BattleState;
            OnBattleStateInitialized();
        });
        EventBus.Subscribe<BattleEndedEvent>(eventData =>
        {
            if (!logEvents) return;
            Debug.Log($"<color=green>[Global]</color>Battle Ended: Result={eventData.Result}");
        });
        EventBus.Subscribe<MoneyChangedEvent>(eventData =>
        {
            if (!logEvents) return;
            Debug.Log($"<color=green>[Global]</color>Money Changed: Owner={eventData.Owner}, CurrentMoney={eventData.CurrentMoney}, Delta={eventData.Delta}");
        });
        EventBus.Subscribe<RunPhaseChangedEvent>(eventData =>
        {
            if (!logEvents) return;
            Debug.Log($"<color=green>[Global]</color> Run Phase Changed: NewPhase={eventData.Phase}");
        });

        
    }

    void OnBattleStateInitialized()
    {
        if (battleState.EventBus != null)
        {
            battleState.EventBus.Subscribe<BattlePhaseChangedEvent>(eventData =>
            {
                if (!logEvents) return;
                Debug.Log($"<color=red>[Battle]</color> Battle Phase Changed: NewPhase={eventData.Phase}");
            });

            battleState.EventBus.Subscribe<ItemUsedEvent>(eventData =>
            {
                if (!logEvents) return;
                Debug.Log($"<color=red>[Battle]</color> Item Used: ItemId={eventData.ItemId}");
            });

            // Round events
            battleState.EventBus.Subscribe<RoundStartedEvent>(eventData =>
            {
                if (!logEvents) return;
                Debug.Log($"<color=red>[Battle]</color> Round Started: RoundNumber={eventData.RoundNumber}");
            });

            battleState.EventBus.Subscribe<RoundEndedEvent>(eventData =>
            {
                if (!logEvents) return;
                Debug.Log($"<color=red>[Battle]</color> Round Ended: RoundNumber={eventData.RoundNumber}");
            });

            // Card events
            battleState.EventBus.Subscribe<CardPlayedEvent>(eventData =>
            {
                if (!logEvents) return;
                Debug.Log($"<color=red>[Battle]</color> Card Played: Card={eventData.Card}");
            });
            
            battleState.EventBus.Subscribe<HandRefilledEvent>(eventData =>
            {
                if (!logEvents) return;
                Debug.Log($"<color=red>[Battle]</color> Hand Refilled: HandCount={eventData.HandCount}");
            });

            battleState.EventBus.Subscribe<DeckShuffledEvent>(eventData =>
            {
                if (!logEvents) return;
                Debug.Log($"<color=red>[Battle]</color> Deck Shuffled: ReshuffleCount={eventData.ReshuffleCount}, DeckCount={eventData.DeckCount}");
            });

            // Score events
            battleState.EventBus.Subscribe<ScoreCalculatedEvent>(eventData =>
            {
                if (!logEvents) return;
                Debug.Log($"<color=red>[Battle]</color> Score Calculated: Combatant={eventData.Combatant}, Score={eventData.Score}");
            });

            battleState.EventBus.Subscribe<BurstOccurredEvent>(eventData =>
            {
                if (!logEvents) return;
                Debug.Log($"<color=red>[Battle]</color> Burst Occurred: Combatant={eventData.Combatant}, Score={eventData.Score}");
            });

            battleState.EventBus.Subscribe<BurstAttemptedEvent>(eventData =>
            {
                if (!logEvents) return;
                Debug.Log($"<color=red>[Battle]</color> Burst Attempted: Combatant={eventData.Combatant}, Score={eventData.Score}, Threshold={eventData.Threshold}, WillBurst={eventData.WillBurst}");
            });

            battleState.EventBus.Subscribe<BlackjackAchievedEvent>(eventData =>
            {
                if (!logEvents) return;
                Debug.Log($"<color=red>[Battle]</color> Blackjack Achieved: Combatant={eventData.Combatant}, Score={eventData.Score}");
            });

            battleState.EventBus.Subscribe<PokerResolvedEvent>(eventData =>
            {
                if (!logEvents) return;
                Debug.Log($"<color=red>[Battle]</color> Poker Resolved: Combatant={eventData.Combatant}, PokerResult={eventData.Poker}");
            });
        }
        else 
        {
            Debug.LogWarning("BattleState is null. EventLogger will not subscribe to battle events.");
        }
    }
}
