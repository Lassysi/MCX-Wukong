# Changelog

## v0.3.0 — 2026-10-22

### Gameplay

- F8 switches between MC and native Wukong controls for story skills. Steve waits safely while Wukong is controlled.
- Reorganized 170 shrine offers, grouped guns with their ammunition, and adjusted quantities/prices. Repairs are explicit trades that retain enchantments; resting no longer repairs automatically.
- Added persistent boat/minecart capture for ordinary enemies. Eligibility follows the native health-bar type; bosses/chiefs are excluded.
- Rebalanced melee, armor, guns and explosives. Restored vanilla-rate wear/hunger costs; worn shields block with 50% damage passing through.

### Fixes

- Large-boss hit detection, cross-section misses, duplicate multipart damage and unwanted proxy pushing.
- MC control lost after grabs, distant cutscene teleports, long travel, high flight or native slide transitions; hidden Steve falling during native control.
- Large native kill-zone damage while MC controls health. Normal enemy damage and MC fall/void/lava damage remain.
- Enchanted golden apple healing/absorption, Infinity consuming arrows, pearl self-damage while bridged, and unsupported blocks staying suspended.
- Blank client-JAR detection and uninstall failures after normal ReShade settings rewrites. Fresh instances use 60 FPS and hide tutorial hints.

Public test release. [Known issues](docs/KNOWN-ISSUES.md) · [Usage](docs/USAGE.md) · [Balance](docs/BALANCE.md) · [Build/test record](validation.json)

## v0.2.0

- Added Will-to-emerald rewards and chapter-dependent shrine trading.
- Added paid repairs on shrine rest, enchanted-book application and native-enemy adaptations for selected enchantments, potions and terrain effects.
- Adjusted hunger, regeneration, durability, gun and explosion rules.
- Added native destructible-scene interactions; improved ender-pearl landing and falling-block stacking.
- Fixed some ranged-weapon/offhand-shield input and display conflicts; improved story recovery, respawn and chapter-transition handling.
- Updated bilingual installation, usage, upgrade and skin guides with formal public version names.
- Added verified backup of existing MCX worlds before installation, preserving progress, inventory and builds.

Public test release; complete fresh-machine and all-chapter in-game acceptance is still pending.

## v0.1.0-beta.1

- First public test release, integrating MC controls, combat and building with Black Myth progression, with a player package, setup tool and project skin.
