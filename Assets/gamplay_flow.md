# Gameplay Flow

This document describes the gameplay flow implemented in the current codebase. It focuses on the core pure C# layer under `Assets/Core/Gameplay` and the thin MonoBehaviour owners under `Assets/Runtime`.

## 1. Global Event Bus Setup

Responsible classes:

- `EventBus`
- `ScopedEventBus`
- `IEventBus`

`EventBus` is the global static event facade. It internally owns one `ScopedEventBus` instance and exposes static `Subscribe`, `Unsubscribe`, and `Publish` methods.

`ScopedEventBus` is the reusable event bus implementation. `BattleState` creates its own scoped bus per battle, so battle-only events can be discarded when the battle ends.

## 2. Persistence Manager Initialization

Responsible class:

- `PersistenceManager`

`PersistenceManager` is a `DontDestroyOnLoad` singleton. At the moment it only guarantees that one persistent instance exists. Save/load data is not implemented yet.

## 3. Run Manager Initialization

Responsible class:

- `RunManager`

`RunManager.Awake` finds a `BattleController` in the scene if one was not assigned in the inspector.

`RunManager.OnEnable` subscribes to the global `BattleEndedEvent`.

`RunManager.Start` optionally starts a debug run by calling `StartRun(debugSeed)`.

## 4. Run Creation

Responsible classes:

- `RunManager`
- `RunState`
- `Deck`

`RunManager.StartRun` creates a new `RunState`.

`RunState` stores persistent run data:

- run seed
- current run phase
- player HP and max HP
- gold
- encounter index
- difficulty level
- devil progression
- run deck
- relic ids
- global modifier ids
- battle history

During construction, `RunState` creates a standard 52-card deck through `Deck.CreateStandardDeck`.

After creating the state, `RunManager` moves the run phase from `Init` to `Map` and publishes `RunPhaseChangedEvent` through the global `EventBus`.

## 5. Battle Start Request

Responsible classes:

- `RunManager`
- `BattleConfig`
- `BattleController`
- `EventBus`

`RunManager.StartBattle` starts a battle from the current run.

If no `BattleConfig` is provided, `RunManager` creates a default one with:

- encounter id based on the current encounter index
- default opponent HP
- default `BasicDevilStrategy`
- default hand size, target score, burst threshold, and wager

`RunManager` then:

1. Sets the run phase to `Battle`.
2. Publishes a global `BattleStartedEvent`.
3. Calls `BattleController.InitializeBattle`.

## 6. Battle State Creation

Responsible classes:

- `BattleController`
- `BattleState`
- `RunState`
- `BattleConfig`
- `Deck`
- `ScopedEventBus`
- `CommandQueue`

`BattleController.InitializeBattle` clears any existing battle reference, creates a new `BattleState`, subscribes to that battle state's `BattleEndedEvent`, and calls `BattleState.Initialize`.

`BattleState` stores per-battle data:

- reference to `RunState`
- active `BattleConfig`
- per-battle `ScopedEventBus`
- per-battle `CommandQueue`
- player combat HP
- opponent combat HP
- battle phase
- round number
- reshuffle count
- deterministic battle seed
- current round
- active modifiers
- combat history

The battle seed is created from `RunState.CreateBattleSeed`.

The per-battle deck is created from the run deck using that battle seed.

## 7. Battle Initialization

Responsible classes:

- `BattleState`
- `ScopedEventBus`
- `CommandQueue`

`BattleState.Initialize` moves the battle phase to `Init`, publishes a battle-scoped `BattleStartedEvent`, enqueues a `BattleStarted` visual command, then moves the battle phase to `PreRound`.

If `BattleController.startFirstRoundOnInitialize` is enabled, `BattleController` immediately calls `StartNextRound`.

## 8. Round Start

Responsible classes:

- `BattleController`
- `BattleState`
- `RoundState`
- `Deck`
- `ScopedEventBus`
- `CommandQueue`

`BattleController.StartNextRound` calls `BattleState.StartRound`.

`BattleState.StartRound`:

1. Stops if the battle is already over.
2. Sets the battle phase to `PreRound`.
3. Increments `RoundNumber`.
4. Creates a new `RoundState`.
5. Publishes `RoundStartedEvent`.
6. Enqueues a `RoundStarted` visual command.
7. Draws the player's starting hand.
8. Draws shared visible cards.
9. Sets the battle phase to `PlayerPhase`.

## 9. Player Hand Draw

Responsible classes:

- `BattleState`
- `Deck`
- `RoundState`
- `ScopedEventBus`
- `CommandQueue`

`BattleState.DrawPlayerHand` draws cards from the per-battle `Deck`.

For each card drawn:

1. `BattleState.TryDrawCard` checks whether the deck needs to reshuffle.
2. If needed, discard cards are moved back into the draw pile and `DeckShuffledEvent` is published.
3. The card is added to `RoundState.PlayerHand`.
4. `CardDrawnEvent` is published for the player.

After drawing, `HandRefilledEvent` is published and a `CardsDrawn` visual command is enqueued.

## 10. Player Phase

Responsible classes:

- `BattleController`
- `BattleState`
- `RoundState`
- `ScopedEventBus`
- `CommandQueue`

UI or other input code should call `BattleController.TryPlayCard(handIndex)`.

`BattleController` forwards the request to `BattleState.TryPlayCard`.

`BattleState.TryPlayCard` only succeeds during `PlayerPhase`. It asks `RoundState` to move the selected card from `PlayerHand` to `PlayerPlayedCards`.

When a card is played:

1. `CardPlayedEvent` is published.
2. A `CardsPlayed` visual command is enqueued.

## 11. Ending Player Phase

Responsible classes:

- `BattleController`
- `BattleState`
- `RoundState`

UI or other input code should call `BattleController.EndPlayerPhase`.

`BattleController` forwards to `BattleState.EndPlayerPhase`.

`BattleState.EndPlayerPhase` only runs from `PlayerPhase`. It publishes `RoundEndedEvent` and moves the battle phase to `PostRound`.

## 12. Devil Strategy

Responsible classes:

- `BattleState`
- `IDevilStrategy`
- `BasicDevilStrategy`
- `RoundState`

After the player phase ends, `BattleState` asks the configured `IDevilStrategy` to choose opponent cards.

The current default implementation is `BasicDevilStrategy`, which draws up to two cards from the battle deck by calling `BattleState.TryDrawForOpponent`.

The chosen cards are stored in `RoundState.OpponentVisibleCards`.

`CardDrawnEvent` is published for each opponent card.

## 13. Score Resolution

Responsible classes:

- `BattleState`
- `ScoreResolver`
- `RoundState`

`BattleState.EndPlayerPhase` calls `ScoreResolver.Resolve` for both combatants.

`ScoreResolver` calculates:

1. blackjack score
2. poker hand rank
3. poker multiplier
4. additive modifiers
5. multiplicative modifiers
6. override modifiers
7. final score
8. burst state
9. blackjack state

The results are stored in `RoundState` through `RoundState.SetScores`.

`BattleState` then publishes score events:

- `ScoreCalculatedEvent`
- `BurstAttemptedEvent`
- `BurstOccurredEvent`
- `BlackjackAchievedEvent`

A `ScoresResolved` visual command is also enqueued.

## 14. Health Resolution

Responsible classes:

- `BattleState`
- `HealthResolver`
- `RoundState`

`BattleState.EndPlayerPhase` calls `HealthResolver.ResolveRound`.

`HealthResolver` determines the round winner using these rules:

1. both burst means no winner
2. one burst means the other combatant wins
3. blackjack beats non-blackjack
4. equal final score means no winner
5. higher final score wins

If the player wins, `BattleState.DamageOpponent` is called.

If the opponent wins, `BattleState.DamagePlayer` is called.

Damage methods update HP, publish `DamageTakenEvent`, and enqueue `HealthChanged` visual commands.

Player damage can also publish `DeathPreventedEvent` with `WasPrevented` set to `false` when player HP reaches zero. Actual death-prevention modifier logic is not implemented yet.

## 15. Round Result Storage

Responsible classes:

- `BattleState`
- `RoundResolution`

`HealthResolver.ResolveRound` returns a `RoundResolution`.

`BattleState` stores that result in its combat history and enqueues a `RoundEnded` visual command.

If either combatant has zero HP, the battle ends immediately.

If both combatants are still alive, the battle phase moves to `Cleanup`.

## 16. Round Cleanup

Responsible classes:

- `BattleController`
- `BattleState`
- `RoundState`
- `Deck`

After `BattleState.EndPlayerPhase`, `BattleController.EndPlayerPhase` automatically calls `BattleState.CleanupRound` if the battle phase is `Cleanup`.

`BattleState.CleanupRound`:

1. Takes all cleanup cards from `RoundState`.
2. Moves those cards to the battle deck discard pile.
3. Clears `CurrentRound`.
4. Moves the battle phase back to `PreRound`.

The next round can then be started with `BattleController.StartNextRound`.

## 17. Battle End

Responsible classes:

- `BattleState`
- `BattleResult`
- `ScopedEventBus`
- `CommandQueue`
- `BattleController`
- `EventBus`

When battle HP reaches an end condition, `BattleState.EndBattle` runs.

`BattleState.EndBattle`:

1. Sets the battle phase to `BattleEnd`.
2. Creates a `BattleResult`.
3. Publishes battle-scoped `BattleEndedEvent`.
4. Enqueues a `BattleEnded` visual command.
5. Clears the battle-scoped event bus.

`BattleController` receives the battle-scoped `BattleEndedEvent` and republishes it through the global `EventBus`.

## 18. Run State Update After Battle

Responsible classes:

- `RunManager`
- `RunState`
- `BattleController`

`RunManager` receives the global `BattleEndedEvent`.

It then:

1. Calls `RunState.ApplyBattleResult`.
2. Stores the battle result in run history.
3. Updates persistent player HP.
4. Increments encounter progress.
5. Increments devil progression if the player won.
6. Asks `BattleController` to clean up its battle reference.
7. Moves the run phase to `Rewards`.

## 19. Visual Command Consumption

Responsible classes:

- `BattleController`
- `CommandQueue`
- `VisualCommand`

The gameplay core does not directly control UI, animation, VFX, or audio.

Instead, `BattleState` enqueues `VisualCommand` entries into its `CommandQueue`.

Presentation-layer code can call `BattleController.TryDequeueVisualCommand` to consume commands.

When visuals are complete, presentation-layer code can call `BattleController.NotifyVisualsResolved`, which publishes `VisualsResolvedEvent` on the battle event bus.

No state transition currently waits on `VisualsResolvedEvent`; the event exists as the integration point for later animation-gated flow.

## 20. Current Extension Points

The current implementation is intentionally loose-coupled. The main extension points are:

- `IDevilStrategy` for opponent behavior
- `Modifier` and modifier events for scoring and future effects
- battle-scoped `ScopedEventBus` for cards, relics, devils, and temporary effects
- global `EventBus` for run-level systems
- `CommandQueue` for presentation synchronization
- `ScoreResolver` for blackjack, poker, and score modifier logic
- `HealthResolver` for damage, healing, and win/loss rules

