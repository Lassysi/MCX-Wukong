# MCX Wukong

[English](README.md) | 简体中文

用 Minecraft 的移动、装备和建造玩法游玩《黑神话：悟空》。保留悟空的地图、敌人和剧情，两款游戏需在同一台电脑上同时运行。

**[下载 v0.4.0 玩家包](https://github.com/Lassysi/MCX-Wukong/releases/download/v0.4.0/MCX-Wukong-v0.4.0-Players.zip)** · [本次更新](https://github.com/Lassysi/MCX-Wukong/releases/tag/v0.4.0) · [安装](docs/INSTALL.zh-CN.md) · [旧版升级](docs/UPGRADE.zh-CN.md)

## 玩法

- 用剑、弓、枪械、TNT 和药水战斗，搭方块建造掩体和平台。
- 在土地庙购买、维修和附魔装备；获得灵蕴时额外赚取绿宝石。
- 用鞘翅飞行、末影珍珠传送，或用船和矿车困住普通怪物。
- 自选一张皮肤，两端共用；剧情需要悟空技能时，按 F8 切回原生操作。

## 运行要求

Windows 64 位、**黑神话 Steam build 21393610**、**Minecraft Java 1.21.4**、**Fabric 0.19.5**、**Java 21**。[CSharpLoader 0.1.0](https://www.nexusmods.com/blackmythwukong/mods/664) 需单独下载，仅支持本机单人。

## 开始游玩

1. 解压 **Players.zip**，按作者说明安装 CSharpLoader。
2. 用 [Prism Launcher](https://prismlauncher.org/) 导入随包 `.mrpack`，登录账号，启动到 MC 主菜单一次后退出。
3. 关闭两款游戏，运行 `MCX-Wukong-Setup.exe`，选择游戏目录及原版 1.21.4 客户端 JAR。皮肤可选填。
4. 进入 **MCX-Wukong** 世界，保持 MC 运行且不最小化，再启动黑神话，在黑神话窗口操作。

联动后输入 `/function mcx:starter` 可领取一次初始装备。每份黑神话存档使用对应的专用 MCX 世界，结束时先退出黑神话，再退出 MC。旧版用户请按[升级说明](docs/UPGRADE.zh-CN.md)操作。

## 按键

| 操作 | 按键 |
|---|---|
| 移动、战斗、方块、背包 | MC 常规操作；E 打开背包 |
| 采集、开箱、土地庙 | H |
| 黑神话地图／菜单 | F6／Esc |
| 切换 MC／悟空原生控制 | F8 |
| 尝试跳过支持的 CG | 长按 E |

**公开测试版：**部分剧情衔接、高空飞行和捕捉场景仍待实测，详见[已知问题](docs/KNOWN-ISSUES.zh-CN.md)。

[使用说明](docs/USAGE.zh-CN.md) · [数值与商店](docs/BALANCE.zh-CN.md) · [皮肤](docs/SKINS.zh-CN.md) · [常见问题](docs/FAQ.zh-CN.md) · [性能](docs/PERFORMANCE.zh-CN.md) · [更新日志](CHANGELOG.zh-CN.md) · [编译](docs/BUILD.zh-CN.md)

原创代码采用 MIT 许可，[第三方组件](THIRD-PARTY-NOTICES.zh-CN.md)按各自许可使用。非官方项目，与 Mojang、Microsoft、游戏科学无隶属关系。
