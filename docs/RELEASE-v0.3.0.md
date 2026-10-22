# MCX Wukong v0.3.0 — Control, combat and traversal

English | 简体中文

Download **MCX-Wukong-v0.3.0-Players.zip** to play. **Source.zip** is for developers. Back up both saves and follow the included upgrade guide; do not overlay the old installation.

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

---

## 简体中文

游玩请下载 **MCX-Wukong-v0.3.0-Players.zip**；**Source.zip** 为源码。更新前备份两边存档并阅读升级说明，不要直接覆盖旧安装。

### 新增与调整

- **F8 临时原生控制。** 恢复悟空的原生镜头、HUD、移动与技能，用于安身法等剧情要求；再次按 F8，从悟空当前位置申请接回 MC。CG、菜单、加载和传送期间会等待安全时机。原生接管期间 Steve 暂停，不受 MC 伤害、不消耗饥饿；悟空仍按原生规则受伤。
- **土地庙商店重整。** 六档剑、斧、镐与六档防具，27 种可用枪械和九类配套弹药。弹药紧邻适配枪械，标明用途并调整购买数量；清理多余工作台、材料、重复方块和低级附魔书。最终基础清单为 170 项，另按背包内容增加维修项。
- **维修改为主动交易。** 土地庙休息不再自动扣绿宝石修装备。把损坏装备放入主背包，在商店顶部用“绿宝石＋该装备”换回满耐久原物品，保留附魔；最多显示 12 条维修项。枪械不参与维修。
- **装备与战斗平衡。** 同类近战装备按材质递增，防具按整套及混搭计算减伤；提高枪械、TNT 和发射器爆炸伤害。后续调整将霰弹单弹丸伤害提高至 9–10，榴弹直击为 9、火箭直击为 16；发射器爆炸倍率 ×3.5、上限 260，再应用对应目标修正。普通 MC 生物保留原伤害规则。
- **恢复耐久与饥饿消耗。** 普通装备恢复原版耐久损耗、饥饿恢复原版消耗，近战命中和举盾格挡额外消耗。盾牌停在最后一点耐久、不破碎；耗尽状态仍可格挡，但承受 50% 穿透伤害。普通武器、工具、防具可以损坏，枪械保留自己的弹药机制。
- **船与矿车捕捉。** 空载具可捕捉靠近的普通怪物，直至玩家打破载具或怪物死亡。范围约为命中框外水平四格、垂直两格；联动期间载具只接受玩家造成的伤害。能否捕捉改按原生血条类型判断，不再按血量；BossBar／EliteBar 对应目标不能捕捉。具体实测边界见下文。

### Bug 修复

- 为亢金龙等长身、巨型敌人提供随骨骼移动的多部位宽松命中辅助，最多 24 个相互覆盖的 10 米判定框；修复跨 MC 区段查询漏掉大型命中框。多部位共用血量、受击冷却和状态，爆炸不会按部位数重复扣血。
- 修复放大后的隐形命中代理在客户端持续推挤 Steve、导致角色无输入后退；保留普通碰撞和攻击击退。
- 改善怪物投技期间及结束后的 MC 控制连续性，保留原生剧情／CG 的控制边界。
- 剧情传送到同地图远处或不同高度时创建额外坐标区域，旧区域锚点与建筑保留；接近区域边缘时在有地面条件下交接，高空飞行不再强行切回黑神话。
- 黑神话接管期间冻结后台 Steve，避免后台跌落或耗尽；MC 未显示时可恢复／复活。等待原生地面时让悟空自然落地，不再悬停。
- 恢复用于换场景的原生滑坡／样条移动，滑行结束并站稳后重新接回 MC。
- 仅在 MC 血量接管有效时过滤“无攻击者且大额”的原生地图死亡区扣血。这是针对性规则，不是对所有地图危险无敌；MC 摔落、虚空和岩浆伤害仍然保留。
- 联动时末影珍珠落地不再自扣血；未联动时保留原版行为。
- 修复附魔金苹果回血被原生血量快照覆盖：立即满血、16 点吸收生命、120 秒异常状态保护，保留抗性Ⅰ；重复食用刷新吸收生命而不无限叠加，牛奶、到期及死亡正常解除保护。
- 修复无限弓消耗箭：包括药箭在内的箭种可保留，但背包仍须至少有一支箭；未附魔无限的弓正常耗箭。
- 修复悬空方块把侧面邻接误认为底部支撑。冒险模式限制未改；正常建造请使用生存模式。


### 发布打包与安装器

- 主机、JAR 和整合包公开版本统一为 0.3.0。新实例默认 60 帧并关闭教程提示；本次准备不覆盖玩家现有选项。
- 手动粘贴实例目录而第三栏留空时，安装器再次尝试寻找原版客户端；失败会提示选择客户端 JAR，不再把空路径当当前目录。
- 卸载仅在所有联动关键设置未变化时接受 ReShade 正常重写的界面／默认选项，并把重写文件保存在安装备份中；关键设置被改仍停止。

### 兼容性与验证范围

Windows 64 位；《黑神话：悟空》Steam build **21393610**；Minecraft Java **1.21.4**；Fabric **0.19.5**；运行时 Java **21**。CSharpLoader **0.1.0** 必须从作者原站单独下载，本包不转发。两款游戏需在同一台电脑同时运行，仅支持本机单人。

这是基于最终 Preview125 的**公开测试版**。开发阶段的主机测试、加载器重写检查和独立 MC GameTest 覆盖上述修复；公开 v0.3.0 本轮重跑结果见 `validation.json`。这些不等于全章节实机验收。第三章滑坡、高空飞行、和尚捕捉／EliteBar 对应关系、低血珍珠落点、F8 剧情施法、残盾及全部 Boss 仍需更广泛实测。本轮未把新打包的公开版本安装到玩家正在使用的游戏。
