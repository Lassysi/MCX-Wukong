# Known issues and validation — v0.3.0

English | [简体中文](KNOWN-ISSUES.zh-CN.md)

Public test release, not an all-chapter completion claim. The final source is based on Preview125; earlier successful tests do not establish that every final interaction has been accepted in native gameplay. Build/test results are recorded in `validation.json`.

- Pending broader live acceptance: chapter-3 slide/scene transition, extended high flight and grounded region handover, captures of the high-HP monk, mapping EliteBar to bottom-screen boss/chief bars, low-health pearl landings, F8 Ring-of-Fire story progression, worn-shield damage and hunger balance.
- Large-enemy hit assistance is generic but has not been checked against every boss. Oversized boxes deliberately favor hit accessibility; shared-root checks prevent per-part damage multiplication in isolated tests.
- Kill-volume damage filtering is a heuristic for large attacker-less losses while MC owns the health gate. It is not immunity to every environmental source. Normal enemy damage and MC fall/void/lava damage remain.
- Some native cutscenes, transformations, scripted moves and story objects keep native control or appearance. Hold-E skipping only works for supported scenes. F8 does not bypass quest requirements or cooldowns.
- The public v0.3.0 fresh installation and full dual-game playthrough have not been completed in this packaging session. Do not describe isolated GameTests or loader rewriting as full gameplay acceptance.
- CSharpLoader must be downloaded separately from its author. There is no promised Melty one-click release. Both licensed games and the exact supported versions are required.
- Gun Core/Modern Guns include two pre-existing debug give functions referencing absent loot tables. Production shop paths and ammunition are tested separately; third-party JARs are unchanged. M202 remains excluded.
- Skin replacement requires a classic 64×64 PNG and reinstall. Dynamic skin hot-swapping, slim-arm skins and universal native cutscene coverage are not promised.
- Minimum hardware and performance across other PCs have not been measured. See [performance](PERFORMANCE.md); streaming recommendations are starting points, not benchmarks.
- Use a dedicated MCX world paired with the correct native save. Do not update pinned third-party mods independently or copy an old world over later progress.
