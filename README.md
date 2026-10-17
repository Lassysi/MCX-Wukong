# MCX Wukong

English | [简体中文](README.zh-CN.md)

Use Minecraft swords, bows, guns, TNT and blocks in **Black Myth: Wukong**. Both games run on the same PC.

**v0.1.0-beta.1 — first public beta.** First install and a full playthrough are not fully tested yet; some cutscenes may still be unskippable.

## Gameplay

- Minecraft movement, camera, inventory and equipment, with Black Myth's maps, enemies, bosses and story progression.
- Place and break blocks to build cover and platforms. Enemies can destroy blocks after about three hits.
- Ordinary building blocks need support and fall when support below them is removed.
- Attack enemies with swords, axes, bows, crossbows, guns, TNT, lava and harming potions. Use spawn eggs to summon allies.
- Shields block attacks from the front. Attacks from the sides and rear still hit. Minecraft food and healing effects restore health.
- Keep native shrine, travel, gathering and chest interactions. Ender pearls and elytra are supported.
- New saves skip the entire prologue by default. Hold E to try skipping later real-time cutscenes; some scenes still have compatibility issues.

Not every chapter, boss or item has been tested.

## Requirements

| Component | Required version |
|---|---|
| OS | 64-bit Windows; reference environment: Windows 11 |
| Black Myth: Wukong | Steam PC build **21393610** |
| Minecraft | **Java Edition 1.21.4**, with your own Java Edition account |
| Fabric Loader | **0.19.5**, configured by the included modpack |
| Java runtime | **64-bit Java 21**, selected in your Minecraft launcher |
| Black Myth loader | **CSharpLoader 0.1.0**, installed from its author's page |
| Other components | ReShade 6.8.0 and bridge components are included; the launcher downloads the Minecraft mods |
| Minecraft launcher | A launcher that imports `.mrpack`; these instructions use Prism Launcher |

Both games must be installed and running on one PC. Minimum hardware for running both has not been measured. Try 4 GB allocated to Minecraft as a starting point, leaving enough RAM for Black Myth and Windows.

Local single-player only. Bedrock Edition and multiplayer are unsupported. Other game versions have not been validated and are rejected by the installer.

## Install

1. Download the **Players ZIP** from this repository's [Releases](https://github.com/Lassysi/MCX-Wukong/releases/tag/v0.1.0-beta.1) and extract it into a new folder.
2. Install [CSharpLoader 0.1.0](https://www.nexusmods.com/blackmythwukong/mods/664) into Black Myth's `b1/Binaries/Win64` folder.
3. Import the included `.mrpack` into [Prism Launcher](https://prismlauncher.org/) and sign in with your own Minecraft Java account. Launch to Minecraft's main menu once to download the client, then exit normally.
4. Close both games. Run `MCX-Wukong-Setup.exe` and select your Black Myth folder and Minecraft instance game folder. Select the vanilla Minecraft 1.21.4 client JAR if it is not found automatically.
5. Click **Install / 安装**. Python, a JDK and the .NET SDK are not needed for players.

See the [first-time installation guide](docs/INSTALL.md) for folder examples, Java settings and startup steps. The installer has English and Chinese labels and messages. CSharpLoader is a separate download; block textures are generated from your local Minecraft client.

## Play

1. Start the imported Minecraft instance and enter the **MCX-Wukong** world created by the installer.
2. Keep Minecraft running in a window without minimizing it. Switch to Black Myth and start a new game or load your save.
3. Wait for the bridge to take over. Initial terrain setup can take tens of seconds.
4. Press T and enter `/function mcx:starter` to claim basic equipment once.

The new world is empty while waiting for the bridge. Use the dedicated world rather than a normal Minecraft survival world.

| Action | Key |
|---|---|
| Move, attack, place blocks, inventory | Normal Minecraft controls |
| Gather, open chests, interact with shrines | H |
| Black Myth map | F6 |
| Black Myth menu | Esc |
| Try skipping a real-time cutscene | Hold E |
| Minecraft chat / commands | T / slash |

The installer enables prologue skipping, including its combat, for new saves by default. You can uncheck it during installation. Native story transformations such as the cicada remain controlled by Black Myth.

## Uninstall

Close both games, run the setup tool with the same two folders, and click **Uninstall / 卸载**. Saves, Minecraft worlds and the third-party loader are kept.

## More

- [FAQ](docs/FAQ.md)
- [Known issues](docs/KNOWN-ISSUES.md)
- [Build instructions](docs/BUILD.md)
- [Third-party notices](THIRD-PARTY-NOTICES.md)

Original project code is MIT-licensed. This is an unofficial project, not affiliated with Mojang, Microsoft or Game Science.
