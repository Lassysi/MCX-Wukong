# 安装

[English](INSTALL.md) | 简体中文 · [旧版升级](UPGRADE.zh-CN.md)

需要 Windows 64 位、黑神话 Steam build **21393610**、Minecraft Java **1.21.4**、Fabric **0.19.5**、**64 位 Java 21**，以及两款游戏和自己的 MC 账号。

## 1. 下载与导入

1. 下载 [v0.3.0 Players.zip](https://github.com/Lassysi/MCX-Wukong/releases/download/v0.3.0/MCX-Wukong-v0.3.0-Players.zip) 并解压，保留安装器、`.mrpack`、`payload` 和说明文件。Source.zip 不是玩家安装包。
2. 按作者说明安装 [CSharpLoader 0.1.0](https://www.nexusmods.com/blackmythwukong/mods/664)，确认黑神话 `b1/Binaries/Win64` 下有 `version.dll` 和 `CSharpLoader/CSharpModBase.dll`。
3. 用 [Prism Launcher](https://prismlauncher.org/) 导入 `MCX-Wukong-v0.3.0.mrpack`，由启动器下载八个指定模组并配置 Fabric，不要自行更新版本。
4. 登录 MC 账号，选择 Java 21，启动到主菜单一次后退出。在 Prism 打开该实例的 Minecraft 文件夹，里面应有 `mods`。

## 2. 安装联动

关闭两款游戏，运行解压目录里的 `MCX-Wukong-Setup.exe`。

| 栏目 | 选择内容 |
|---|---|
| 黑神话目录 | 包含 `b1` 的 `BlackMythWukong` 文件夹 |
| MC 实例游戏目录 | 刚导入实例中包含 `mods` 的游戏文件夹 |
| 原版客户端 JAR | MC 1.21.4 客户端，能找到时自动填写 |
| 可选皮肤 | 64×64、经典四像素手臂 PNG；留空用项目皮肤 |

Prism 客户端通常在其数据目录的 `libraries/com/mojang/minecraft/1.21.4/minecraft-1.21.4-client.jar`。不要选 `java.exe`、模组、服务端 JAR 或 Fabric 安装器。第三栏留空时，安装器会再次尝试查找。

新存档默认跳过整个序章（含战斗），取消勾选可保留。点“安装”后，工具会检查版本、备份原文件及存档，从本机客户端生成材质，并在缺少时创建 MCX 世界；已有带标记的 MCX 世界保留。玩家无需 Python、JDK 或 .NET SDK。

## 3. 启动与退出

1. 进入 **MCX-Wukong** 世界，保持 MC 窗口运行且不最小化。
2. 启动黑神话，读取对应存档，等待联动；在黑神话窗口操作。
3. 接管后输入 `/function mcx:starter` 可领取一次装备；H 用于土地庙／开箱，F6 打开地图，F8 临时切回悟空施法。

每份黑神话存档使用对应 MCX 世界。先退出黑神话，再退出 MC。卸载时关闭两款游戏，在安装器中选原目录、点“卸载”；世界、进度和 CSharpLoader 保留。备份在 `b1/Binaries/Win64/MCX-Wukong-backups`。

[使用说明](USAGE.zh-CN.md) · [皮肤](SKINS.zh-CN.md) · [常见问题](FAQ.zh-CN.md) · [已知问题](KNOWN-ISSUES.zh-CN.md)
