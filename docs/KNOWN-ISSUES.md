# Known issues — v0.4.0

English | [简体中文](KNOWN-ISSUES.zh-CN.md)

This is a public test release; not every chapter, boss or fresh-install environment has been tested.

- v0.4.0 ground recovery, arena-respawn recovery, multipart hit counting, right-clicks near bosses, block toughness, staggers and Infinity passed isolated checks; behavior in both games still needs playtesting. The watchdog request for a fresh ground handshake has not been tested end to end in native gameplay.
- More playtesting is needed for chapter-3 slides, long/high flight, grounded region switches, monk capture, F8 story spells, low-health pearl landings and worn shields. Native EliteBar data is treated as a chief/boss bar; that mapping still needs confirmation.
- Some cutscenes, transformations and scripted moves retain native control or appearance. Hold-E skipping only works in supported scenes. F8 does not bypass quest requirements.
- Large-boss hit assistance may need adjustment for individual enemies. The native kill-zone filter does not prevent all environmental damage; ordinary attacks and MC fall/void/lava damage remain.
- Gun Core/Modern Guns have two pre-existing debug functions referencing missing loot tables; the shop uses tested paths. M202 is excluded. Skin changes need a classic 64×64 PNG and reinstall; slim-arm skins and live swapping are unsupported.
- Other game/mod versions and minimum hardware requirements are unverified. Use the pinned dependencies and a dedicated MCX world paired with the right native save.

Build and isolated test results are in [validation.json](../validation.json). They do not replace a full dual-game playthrough. [FAQ](FAQ.md) · [Performance](PERFORMANCE.md)
