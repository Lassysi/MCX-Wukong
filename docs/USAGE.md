# Usage

English | [简体中文](USAGE.zh-CN.md)

Finish [installation](INSTALL.md), enter the dedicated MCX-Wukong world, leave MC running unminimized, then play in the Black Myth window. Both games are required.

## Controls and story

| Action | Control |
|---|---|
| Movement, camera, attacks, blocks, items | Normal Minecraft controls |
| Inventory / swap hands / chat | E / F / T |
| Gather, chest, shrine | H |
| Native map / menu | F6 / Esc |
| Switch between MC and native Wukong control | F8 |
| Try skipping a native real-time cutscene | Hold E; only supported scenes |
| Starter gear after the bridge connects | `/function mcx:starter`, once |

For a story that needs Wukong's own skill, press F8, use the native skill normally, then press F8 to return at the current location. Spell costs/cooldowns and quest checks stay native. Requests during CG/menu/loading/travel wait for gameplay. Respawn before using F8 on an MC death screen. Steve is frozen and protected from MC damage during native control; Wukong can still take native damage. A lost hold heartbeat releases Steve after three seconds.

Native story transformations and required slides retain native control. Do not force a return or switch MC worlds during loading. Flight stays under MC control; this is not unrestricted passage through story gates. `/gamemode survival` restores ordinary building after Adventure mode; `/gamemode creative` is optional.

## Shop, ammunition and repairs

Use H to activate a native shrine. Near it, right-click with an empty main hand or emeralds to open the Shrine Trader. All base equipment tiers and the supported gun families are offered from chapter one. Guns sit beside their matching ammunition, whose tooltip identifies compatible weapons. A magazine and an individual round are different items; buy the matching kind. M202 remains excluded.

The base catalog has 170 offers, plus up to 12 repair trades for damaged carried items. Worn armor and the offhand item must be moved to the main inventory to appear. Pay emeralds and the exact damaged item to receive a repaired copy with its enchantments; resting never buys repairs automatically. Guns do not use vanilla durability or these repair trades.

New Will earnings generate emeralds without spending native Will. Chapter defaults: 60 / 100 / 160 / 250 / 350 / 500 Will per emerald. Existing Will is not immediately converted. See [balance and prices](BALANCE.md).

Near the shrine, right-click with equipment in the main hand and an enchanted book in the offhand to apply compatible enchantments. The book is consumed; XP/anvils are not required. Not every Minecraft enchantment is adapted.

## Combat, shields and food

Bridge damage follows the equipment curves in [balance](BALANCE.md), plus cooldowns, criticals, enchantments and target modifiers. Vanilla item tooltips do not display all bridge values. Ordinary MC mobs keep their normal damage rules.

Weapons/tools/armor wear at the vanilla rate. A shield cannot break: when worn down, it still blocks with 50% damage passing through until repaired. Hunger uses vanilla exhaustion plus 0.3 per landed proxy melee hit and 1.0 per shield block before scaling. Starvation damage is disabled; this does not disable combat/fall damage. The goal of consuming a meaningful amount during a boss fight is not a measured guarantee for every build.

At food level 18+, after 60 seconds without damage, regeneration restores half a heart every four seconds. Enchanted golden apples restore full health and 16 absorption HP with a 120 s status guard, Resistance I and fire resistance. Guard refresh does not stack unlimited gold hearts; expiry, milk and death remove it. Milk/honey's temporary native-status suppression is not universal cleansing of every native effect.

Infinity preserves ammunition, including tipped arrows, only when at least one arrow is present. It does not make an empty inventory shoot. Ranged main-hand use takes priority over the offhand shield.

## Capturing enemies

Place an empty boat or minecart near an eligible ordinary enemy: approximately four blocks horizontally and two vertically from its proxy hitbox. It stays held until you break the vehicle or it dies. While bridged only player damage breaks the vehicle; release has a ten-second recapture cooldown. BossBar/EliteBar enemies cannot be captured or reeled in. Native bar classification replaces the former HP threshold, so a high-HP ordinary enemy can qualify. EliteBar mapping and individual enemies still need live confirmation.

## Building and travel

Ordinary blocks require real support underneath; side contact alone does not hold an overhang. Unsupported blocks fall. Enemies can destroy ordinary cover after roughly three hits. Pickaxes and explosions affect native objects that were already destructible, retaining native drops/events; story gates are not forcibly removed.

Pearl landing self-damage is disabled only while bridged, but landing/collision checks still apply. Long grounded movement can move into a fresh coordinate region without relocating earlier builds. High flight keeps MC control until a grounded handover is possible. Required native slide volumes temporarily release MC and resume it after the slide and supported landing. These paths still need broader real-game validation.

## Saves and skins

Use one matching MCX world per Black Myth save. Save and exit Black Myth first, then MC. Never hot-replace DLLs/JARs or restore an old world over new progress. See [upgrade](UPGRADE.md), [skins](SKINS.md) and [known issues](KNOWN-ISSUES.md).
