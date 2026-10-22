# MCX Wukong

English | [简体中文](README.zh-CN.md)

Brings Minecraft movement, camera, inventory, equipment and blocks into **Black Myth: Wukong**, retaining its maps, enemies, bosses and story progression. Both games must run on the same PC.

**[Download v0.3.0 Players](https://github.com/Lassysi/MCX-Wukong/releases/download/v0.3.0/MCX-Wukong-v0.3.0-Players.zip)** · [Release page](https://github.com/Lassysi/MCX-Wukong/releases/tag/v0.3.0) · [First installation](docs/INSTALL.md) · [Upgrade](docs/UPGRADE.md)

v0.3.0 is a public test release. Some real-time story scenes, chapter transitions and fresh installations require further in-game validation. See [known issues](docs/KNOWN-ISSUES.md).

## Features

- Minecraft movement, camera, inventory and equipment. Weapon and armor attributes affect damage and defense.
- Place and break blocks to build cover and platforms. Ordinary building blocks fall without support; enemies destroy blocks after about three hits.
- Fight Black Myth enemies with swords, axes, bows, crossbows, guns, TNT, lava and harming potions. Spawn eggs summon allies.
- Front-facing shield protection, food and healing effects, ender pearls and elytra.
- Native shrine, travel, gathering and chest interactions; Minecraft commands and creative mode.
- New Will-to-emerald rewards, shrine trading, explicit paid repair trades and enchanted-book application.
- Additional potion, terrain and enchantment effects on native enemies, plus interaction with destructible scene objects.
- Import one skin PNG for both games. New saves skip the prologue and its combat by default. Hold E to try skipping later real-time cutscenes.

See the [usage guide](docs/USAGE.md) for controls, rules and limits. Not every chapter, boss, item or combination has been validated.

## Requirements

| Component | Required version |
|---|---|
| OS | 64-bit Windows; reference environment: Windows 11 |
| Black Myth: Wukong | Steam PC build **21393610** |
| Minecraft | **Java Edition 1.21.4**, with your own Java Edition account |
| Fabric Loader | **0.19.5**, configured by the included modpack |
| Java runtime | **64-bit Java 21**, selected in your Minecraft launcher |
| Black Myth loader | **CSharpLoader 0.1.0**, downloaded separately from its author |
| Other components | ReShade 6.8.0 and bridge components are included; the launcher downloads the required MC mods |
| Minecraft launcher | Must import `.mrpack`; the installation guide uses Prism Launcher |

Local single-player only. Bedrock Edition, multiplayer and unmodded Minecraft are unsupported. Other game versions have not been validated and are rejected by the installer.

Minimum hardware for running both games has not been measured. Start with 4 GB allocated to Minecraft, a 1280×720 window and render distance 6, leaving resources for Black Myth and Windows.

## Install and play

1. Download and fully extract **Players.zip** above. GitHub's `Source code` archive is not an installer.
2. Install [CSharpLoader 0.1.0](https://www.nexusmods.com/blackmythwukong/mods/664) into Black Myth's `b1/Binaries/Win64`, following its author's instructions.
3. Import `MCX-Wukong-v0.3.0.mrpack` into [Prism Launcher](https://prismlauncher.org/) or another compatible launcher. Sign in with your own Minecraft account, start Minecraft to its main menu once, then exit normally.
4. Close both games. Run `MCX-Wukong-Setup.exe`; select the Black Myth root folder, the MC instance game folder and the vanilla Minecraft 1.21.4 client JAR.
5. Optionally select a **64×64 PNG with classic four-pixel arms** in the fourth field. Leave it blank for the project skin, then click **Install / 安装**. Players do not need Python, a JDK or the .NET SDK.
6. Start Minecraft and enter the **MCX-Wukong** world created by the installer. Keep it running in a window without minimizing, then start Black Myth. Play in the Black Myth window.
7. Wait for bridge initialization. Press T and enter `/function mcx:starter` to claim basic equipment once.

Initial setup requires internet access. The MCX bridge JAR is included; the launcher downloads the other eight pinned mods. Do not update them separately. See [first installation](docs/INSTALL.md) for folder examples, Java settings and detailed steps.

## Controls

| Action | Key or method |
|---|---|
| Move, attack, place blocks, inventory | Normal MC controls; E opens inventory |
| Gather, open chests, native shrine interaction | H |
| Black Myth map / menu | F6 / Esc |
| Temporary native Wukong control | F8; press again to request MC control |
| Try skipping a real-time cutscene | Hold E; some scenes cannot be skipped |
| MC chat / commands | T / slash |
| Shrine shop | Near an activated shrine, right-click with an empty main hand or emeralds |
| Shrine enchanting | Near an activated shrine, right-click with gear in the main hand and an enchanted book in the offhand |

Native story transformations such as the cicada remain controlled by Black Myth. Wait for the native sequence to finish; do not switch worlds during story or chapter loading.

## Saves and uninstall

Use the same Minecraft instance and dedicated MCX world with the matching Black Myth save. Do not use a normal survival world or share one MCX world across multiple Black Myth saves.

Save and exit Black Myth normally before saving and exiting Minecraft. To uninstall, close both games, run the same-version setup tool with the original two folders, then click **Uninstall / 卸载**. Saves, builds, backups and the third-party loader are kept.

## Documentation

- [Usage](docs/USAGE.md)
- [Upgrade](docs/UPGRADE.md)
- [Custom skins](docs/SKINS.md)
- [FAQ](docs/FAQ.md)
- [Known issues](docs/KNOWN-ISSUES.md)
- [Changelog](CHANGELOG.md)
- [Build instructions](docs/BUILD.md)
- [Third-party notices](THIRD-PARTY-NOTICES.md)

Original project code is MIT-licensed; third-party terms still apply. This is an unofficial project, not affiliated with Mojang, Microsoft or Game Science.

## v0.3.0 update

F8 native control, large-boss hit assistance and push fixes, cutscene travel/flight/slide recovery, explicit shrine repairs and a reorganized shop.

- [Full release notes](docs/RELEASE-v0.3.0.md)
- [Balance and prices](docs/BALANCE.md)
- [Performance and streaming](docs/PERFORMANCE.md)

Resting no longer purchases repairs; vanilla-rate durability and hunger are restored. The current usage guide and changelog take precedence over older descriptions.
