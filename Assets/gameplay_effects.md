# Gameplay Effects

## Architecture

Gameplay-effect data and behavior are intentionally separate.

- `ActiveItemDefinition`, `RelicDefinition`, and `CardUpgradeDefinition` are ScriptableObjects containing presentation and economy data.
- `RunState` stores permanent ownership using stable string IDs rather than references to Unity assets.
- `ActiveItemResolver`, `RelicRuleResolver`, and `CardModifierResolver` map those IDs to runtime behavior.
- `BattleState` calls the appropriate resolver at a defined point in the gameplay loop.
- `BattleEffectRuntime` owns scoped event subscriptions for effects that react to battle events and disposes them with the battle.

This separation keeps `RunState`, save data, and core gameplay independent of Unity asset lifetimes. A ScriptableObject does not execute an effect merely because it exists in a catalog: its ID must also be supported by the appropriate runtime extension point.

## Existing hooks

### Active items

The use path is:

1. UI or another controller calls `BattleController.TryUseActiveItem(itemId)`.
2. `BattleState.TryUseActiveItem` verifies that a round exists, the battle is active, and `RunState` owns at least one copy.
3. `ActiveItemResolver.TryApply` maps the ID to a `BattleState` operation.
4. The item is removed from `RunState` only if the effect succeeds.
5. The battle's scoped bus publishes `ItemUsedEvent`.

Current operations reject the last player Hit card, draw three cards into the player's hand, or double the current round wager. Failed effects do not consume an item.

Use this hook for explicit, player-triggered consumables. Add public, invariant-preserving methods to `BattleState` when an item needs a new operation; do not let UI code mutate `RoundState` collections directly.

Current active item IDs:

- `hit_to_played`: move the latest player Hit card from the played field to the player discard pile so it no longer contributes to score or poker hands.
- `draw_three`: draw up to three cards from the player deck into hand. This can exceed the normal hand size.
- `double_wager`: commit an additional stake equal to the current wager from both sides, doubling the current round pot. It only succeeds when both sides can pay the extra stake.

### Relics

Relics are persistent run rules. The current relic hook is evaluated while a round is created:

```text
BattleState.StartRound
  -> RelicRuleResolver.ResolveTargetScore
  -> RelicRuleResolver.ResolveBurstThreshold
  -> new RoundState(resolved rules)
```

The burst-22 relic changes the threshold for rounds created after ownership is acquired. A relic purchased in the shop therefore applies to the next round without rebuilding the battle.

Use rule resolvers for passive effects that change values at a stable lifecycle boundary, such as round creation, wager calculation, draw count, or score resolution. Use scoped event handlers instead when an effect must react each time an event occurs.

### Card upgrades

Rank upgrades are resolved only when a player card is committed to play:

- normal play passes `BattleState.ApplyPlayerCardUpgrade` into `RoundState.TryPlayCard`;
- hit draws a raw card, then calls `ApplyPlayerCardUpgrade` before adding it to the played pile.

`ApplyPlayerCardUpgrade` checks `RunState.TryGetRankUpgrade(card.Rank)` and passes the card and owned upgrade ID to `CardModifierResolver.Apply`. The transformed card is placed into the played pile, then `CardPlayedEvent` publishes that transformed value.

Consequences:

- every suit of the purchased rank is affected;
- cards in the hand, deck, or discard pile remain raw until played;
- cards already played are not retroactively changed when an upgrade is purchased;
- opponent cards do not use player-owned upgrades;
- downstream scoring and event subscribers see the transformed card.

The current upgrades change the played card's suit, which affects poker-hand evaluation while preserving its rank and blackjack value.

## Scoped events and lifecycle

Every `BattleState` owns a `ScopedEventBus`. Core battle events such as `CardPlayedEvent`, `ItemUsedEvent`, score events, and round events are published on this bus.

`BattleEffectRuntime` is constructed with the battle and is the central lifecycle owner for future reactive handlers. It currently establishes the `CardPlayedEvent` and `ItemUsedEvent` extension points. Add event-driven dispatch or concrete `IBattleEffectHandler` implementations there as content requires them.

Any future handler must:

1. subscribe to `battle.EventBus`, not the global static bus, for battle-only reactions;
2. retain the exact callback delegate used for subscription;
3. unsubscribe in `Dispose`;
4. mutate gameplay through `BattleState`/`RoundState` methods that enforce invariants;
5. avoid presentation dependencies in core gameplay code.

`BattleState.Dispose` unregisters devil hooks, disposes `BattleEffectRuntime`, and clears the scoped bus. `BattleController.CleanupBattle` calls it even when a battle is abandoned before its normal end.

Use the global `EventBus` for run/UI lifecycle notifications such as purchases, ownership changes, `ShopOpenedEvent`, and `RunPhaseChangedEvent`. Do not use it for effects whose subscriptions must be isolated to one battle.

## Connecting a new ScriptableObject to behavior

Creating a definition asset is only the data half of an effect. Follow all applicable steps below.

### 1. Define a stable ID

Add a constant to the matching resolver, for example:

```csharp
public const string DoubleNextCard = "item_double_next_card";
```

Use the exact same value in the ScriptableObject's `Id`. IDs are serialized into save data, so never reuse an old ID for unrelated behavior and avoid renaming released IDs without a migration.

### 2. Create and catalog the asset

Use one of these Create menus:

- `Demon Blackjack/Shop/Active Item`;
- `Demon Blackjack/Shop/Relic`;
- `Demon Blackjack/Shop/Card Upgrade`.

Fill in the ID, display name, description, and price. Add the asset to the corresponding list in the active `ShopCatalog`.

### 3. Register its sprite

Add an entry with the same ID to the appropriate list in `GameplayAssetRegistry`:

- Active Item Sprites;
- Relic Sprites;
- Card Upgrade Sprites.

Duplicate IDs should be avoided; the last matching registry entry wins when the lookup dictionary is built.

### 4. Implement the runtime behavior

Choose the extension point based on effect timing:

- Explicit consumable action: add a case to `ActiveItemResolver.TryApply`.
- Passive value/rule change: add a query to `RelicRuleResolver` and call it from the relevant lifecycle boundary.
- Played-card transformation: add a case to `CardModifierResolver.Apply(Card, id)`.
- Reaction to cards, items, scores, money, phases, or rounds: implement a scoped event handler and register/dispose it through `BattleEffectRuntime`.

For a reactive relic, first check `battle.RunState.HasRelic(id)` in the handler. For a rank upgrade reaction, check the event owner and the card's `ModifierId` or query the owned upgrade for the card's rank. Always decide explicitly whether the effect applies to the player, opponent, or both.

### 5. Publish observable results

If the effect changes state in a way presentation must animate, publish a typed scoped event and enqueue a `VisualCommand` where ordering relative to other battle visuals matters. Avoid parsing arbitrary strings in UI code when a typed event can represent the result.

### 6. Test both ownership and activation

At minimum, cover:

- purchase and insufficient-funds behavior;
- save/load of the owned ID and any required metadata;
- the exact activation boundary;
- player/opponent targeting;
- success and no-op/failure behavior;
- item consumption only after success;
- handler unsubscription when the battle is disposed.

## Recommended evolution for complex content

The current resolvers use ID switches, which are simple and explicit for the initial content set. If the number of effects grows substantially, preserve the same ScriptableObject IDs and replace the switches with registries such as `Dictionary<string, Func<...>>` or handler factories. Keep behavior out of the data assets unless there is a deliberate decision to let Unity objects execute core gameplay logic; doing so would couple tests and save-compatible gameplay to asset loading.

Effects requiring parameters beyond an ID should add serialized configuration to a specialized definition type and convert it into a plain runtime descriptor when the battle is initialized. Do not store a ScriptableObject reference directly in `RunState` or persistence data.

## Implemented card upgrade IDs

Card upgrades are still assigned by rank in `RunState`, but their runtime behavior now falls into three categories:

- immediate played-card transformation in `CardModifierResolver`;
- blackjack score interpretation in `ScoreResolver`;
- event-driven reactions in `BattleEffectRuntime.OnCardPlayed`.

The event-driven upgrades use the battle's scoped `CardPlayedEvent`. They apply only to player-owned played cards because player rank upgrades are applied through `BattleState.ApplyPlayerCardUpgrade`.

Shop offer eligibility is controlled by `CardUpgradeDefinition.AssignmentType`. `NumberedCards` offers can target Ace through Ten, `FaceCards` can target any Jack/Queen/King, and the rank-specific face values `JackCards`, `QueenCards`, and `KingCards` restrict offers to exactly that rank.

### Suit imprint upgrades

These IDs are resolved by `CardModifierResolver.Apply` when a player card is committed to play:

- `rank_to_hearts`: the played card keeps its rank but becomes Hearts.
- `rank_to_diamonds`: the played card keeps its rank but becomes Diamonds.
- `rank_to_clubs`: the played card keeps its rank but becomes Clubs.
- `rank_to_spades`: the played card keeps its rank but becomes Spades.

These affect poker suit evaluation and visuals for the played card. They do not change blackjack value.

### Numbered-card upgrades

`negative_rank`

- The played card keeps its rank and suit.
- In blackjack score calculation, the card contributes the negative of its blackjack value.
- Numbered ranks contribute `-2` through `-10`; Ace contributes `-11`.
- Negative Aces are not counted as soft Aces for the usual Ace reduction rule.
- Poker evaluation still uses the card's real rank and suit.

`hit_lower`

- When the modified card is played, `BattleEffectRuntime` asks `BattleState` to play one random card from the player's current hand with a lower rank than the triggering card.
- The extra card is moved from hand to `RoundState.PlayerPlayedCards` using the same player play helper as other effect-driven plays.
- The extra card has its own rank upgrade applied before it is placed in the played pile.
- The extra play publishes another `CardPlayedEvent`, so chained card-upgrade effects can trigger.
- If no lower-rank card exists in hand, nothing happens.
- This effect does not complete the player turn by itself.

`draw_suit`

- When the modified card is played, `BattleEffectRuntime` asks `BattleState` to draw one random card with the same suit from the player's current draw pile into the player's hand.
- The search is limited to the current draw pile. It does not reshuffle the discard pile.
- The selected card is removed from the draw pile and added to `RoundState.PlayerHand`.
- The effect publishes `CardDrawnEvent`, `HandRefilledEvent`, and a draw visual command.
- If no matching-suit card exists in the draw pile, nothing happens.

### Face-card upgrades

All face-card upgrades use the "previous player played card", defined as `RoundState.PlayerPlayedCards[Count - 2]` after the triggering face card has entered the player played pile. If there is no previous card, the effect does nothing.

`move_jack`

- Its definition should use `CardUpgradeAssignmentType.JackCards`, so shop offers only attach it to Jacks.
- When the modified Jack is played, the previous player played card is removed from `RoundState.PlayerPlayedCards`.
- That previous card is added to `RoundState.OpponentVisibleCards`, so it contributes to the opponent's score for the current round.
- No opponent `CardPlayedEvent` is published for the moved card. This avoids treating the move as an opponent action and avoids opponent hand-refill side effects.
- `RoundState` tracks ownership for cards in the opponent visible pile. Cards moved by `move_jack` remain player-owned.
- During round cleanup or opponent field clearing, player-owned cards in the opponent visible pile are discarded to the player deck's discard pile, not the opponent deck.

`copy_queen`

- Its definition should use `CardUpgradeAssignmentType.QueenCards`, so shop offers only attach it to Queens.
- In blackjack score calculation, the modified Queen contributes `0`.
- Poker evaluation still uses the Queen's real rank and suit.
- When the modified Queen is played, the previous player played card determines the suit to copy.
- `BattleEffectRuntime` asks `BattleState` to play one random card from the player's current hand with that same suit.
- The selected hand card is moved to the player played pile, has its own rank upgrade applied, and publishes `CardPlayedEvent`, so chained effects can trigger.
- If no current hand card has the previous card's suit, nothing happens.

`duplicate_king`

- Its definition should use `CardUpgradeAssignmentType.KingCards`, so shop offers only attach it to Kings.
- When the modified King is played, a battle-only duplicate of the previous player played card is added to the player's hand.
- The duplicate preserves suit, rank, and modifier ID.
- The duplicate is not added to `RunState.Deck` and does not persist after battle cleanup.
- Adding the duplicate publishes `HandRefilledEvent` and a draw visual command so presentation can refresh the hand.
