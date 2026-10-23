# Installation

English | [简体中文](INSTALL.zh-CN.md) · [Upgrading?](UPGRADE.md)

Requires Windows x64, Black Myth Steam build **21393610**, Minecraft Java **1.21.4**, Fabric **0.19.5** and **64-bit Java 21**. Both games and your own Minecraft account are required.

## 1. Download and import

1. Download [v0.4.0 Players.zip](https://github.com/Lassysi/MCX-Wukong/releases/download/v0.4.0/MCX-Wukong-v0.4.0-Players.zip) and extract it. Keep setup, `.mrpack`, `payload` and the guides together. Source.zip is not an installer.
2. Install [CSharpLoader 0.1.0](https://www.nexusmods.com/blackmythwukong/mods/664) following its author's instructions. Check for `version.dll` and `CSharpLoader/CSharpModBase.dll` under Black Myth's `b1/Binaries/Win64`.
3. Import `MCX-Wukong-v0.4.0.mrpack` into [Prism Launcher](https://prismlauncher.org/). It downloads the eight pinned mods and configures Fabric; do not update them separately.
4. Sign in with your own account, select Java 21, reach the Minecraft main menu once, then exit. In Prism, open the instance's Minecraft folder; it should contain `mods`.

## 2. Run setup

Close both games and run `MCX-Wukong-Setup.exe` from the extracted folder.

| Field | Select |
|---|---|
| Black Myth folder | `BlackMythWukong`, containing `b1` |
| MC instance game folder | The imported instance's game folder containing `mods` |
| Vanilla client JAR | Minecraft 1.21.4 client, found automatically when possible |
| Skin, optional | Classic four-pixel-arm 64×64 PNG; blank uses the project skin |

In Prism, the client is usually `libraries/com/mojang/minecraft/1.21.4/minecraft-1.21.4-client.jar` under its data folder. Select the client, not `java.exe`, a mod, a server JAR or the Fabric installer. Setup retries discovery if field 3 is blank.

New saves skip the entire prologue, including combat, by default; uncheck that option to retain it. Click **Install**. Setup checks versions, backs up replaced files/saves, generates textures from your local client and creates the MCX world if absent. Players do not need Python, a JDK or the .NET SDK. Existing marked MCX worlds are kept.

## 3. Launch and exit

1. Enter the **MCX-Wukong** world. Keep Minecraft open in a window without minimizing it.
2. Start Black Myth, load the matching save and wait for the bridge. Play in the Black Myth window.
3. After connecting, `/function mcx:starter` grants starter gear once. H handles shrines/chests; F6 opens the map; F8 switches native control for story skills.

Use one MCX world per native save. Exit Black Myth first, then Minecraft. To uninstall, close both, select the original folders in setup and click **Uninstall**; worlds, progress and CSharpLoader remain. Backups are under `b1/Binaries/Win64/MCX-Wukong-backups`.

[Usage](USAGE.md) · [Skins](SKINS.md) · [FAQ](FAQ.md) · [Known issues](KNOWN-ISSUES.md)
