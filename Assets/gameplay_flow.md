# Gameplay Flow

This document describes the intended MVP gameplay flow for the pure C# layer under `Assets/Core/Gameplay` and the thin MonoBehaviour owners under `Assets/Runtime`.

## 1. Run Resource Model

`RunState` stores one persistent survival resource: money. Health and money are no longer separate systems.

The player starts a run with 100 money. The player loses the run when money reaches 0. Legacy HP-facing API may remain temporarily as a presentation compatibility shim, but gameplay logic should read and write money.

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

A battle consists of rounds. Starting a round creates the round state, restores any carried-over hand cards, and refills empty hands before any wager decision is made. After this preparation step, the battle remains in `PreRound` until the presentation layer commits a wager decision.

The default wager is 10% of the player's current money, rounded down to whole money with a minimum of 1 while the player has money. Wagers are expressed as ratios of that default wager rather than fixed 10, 20, 30, ..., 100 values. A proposed wager can exceed one side's current money, but the final stake is capped by each combatant's money. If either side has less money than the final proposed wager, that side is forced all in for its remaining money.

Initiative alternates by round. The player acts first on odd rounds, starting with round 1; the opponent acts first on even rounds.

When the player proposes a wager, the devil strategy chooses whether to accept or decline it. Higher affinity biases random decisions toward outcomes beneficial to the player. If the devil declines the player's proposal, the round automatically uses the default wager.

When the devil has initiative, the devil evaluates its current hand by summing card ranks and mapping that sum to `DevilHandLevel`. The devil's proposed wager is `DevilHandLevel` multiplied by the default wager. If the player declines the devil's proposal, the round automatically uses the default wager.

After a wager is decided, both combatants stake their final committed amounts. Player money decreases through `RunState`; opponent money decreases through `BattleState`. The pot is the sum of both committed amounts, and only then does the round advance to the player or opponent phase.

## 4. Hand Refill

Unplayed hand cards carry across round cleanup.

`BattleState.RefillPlayerHandIfEmpty` draws from the player battle deck using `BattleState.PlayerDrawValue` whenever the current player hand is empty.

`BattleState.RefillOpponentHandIfEmpty` draws from the devil battle deck using `IDevilStrategy.DrawValue` whenever the opponent hand is empty.

At round start, refill order follows initiative. The combatant who goes first draws first, so the first opening draws for the round come from that combatant's own deck. These refill checks happen before wager decisions so both combatants can evaluate their hands.

Refill checks must happen any time a hand becomes empty during battle. In particular, `CardPlayedEvent` and `CardDiscardedEvent` flows should trigger refill behavior.

## 5. Turn Flow

A single round consists of alternating turns until the round resolves.

On a player turn, gameplay input can choose:

- `stand`: do nothing and pass the turn
- `hit`: immediately draw the first card from the draw pile, play it, and end the turn
- `play`: play one or more cards from the player's hand, then end the turn

`stand` and `hit` end the turn immediately. Playing a hand card does not automatically end the turn, because a combatant can play as many hand cards as desired during a play turn. The presentation layer should keep its play/confirm control disabled until at least one card has been played in that turn, but UI implementation is intentionally deferred.

On an opponent turn, `IDevilStrategy` chooses whether to stand, hit, or play cards from hand. The default strategy uses shared blackjack-oriented logic to avoid bursting when possible.

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

Burst also makes the bursting combatant lose half of their current money. This penalty is applied to money, not HP. If a combatant has only 1 money, the penalty removes that last money so battle end can be reached.

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

After round cleanup, the battle moves back to `PreRound`.

Longer-term design calls for dialogue events or shops between rounds where the player can refine their deck or buy upgrades. These features are undecided. For now, gameplay only exposes continuing to the next round and skips between-round logic.

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
