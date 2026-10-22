# Usage

English | [简体中文](USAGE.zh-CN.md)

Enter the dedicated MCX world, leave MC running unminimized, then play in Black Myth. Use normal Minecraft controls; E opens inventory, T opens chat, H handles gathering/chests/shrines, F6 opens the native map and Esc its menu. `/function mcx:starter` grants starter gear once.

## Native skills and story

Press **F8** to restore Wukong's controls, camera and HUD, use the required skill, then press F8 to return at the current position. Spell costs and quest checks stay native. Switch requests wait during cutscenes, menus, loading or travel; on an MC death screen, respawn first. Steve waits without MC damage or hunger drain; Wukong can still be hurt.

Native transformations and required slides keep native control. Let them finish. Hold E to try skipping supported real-time cutscenes.

## Shrine shop and equipment

Activate a shrine with H. Nearby, right-click with an empty main hand or emeralds to open the shop. Its 170 base offers include equipment tiers and 27 guns from chapter one. Matching ammunition is beside each gun and labeled; magazines and individual rounds are different items. M202 is excluded.

Repairs appear at the top for damaged items in the main inventory. Move worn armor/offhand items there, then pay emeralds plus the damaged item for a full repair retaining enchantments. Resting never buys repairs automatically; guns are excluded. For enchanting, right-click near a shrine with gear in the main hand and a compatible enchanted book in the offhand. It consumes the book, not XP.

New Will earnings also generate emeralds without deducting native Will. Costs, damage curves and ammunition quantities are in [balance](BALANCE.md) and the [shop catalog](SHOP.md). Vanilla tooltips do not show every bridge damage modifier.

## Combat and food

Weapons, tools and armor wear at the vanilla rate. Shields never break; when worn out they let 50% of blocked damage through until repaired. Hunger uses vanilla exhaustion plus costs for landed attacks and shield blocks. Starvation damage is disabled; combat, fall, lava and void damage remain.

At food level 18+, after 60 seconds without damage, regeneration restores half a heart every four seconds. Enchanted golden apples heal fully, grant 16 absorption HP and a 120 s status guard with Resistance I and fire resistance. Re-eating refreshes rather than endlessly stacking gold hearts. Milk, expiry and death remove the guard. Milk/honey do not clear every native debuff.

Infinity preserves arrows, including tipped arrows, but needs at least one arrow in inventory. A ranged main hand takes priority over the offhand shield. Ordinary MC mobs retain their normal damage rules.

## Capture, building and travel

An empty boat/minecart can capture an ordinary enemy roughly four blocks horizontally and two vertically from its hitbox. It stays until you break the vehicle or it dies; while bridged, only player damage breaks the vehicle. Release has a ten-second recapture cooldown. Enemies with native boss/chief bars cannot be captured or reeled in; see [known issues](KNOWN-ISSUES.md).

Ordinary blocks need support underneath: side contact alone does not hold them up. Enemies break ordinary cover after about three hits. Pickaxes and explosives affect native destructible objects, retaining drops/events; they do not remove story gates. Adventure mode restricts normal building—use `/gamemode survival` to return to Survival.

Pearl landings do not cause self-damage while bridged, but landing/collision checks remain. Flight stays under MC control; grounded travel can switch coordinate regions without moving earlier builds. Native slides resume MC control after a supported landing.

Use one MCX world per Black Myth save. Exit Black Myth before MC; follow [upgrade](UPGRADE.md) and [skin](SKINS.md) instructions before replacing files.
