# MCX Wukong

English | [简体中文](README.zh-CN.md)

Play Black Myth: Wukong with Minecraft movement, equipment and building. Wukong's maps, enemies and story remain; both games run together on one PC.

**[Download v0.4.0 Players.zip](https://github.com/Lassysi/MCX-Wukong/releases/download/v0.4.0/MCX-Wukong-v0.4.0-Players.zip)** · [What's new](https://github.com/Lassysi/MCX-Wukong/releases/tag/v0.4.0) · [Installation](docs/INSTALL.md) · [Upgrade](docs/UPGRADE.md)

## Features

- Fight with swords, bows, guns, TNT and potions; build cover and platforms.
- Trade at shrines, repair and enchant gear, and earn emeralds alongside native Will.
- Fly with elytra, travel with pearls, and trap ordinary enemies in boats or minecarts.
- Use one custom skin in both games. Press F8 when a quest needs Wukong's own skills.

## Requirements

Windows x64, **Black Myth: Wukong Steam build 21393610**, **Minecraft Java 1.21.4**, **Fabric 0.19.5** and **Java 21**. Download [CSharpLoader 0.1.0](https://www.nexusmods.com/blackmythwukong/mods/664) separately. Local single-player only.

## Get started

1. Extract **Players.zip** and install CSharpLoader following its author's instructions.
2. Import the included `.mrpack` into [Prism Launcher](https://prismlauncher.org/). Sign in, reach Minecraft's main menu once, then exit.
3. With both games closed, run `MCX-Wukong-Setup.exe`. Select the game folders and vanilla 1.21.4 client JAR; the skin field is optional.
4. Enter the **MCX-Wukong** world, keep Minecraft open and unminimized, then launch Black Myth. Play in the Black Myth window.

After connecting, `/function mcx:starter` grants starter gear once. Use a dedicated MCX world for each Black Myth save; exit Black Myth before Minecraft. For an existing installation, follow the [upgrade guide](docs/UPGRADE.md).

## Keys

| Action | Key |
|---|---|
| Movement, combat, blocks, inventory | Minecraft controls; E opens inventory |
| Gather, chests, shrines | H |
| Native map / menu | F6 / Esc |
| Switch MC / native Wukong control | F8 |
| Try skipping a supported cutscene | Hold E |

**Public test release:** some story transitions, high flight and capture cases still need testing. See [known issues](docs/KNOWN-ISSUES.md).

[Usage](docs/USAGE.md) · [Balance and shop](docs/BALANCE.md) · [Skins](docs/SKINS.md) · [FAQ](docs/FAQ.md) · [Performance](docs/PERFORMANCE.md) · [Changelog](CHANGELOG.md) · [Build](docs/BUILD.md)

Original code is MIT-licensed; [third-party licenses](THIRD-PARTY-NOTICES.md) still apply. Unofficial project, unaffiliated with Mojang, Microsoft or Game Science.
