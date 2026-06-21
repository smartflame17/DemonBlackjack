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

Current operations clear the player's field, the opponent's field, or both fields. Failed effects do not consume an item.

Use this hook for explicit, player-triggered consumables. Add public, invariant-preserving methods to `BattleState` when an item needs a new operation; do not let UI code mutate `RoundState` collections directly.

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
- Reaction to cards, items, scores, health, phases, or rounds: implement a scoped event handler and register/dispose it through `BattleEffectRuntime`.

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

