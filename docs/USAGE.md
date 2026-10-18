# Usage

English | [简体中文](USAGE.zh-CN.md)

Complete [installation](INSTALL.md) first. Keep Minecraft inside the `MCX-Wukong` world without minimizing it, and play in the Black Myth window.

## Basic controls

WASD moves, the mouse controls the camera, left-click attacks or breaks blocks, right-click uses items or places blocks, E opens inventory, F swaps hands and T opens chat. H handles native gathering, chests and shrines; F6 opens the map; Esc opens the native menu. Hold E during a cutscene to try skipping it. Some scenes cannot be skipped.

After the bridge takes over, `/function mcx:starter` grants basic equipment once. Commands are enabled: `/gamemode creative` enters creative mode and `/gamemode survival` returns to survival. Wait during story transformations and chapter loading.

## Shrines, trading and emeralds

1. Activate or interact with a native shrine using H.
2. Near an activated shrine, right-click with an empty main hand or emeralds to open the shop.
3. Buy chapter-dependent guns, ammunition, food, blocks, equipment, potions and enchanted books with emeralds.

Newly gained Will also generates emeralds without deducting native Will. Existing Will is not converted immediately. The default rates for chapters one through six are 60 / 100 / 160 / 250 / 350 / 500 Will per emerald.

Guns require matching ammunition, bows require arrows and crossbows must be loaded. A ranged main-hand weapon takes priority over shield use. Some third-party gun types still have compatibility limits.

## Durability, repairs and enchanting

Ordinary equipment wears at about 20% of the vanilla rate by default. Resting at a shrine with H spends emeralds from the inventory on repairs. With insufficient emeralds, only affordable repairs are performed. Guns are excluded from these repairs and enchanting and retain their ammunition system.

| Equipment category | Durability restored per emerald |
|---|---|
| Wood, stone, leather | 400 |
| Iron, gold, chainmail | 200 |
| Diamond, trident, mace | 100 |
| Netherite, elytra | 80 |

To enchant, hold equipment in the main hand and an enchanted book in the offhand, then right-click near an activated shrine. A successful application consumes the book without XP or an anvil. Incompatible enchantments are skipped; equal-level combinations respect maximum levels.

Integrated effects include Sharpness, Power, Fire Aspect, Flame, Thorns and Protection, plus Smite, Bane of Arthropods, Knockback and Punch adaptations for some native enemies. Looting increases emerald rewards from Will conversion, not native item drops. Efficiency reduces hits needed for native destructible scene objects. Not all Minecraft enchantments are adapted.

## Blocks and scene objects

Ordinary building blocks need support beneath them. Unsupported blocks fall and stack on landing. Native enemies destroy building blocks after about three hits.

Use a pickaxe or TNT/explosions on walls and pillars that Black Myth already supports destroying. Swords and empty hands can break light pots and jars. Native drops and events are retained. Story gates, quest mechanisms and indestructible objects are not forcibly removed.

Ender pearls check native scene collisions and landing positions; complex terrain still needs care. Elytra and firework flight are supported.

## Food and status effects

Hunger exhaustion defaults to 10% of vanilla, with starvation damage disabled. At food level 18 or higher, after 60 seconds without damage, regeneration restores half a heart every four seconds. Food and healing effects remain usable.

Enchanted golden apples immediately restore full health and grant their Minecraft status effects. Milk and honey retain their Minecraft clearing effects and temporarily suppress native ongoing status damage for 30 and 15 seconds respectively. This does not remove every native debuff.

Some Slowness, Weakness, Levitation, knockback and terrain-slowing effects apply to ordinary native enemies. Strong enemies have reduced or restricted effects. Native burn/frost visuals and statuses are not fully synchronized.

## Skins, story and saves

Import a custom skin using the fourth installer field rather than placing files inside either game. See [custom skins](SKINS.md) for the format and replacement steps.

New saves skip the whole prologue by default; this can be disabled during installation. Native story transformations such as the cicada remain controlled by the native game. Some cutscenes show the original character or cannot be skipped. Let the sequence finish rather than switching worlds or replacing files.

Use a corresponding MCX world for each Black Myth save. Save and exit Black Myth first, then save and exit Minecraft. Back up both games before updating; see [upgrade](UPGRADE.md).
