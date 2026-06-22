# Shop System

## Purpose

The shop is a run-level purchasing phase inserted between completed, non-final battle rounds. The battle remains alive while the shop is open: its canvas stays visible behind `ShopUI`, its completed round remains in `BattlePhase.Cleanup`, and round cleanup is deferred until the player presses Continue.

The relevant responsibilities are split as follows:

- `BattleUiPresenter` opens the shop from `ToShopButton`.
- `RunManager` owns the transition between `RunPhase.Battle` and `RunPhase.Shop`.
- `ShopUi` generates and binds offers, then delegates purchases to `RunState`.
- `RunState` validates and commits permanent ownership and money changes.
- `GameplayCanvasCoordinator` keeps BattleUI active beneath ShopUI and enables the shop's raycast-blocking canvas.
- `ShopSlotView` binds a slot's button, sprite, label, and price, and deactivates its root after purchase.

## Round-to-shop flow

1. `BattleState.ResolveRound` resolves the wager and leaves a surviving battle in `BattlePhase.Cleanup`.
2. The visual command runner finishes the round animation. `BattleController.IsWaitingForVisuals` remains true so the completed round is not cleaned automatically.
3. `BattleUiPresenter` displays the round result and `ToShopButton`.
4. Clicking the button calls `RunManager.OpenShop()`.
5. `OpenShop` only succeeds when a battle exists, is not over, and is in `BattlePhase.Cleanup`. It changes the run to `RunPhase.Shop` and publishes `ShopOpenedEvent`.
6. `GameplayCanvasCoordinator` activates ShopUI while deliberately keeping BattleUI active. ShopUI receives a higher sorting order and a raycast-blocking `CanvasGroup`.
7. Enabling `ShopUi` generates and displays that round's offers.

The shop is skipped at battle end because an ended battle is in `BattlePhase.BattleEnd`, not `Cleanup`; the normal battle-result flow remains responsible for leaving the encounter.

## Offer generation

`ShopUi.GenerateOffers` runs once per activation. Its random seed comes from `ShopOfferGenerator.CreateSeed`, which combines:

- the run seed;
- the encounter index;
- the completed round number.

This makes a shop reproducible for the same run and round. `ShopOfferGenerator.TakeRandom` removes selected entries from a temporary pool, so a source entry cannot occupy more than one slot in that category.

The default slot counts are:

- 3 active items;
- 3 relics;
- 5 card upgrades.

Owned relics are filtered before selection. Upgrade candidates are all unique combinations of a catalog upgrade definition and a `Rank`; the selected rank is displayed in the slot. If a catalog has fewer eligible entries than available slots, the extra slot roots are deactivated.

## Catalog and asset lookup

`ShopUi` resolves its catalog in this order:

1. the `catalog` field assigned in the Inspector;
2. `Resources/Shop/ShopCatalog`;
3. `ShopCatalog.CreateRuntimeDefault()`.

The runtime fallback contains the three clear-field active items, the burst-22 relic, and four rank-to-suit upgrades. For authored content, create definition assets through `Create > Demon Blackjack > Shop`, add them to a `ShopCatalog`, and assign that catalog to `ShopUi` or store it at `Assets/Resources/Shop/ShopCatalog.asset`.

Definitions contain shop-facing data only:

- stable `Id`;
- display name;
- description;
- non-negative price.

The definition ID is the contract joining shop data, saved ownership, sprite lookup, and gameplay logic. IDs must remain stable after shipping.

Icons are configured in `GameplayAssetRegistry` under the active-item, relic, and card-upgrade ID/sprite lists. `ShopUi` queries `GetActiveItemSprite`, `GetRelicSprite`, or `GetCardUpgradeSprite`; missing entries fall back to the registry's configured shop fallback and then its default card front.

## Purchase flow

Clicking a slot calls the matching atomic method on `RunState`:

- `TryPurchaseActiveItem(id, price)`;
- `TryPurchaseRelic(id, price)`;
- `TryPurchaseRankUpgrade(rank, id, price)`.

The UI never deducts money or changes ownership directly. `RunState` validates the request, verifies affordability, applies the money delta, updates ownership, and returns `ShopPurchaseResult`. Only a successful result deactivates the slot root. Failed purchases leave the offer available.

Afterward, `ShopUi` publishes either `ShopPurchaseSucceededEvent` or `ShopPurchaseFailedEvent`. Systems such as sound, animation, analytics, or tooltips can subscribe to these global events without being coupled to the purchase code.

### Ownership rules

- Active-item IDs are stored as counted entries. Duplicate purchases are allowed; successful use removes one copy.
- Relics are unique. An owned relic cannot be purchased and is excluded from later offer pools.
- A rank owns at most one `OwnedRankUpgrade` containing its rank, upgrade ID, and paid price.
- Replacing a rank upgrade refunds `floor(previous paid price / 2)` toward the new purchase. The player only needs to afford the resulting net cost. Any refund beyond the new price is not paid out as profit.

All three ownership types are serialized by `RunState.ToData` and restored by `RunState.FromData`. Legacy rank-modifier saves load with a paid price of zero.

## Leaving the shop

`ContinueButton` calls `RunManager.ContinueFromShop()`:

1. `BattleController.CompletePendingVisualTransition()` cleans the completed round and moves the battle from `Cleanup` to `PreRound`.
2. The run returns to `RunPhase.Battle`.
3. `ShopClosedEvent` is published.
4. BattleUI refreshes, creates the next round, and opens the existing wager flow.

The sold state needs no separate persistence because there is no refill or reroll and the shop exists only for this transition. Slot roots are disabled rather than destroyed so layout objects remain intact.

## Scene binding requirements

`ShopUi` currently auto-binds the existing MainScene hierarchy by object name. When changing the hierarchy, update `BindSceneSlots` or assign a more explicit serialized binding layer. Required controls include `ContinueButton`, three item roots/buttons/prices, three relic roots/buttons/prices, and five upgrade roots/buttons/prices.

The second item root is currently named `item2Slot ` with a trailing space; the binding intentionally matches that scene name.

