# Balance and selected prices — v0.4.0

English | [简体中文](BALANCE.zh-CN.md)

These are bridge base values; cooldowns, criticals, enchantments and target modifiers apply afterwards. Vanilla tooltips are unchanged. Old configs migrate once to revision 124 and the new wear/hunger fields; subsequent edits are retained. Back up `config/dqj-balance.json` before editing.

| Material | Sword | Axe | Pickaxe | Sword price | Axe price | Pickaxe price |
|---|---:|---:|---:|---:|---:|---:|
| Wood | 6 | 8 | 3 | 1 | 1 | 1 |
| Stone | 7.5 | 10 | 4 | 2 | 2 | 1 |
| Gold | 9 | 12 | 5 | 3 | 4 | 2 |
| Iron | 11 | 14 | 6 | 5 | 6 | 3 |
| Diamond | 13 | 16 | 7 | 9 | 11 | 6 |
| Netherite | 15 | 19 | 8 | 14 | 17 | 10 |

Full-set armor reductions: leather 25%, gold 35%, chainmail 45%, iron 55%, diamond 70%, netherite 80%. Mixed sets use helmet/boots 15% each, chest 40%, leggings 30% of that set's reduction. Protection, resistance and absorption are additional systems.

Shotguns: Beretta/Remington 9 damage per pellet, Spas 10. Launcher direct hits: grenade 9, rocket 16. Launcher explosions ×3.5, cap 260; other grenade explosions ×3, cap 160. TNT ×2, cap 120; ordinary creepers retain their 30 cap. Applicable boss gun/explosion damage multiplier is 0.8. These are not displayed Black Myth HP values.

| Purchase | Emeralds |
|---|---:|
| 32 ordinary golden apples | 2 |
| 1 enchanted golden apple | 32 |
| Boat / minecart / fishing rod | 12 / 15 / 6 |
| Elytra / totem / shield | 16 / 20 / 3 |
| 4 ender pearls | 3 |
| Beretta / Remington / Spas | 8 / 12 / 14 |
| M79 or GL06 / Milkor | 16 / 24 |
| M72 / RPG-7 | 20 / 26 |
| Barrett / M134 | 20 / 22 |
| 32 shotgun shells / 12 explosive rounds / 8 rockets | 2 / 5 / 5 |
| Sharpness V / Power V / Protection IV / Infinity | 16 / 14 / 14 / 10 |

Ordinary golden apples and enchanted golden apples are different products. Repairs charge by tier and missing durability, preserve item data/enchantments and exclude guns. See the generated [base shop catalog](SHOP.md) for the actual per-offer listing.


## Block toughness

| Examples | Ordinary enemy hits | Boss hits | Shop quantity / emeralds |
|---|---:|---:|---|
| Dirt, oak planks, glass | 1 | 1 | 64 / 1 |
| Stone, cobblestone, stone bricks | 2 | 1 | Cobblestone or stone bricks: 64 / 3 |
| Obsidian, netherite blocks, anvils | 3 | 2 | Obsidian: 16 / 5 |

Tiers follow blast resistance. Oak stairs/slabs cost 1 emerald per 32. Boss hits count as 2 toughness damage; ordinary hits count as 1. Damage counters reset after 30 seconds without a hit. Bedrock, barriers, unbreakable blocks and bridge ground proxies are excluded. Shop tooltips state the hit counts.

## Enemy staggers

Light hits add 1 buildup and heavy hits add 2. Initial thresholds are 3 for ordinary enemies and 6 for bosses/chiefs. After a stagger, the target has 2 seconds of immunity and the next threshold increases by ×1.5; 6 seconds without a stagger resets it. Heavy reactions have a separate 4-second cooldown. Several damage events within 0.25 seconds count as one attack. Damage values are unchanged.
