# Gameplay Flow

This document describes the intended MVP gameplay flow for the pure C# layer under `Assets/Core/Gameplay` and the thin MonoBehaviour owners under `Assets/Runtime`.

## 1. Run Resource Model

`RunState` stores one persistent survival resource: money.

The player starts a run with 100 money. The player loses the run when money reaches 0. Gameplay logic and presentation code should read and write money.

`RunState` also stores persistent run data:

- run seed
- current run phase
- player money
- encounter index
- difficulty level
- devil progression
- per-devil affinity values
- run deck
- relic ids
- global modifier ids
- battle history

During construction, `RunState` creates a standard 52-card deck through `Deck.CreateStandardDeck`.

## 2. Battle Start

`RunManager.StartBattle` starts a battle from the current run. If no `BattleConfig` is provided, it creates a default config with:

- encounter id based on the current encounter index
- opponent starting money, currently 100
- default `BasicDevilStrategy`
- default hand size, target score, burst threshold, and wager bounds

`BattleController.InitializeBattle` creates a `BattleState`, subscribes to that battle state's `BattleEndedEvent`, and calls `BattleState.Initialize`.

`BattleState` owns per-battle state:

- reference to `RunState`
- active `BattleConfig`
- scoped battle event bus
- command queue
- opponent money
- player battle deck
- opponent battle deck
- battle phase
- round number
- player and opponent hand carryover
- deterministic battle seed
- current round
- active modifiers
- combat history

The player's money is stored on `RunState`; the opponent's money is stored on `BattleState`.

The player and devil do not share a battle deck. The player battle deck is created from `RunState.Deck`. The devil battle deck is created through `IDevilStrategy.CreateStartingDeck`. The default devil strategy returns a standard 52-card deck equivalent to the player default, but character-specific strategies can provide different starting decks later.

## 3. Round Start, Hand Refill, And Wager

A battle consists of rounds. `BattleState.StartRound` is the single round-start operation: it creates the round state, restores carried-over hand cards, refills empty hands, commits both stakes, publishes the round-start notifications, and advances to the player phase.

The wager is fixed per battle by `BattleConfig.BaseWager`. The presentation layer refreshes the value through `BattleState.GetDefaultWager` immediately before starting each round so gameplay modifiers can affect that value later without restoring a wager-selection step. Each combatant's committed stake is capped by their available money, so a combatant with less money than the configured wager goes all in.

Initiative alternates by round. The player acts first on odd rounds, starting with round 1; the opponent acts first on even rounds.

Player money decreases through `RunState`; opponent money decreases through `BattleState`. The pot is the sum of both committed amounts. There is no runtime wager proposal, acceptance, or decline step.

## 4. Hand Refill

Unplayed hand cards carry across round cleanup.

`BattleState.RefillPlayerHandIfEmpty` draws from the player battle deck using `BattleState.PlayerDrawValue` whenever the current player hand is empty.

`BattleState.RefillOpponentHandIfEmpty` draws from the devil battle deck using `IDevilStrategy.DrawValue` whenever the opponent hand is empty.

At round start, refill order follows initiative. The combatant who goes first draws first, so the first opening draws for the round come from that combatant's own deck.

Refill checks must happen any time a hand becomes empty during battle. In particular, `CardPlayedEvent` and `CardDiscardedEvent` flows should trigger refill behavior.

## 5. Turn Flow

A single round consists of alternating turns until the round resolves.

On a player turn, gameplay input can choose:

- `stand`: do nothing and pass the turn
- `hit`: immediately draw the first card from the draw pile, play it, and end the turn
- `play`: play one card from the player's hand, then end the turn

`stand`, `hit`, and playing one hand card end the turn. A combatant can play at most one hand card during a play turn, so the presentation layer should keep its play/confirm control disabled until exactly one card is selected.

On an opponent turn, `IDevilStrategy` chooses whether to stand, hit, or play one card from hand. The default strategy uses shared blackjack-oriented logic to avoid bursting when possible.

## 6. Score And Burst Checks

After each turn, `BattleState` resolves current scores with `ScoreResolver`.

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

If either combatant bursts, the round resolves immediately. A burst means immediate loss of the round. If both combatants burst in the same turn, the round is a draw.

Burst also makes the bursting combatant lose half of their current money. If a combatant has only 1 money, the penalty removes that last money so battle end can be reached.

## 7. Round Resolution

When a round resolves without an immediate burst result, winner rules are:

1. blackjack beats non-blackjack
2. equal final score means draw
3. higher final score wins

Wager resolution:

- player win: player receives the full pot
- opponent win: opponent receives the full pot
- draw: each combatant receives their own committed stake back

If player money reaches 0, the battle ends as a loss. If opponent money reaches 0 while the player still has money, the battle ends as a win.

`BattleState` stores a `RoundResolution` in combat history and enqueues a `RoundEnded` visual command.

## 8. Between Rounds

After round cleanup, the battle moves back to `PreRound`. The battle UI displays `RoundStartPanel` for its configured delay and then starts the next round automatically with the latest default wager.

## 9. Battle End And Run Update

`BattleState.EndBattle`:

1. Sets the battle phase to `BattleEnd`.
2. Creates a `BattleResult`.
3. Publishes battle-scoped `BattleEndedEvent`.
4. Enqueues a `BattleEnded` visual command.
5. Clears the battle-scoped event bus.

`BattleController` republishes the battle-scoped `BattleEndedEvent` through the global `EventBus`.

`RunManager` receives the global `BattleEndedEvent`, calls `RunState.ApplyBattleResult`, stores history, increments encounter progress, increments devil progression if the player won, cleans up the battle reference, and moves the run phase to `Rewards`.

## 10. Devil Strategy Contract

`IDevilStrategy` is responsible for:

- choosing to stand, hit, or play during an opponent turn
- choosing which hand cards to play
- choosing the wager when the devil has initiative
- choosing whether to accept or decline the player's wager
- creating the devil's starting deck
- registering and unregistering affinity hooks on the global event bus, battle event bus, or both
- exposing character-specific global modifiers
- sharing common blackjack-oriented logic that can play optimally across devil types

Affinity increases are event-driven, not necessarily battle-result-driven. A devil implementation can subscribe to events such as cards being played, suits being played, scores being reached, or any future gameplay signal. For now, affinity is read from `RunState` and biases random choices toward outcomes beneficial to the player.

## 11. Visual Command Consumption

The gameplay core does not directly control UI, animation, VFX, or audio.

`BattleState` enqueues `VisualCommand` entries into its `CommandQueue`. Presentation-layer code can call `BattleController.TryDequeueVisualCommand` to consume commands.

When visuals are complete, presentation-layer code can call `BattleController.NotifyVisualsResolved`, which publishes `VisualsResolvedEvent` on the battle event bus.

No UI changes are part of this gameplay-layer update.
