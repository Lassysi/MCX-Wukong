# Changelog

## v0.3.0 — 2026-10-22

### Added and changed

- **F8 native-control toggle.** Temporarily restore Wukong's camera, HUD, movement and native spells for story requirements such as Ring of Fire. Press F8 again to request MC control at the current native position. Requests wait through cutscenes, menus, loading and travel. Steve is held without MC damage/hunger drain while native control is active; this does not make Wukong invulnerable.
- **Shrine shop overhaul.** Six material tiers of swords, axes and pickaxes, six armor tiers, 27 supported guns and all nine matching ammunition families. Ammunition sits next to its guns with compatibility labels and useful purchase quantities. Removed redundant crafting items, duplicate blocks and low-level books. The final base catalog has 170 offers, plus context-dependent repair offers.
- **Repair as an explicit trade.** Resting no longer spends emeralds automatically. Put damaged equipment in the main inventory, open the Shrine Trader, and exchange the item plus emeralds for the same item at full durability, retaining enchantments. Up to 12 repair offers appear at the top. Guns are excluded.
- **Equipment and combat balance.** Material-based melee progression and armor reduction; stronger guns, TNT and launcher blasts. Later tuning increases shotgun pellets to 9–10, grenade-launcher direct hits to 9 and rocket direct hits to 16; launcher blasts scale ×3.5 up to 260 before applicable target modifiers. Ordinary Minecraft mobs keep their native damage rules.
- **Durability and hunger restored.** Vanilla-rate equipment wear and hunger exhaustion, extra exhaustion for landed melee hits and shield blocks. Shields stop one point before breaking; a worn shield permits 50% of blocked damage through. Normal weapons/tools/armor can break. Guns retain their own ammunition system.
- **Boat and minecart capture.** Ordinary enemies near an empty vehicle can be held until the vehicle is broken by the player or the enemy dies. Capture reach is approximately four blocks horizontally and two vertically from the hitbox. While bridged, only player damage can break these vehicles. Eligibility uses native health-bar data, not an HP threshold: BossBar/EliteBar enemies are excluded. See the validation limits below.

### Fixes

- Enlarged, bone-following multipart hit assistance for long or giant enemies, including Kang-Jin Loong: up to 24 overlapping 10 m boxes. Fixed Minecraft broad-phase section searches missing oversized hitboxes across section boundaries. Parts share health, cooldowns and effects so explosions do not multiply damage by part count.
- Stopped enlarged invisible proxies pushing Steve backwards on the client. Ordinary collisions and attack knockback remain.
- Improved MC control continuity during enemy grabs and recovery, with native story/cutscene boundaries retained.
- Added new coordinate regions for distant or vertically displaced cutscene destinations, preserving existing region anchors and builds. Grounded edge handovers prevent running out of a region; flight keeps MC control instead of handing over in mid-air.
- Prevented hidden Steve falling or starving while native control is active; added recovery/respawn when MC is not displayed. A takeover waiting for native ground lets the native body fall instead of holding it in the air.
- Restored native slide/spline traversal for scene transitions; MC resumes after the native slide ends and the character stands on supported ground.
- Filtered large attacker-less native kill-volume health losses only while the MC health gate is active. This is a targeted heuristic, not immunity to every native hazard. MC fall, void and lava damage remain.
- Removed ender-pearl landing self-damage while bridged; vanilla behavior remains outside the bridge.
- Fixed enchanted golden apple healing being overwritten by native health snapshots. It restores full health, grants 16 absorption HP and a 120 s status guard, retains Resistance I, and refreshes absorption without unlimited stacking. Milk, expiry and death remove the guard normally.
- Infinity bows preserve arrow types, including tipped arrows, but still require at least one arrow in inventory. Bows without Infinity still consume ammunition.
- Corrected unsupported blocks treating neighboring side faces as underside support. Adventure-mode restrictions are unchanged; use Survival for ordinary placement.


### Packaging and setup

- Public host/JAR/modpack version is consistently 0.3.0. Fresh modpack instances default to 60 FPS and hide tutorial hints; existing options are not overwritten by this preparation.
- Setup retries vanilla-client discovery when a pasted instance path leaves field 3 empty and shows a clear client-JAR error instead of opening the current directory.
- Uninstall accepts a normal ReShade UI/default rewrite only when every bridge-critical setting is unchanged, preserving the rewritten file in the installation backup. Modified bridge settings still stop uninstall.

### Compatibility and validation

Windows x64; Black Myth: Wukong Steam build **21393610**; Minecraft Java **1.21.4**; Fabric **0.19.5**; Java **21** runtime. CSharpLoader **0.1.0** is a separate download from its author and is not bundled. Both games run on the same PC; local single-player only.

This is a **public test release** based on final Preview125. Historical host suites, loader rewrite checks and isolated MC GameTests cover the fixes above; see `validation.json` for checks rerun on the public v0.3.0 build. They do not establish all-chapter gameplay acceptance. The final chapter-3 slide, high flight, monk capture/EliteBar mapping, low-health pearl landing, F8 story-spell sequence, worn-shield behavior and complete boss roster still need broader live testing. The native patch was installed in development; the newly packaged public version has not been installed over the player's running games.

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
