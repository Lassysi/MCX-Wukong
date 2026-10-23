# MCX Wukong v0.4.0

Public test release. Download **MCX-Wukong-v0.4.0-Players.zip**; Source.zip is for developers.

### Changes

- Improved ground detection when MC control resumes after travel or respawn.
- Added recovery for getting stuck after arena respawns; shortened the wait for the get-up animation.
- Fixed one melee swing dealing repeated damage through a large boss's multiple hitboxes. Separate arrows still count independently.
- Fixed boss hitboxes intercepting right-clicks and blocking nearby building.
- Added soft / stone / hard block toughness, with matching stone and obsidian prices.
- Reworked enemy stagger buildup and cooldowns to reduce repeated flinching.
- Restored vanilla Infinity: ordinary arrows are preserved; tipped and spectral arrows are consumed.

Game requirements are unchanged: Black Myth Steam build 21393610, Minecraft Java 1.21.4, Fabric 0.19.5 and Java 21. Download CSharpLoader 0.1.0 separately. Local single-player only.

Builds and isolated tests passed. The new fixes still need dual-game playtesting; all-chapter compatibility is not yet verified.

[Install](https://github.com/Lassysi/MCX-Wukong/blob/v0.4.0/docs/INSTALL.md) · [Upgrade](https://github.com/Lassysi/MCX-Wukong/blob/v0.4.0/docs/UPGRADE.md) · [Known issues](https://github.com/Lassysi/MCX-Wukong/blob/v0.4.0/docs/KNOWN-ISSUES.md)

---

### 简体中文

公开测试版。玩家下载 **MCX-Wukong-v0.4.0-Players.zip**，Source.zip 为源码。

- 改进传送、复活后的地面检测，减少 MC 接管等待。
- 为挑战／复战复活后无法移动加入自动恢复，并缩短起身动画等待。
- 修复一次近战挥击通过大型 Boss 多个判定框重复结算伤害；独立箭矢仍分别计数。
- 修复 Boss 判定挡住右键和附近放方块。
- 方块新增软质、石质、坚硬耐久档位，石材与黑曜石售价同步调整。
- 调整敌人硬直积累与冷却，减少连续挨打僵住。
- “无限”附魔恢复原版规则：普通箭不消耗，药箭和光灵箭正常消耗。

运行要求不变：黑神话 Steam build 21393610、Minecraft Java 1.21.4、Fabric 0.19.5、Java 21。CSharpLoader 0.1.0 需单独下载，仅支持本机单人。

构建和独立测试已通过，本次修复仍待双游戏实测，尚未完成全章节验证。

[安装](https://github.com/Lassysi/MCX-Wukong/blob/v0.4.0/docs/INSTALL.zh-CN.md) · [升级](https://github.com/Lassysi/MCX-Wukong/blob/v0.4.0/docs/UPGRADE.zh-CN.md) · [已知问题](https://github.com/Lassysi/MCX-Wukong/blob/v0.4.0/docs/KNOWN-ISSUES.zh-CN.md)
