# First-time installation

English | [简体中文](INSTALL.zh-CN.md)

You need both games. Make sure each game starts on its own before installing the bridge.

## 1. Check the requirements

| Component | Required version | Where to get it |
|---|---|---|
| OS | 64-bit Windows; Windows 11 is the reference environment | Your PC |
| Black Myth: Wukong | Steam PC build 21393610 | Steam |
| Minecraft | Java Edition 1.21.4 | Your launcher, using your own Java Edition account |
| Java runtime | 64-bit Java 21 | Select it in the launcher; use automatic download if your launcher offers it |
| Fabric Loader | 0.19.5 | Configured when importing the included `.mrpack` |
| CSharpLoader | 0.1.0 | [Author's download page](https://www.nexusmods.com/blackmythwukong/mods/664) |
| ReShade | 6.8.0 x64 | Included in the player package and installed by the setup tool |

Bedrock Edition is incompatible. If you already play another Minecraft version, make a separate 1.21.4 instance for this modpack.

Minimum CPU, RAM and VRAM requirements for running both games have not been measured. Try allocating 4096 MiB to Minecraft, leaving enough memory for Black Myth and Windows. Keep the included Minecraft settings initially: a 1280x720 window and render distance 6. Avoid extra shaders or mods. If performance is poor, lower Black Myth's graphics settings or resolution. On laptops, check that both games use the dedicated GPU.

Exit both games normally before installing or uninstalling.

## 2. Get the player package

Download [MCX-Wukong-v0.1.0-beta.1-Players.zip](https://github.com/Lassysi/MCX-Wukong/releases/download/v0.1.0-beta.1/MCX-Wukong-v0.1.0-beta.1-Players.zip) and extract it into a separate folder.

Players need the **Players ZIP**. GitHub's automatic **Source code** downloads are for developers.

The extracted folder contains `MCX-Wukong-Setup.exe`, `MCX-Wukong-v0.1.0-beta.1.mrpack`, `payload`, guides and licenses. Keep these files together; run the installer from this folder.

## 3. Install the Black Myth loader

1. In Steam, open Black Myth's local installation folder and note its location.
2. Download CSharpLoader 0.1.0 from its [author](https://www.nexusmods.com/blackmythwukong/mods/664) and follow the author's installation instructions.
3. Check this layout:

```text
BlackMythWukong/
  b1/Binaries/Win64/
    b1-Win64-Shipping.exe
    version.dll
    CSharpLoader/
      CSharpModBase.dll
      b1cs.ini
```

Keep the full loader installation and avoid an extra nested folder. The setup tool enables `Develop=1` and `EnableJit=1`.

Start with a clean Black Myth installation. Compatibility with other character, graphics and gameplay mods has not been tested.

## 4. Import the Minecraft modpack

These steps use [Prism Launcher](https://prismlauncher.org/). Other launchers must support `.mrpack` imports.

1. Install the launcher and sign in with your own Minecraft Java account.
2. Add an instance using Import, then select `MCX-Wukong-v0.1.0-beta.1.mrpack`.
3. Wait for the mods to download. Keep Minecraft 1.21.4 and Fabric Loader 0.19.5.
4. Select **64-bit Java 21** for the instance. You can start with a 4096 MiB memory allocation.
5. Launch to Minecraft's main menu, then exit normally. This first launch downloads the client and runtime files.
6. Use the launcher to open this instance's game folder. The folder contains `mods`; some launchers call it `.minecraft`.

The launcher downloads eight third-party mods: Better Combat, Cloth Config, Fabric API, Gun Core, Modern Guns, Mod Menu, Placeholder API and Player Animation Library. The pack also includes the MCX bridge JAR. Keep the pinned versions and use this instance without extra mods.

Initial import and startup require internet access. The MCX bridge JAR is included in the `.mrpack`; the launcher downloads the remaining mods from its pinned links. No individual mod search is required. Unmodded Minecraft or an instance missing the required mods cannot run the bridge.

## 5. Run setup

With both games closed, run `MCX-Wukong-Setup.exe` from the extracted package. All fields and messages have English and Chinese text.

| Field | Select |
|---|---|
| Black Myth installation folder | The `BlackMythWukong` root folder containing `b1` |
| Minecraft instance game folder | The imported instance folder containing `mods` |
| Vanilla Minecraft 1.21.4 client JAR | Use the automatically found file, or locate it as described below |
| Custom skin PNG | Leave blank for the project skin, or choose a 64x64 PNG with classic four-pixel arms |

The PNG can be stored in any folder. Use **Browse / 选择** beside the fourth field; there is no skin folder to copy it into. For changing an existing installation, see [custom skins](SKINS.md).

The client JAR is usually in the launcher's data folder. Example locations:

```text
versions/1.21.4/1.21.4.jar
libraries/com/mojang/minecraft/1.21.4/some-client.jar
```

Select the vanilla game client, not a file in `mods`, a Fabric installer or a server JAR. Setup checks the file version and hash.

**Skip the entire prologue in new saves** is checked by default, including its combat. Uncheck it to keep the native prologue. Opening character appearance may still have issues.

Click **Install / 安装** and wait. Setup backs up replaced files and Black Myth saves, generates local textures and a character mesh, and creates an empty **MCX-Wukong** world. Players do not need Python, a JDK or the .NET SDK.

If a version check fails, read the error and stop. Other game builds need separate validation.

## 6. Run both games

1. Start the imported Minecraft instance and enter its **MCX-Wukong** single-player world.
2. Keep Minecraft running. Use Alt+Tab to switch windows; leave Minecraft unminimized.
3. Start Black Myth and choose New Game or Continue. Play with the Black Myth window focused.
4. Wait for bridge initialization, which can take tens of seconds. The Minecraft world is empty while waiting.
5. After the bridge takes over, press T and enter `/function mcx:starter` to claim basic equipment once. See the FAQ for guns.

Use Minecraft controls to move and manage equipment. Press H for gathering, chests and shrines, F6 for Black Myth's map, and Esc for its menu. Hold E to try skipping real-time cutscenes; some scenes still cannot be skipped.

Native story transformations such as the cicada stay under Black Myth's control. Wait for them to finish; avoid changing Minecraft worlds or exiting one game during a scene.

## 7. Save, exit and uninstall

- Use the dedicated bridge world. The bridge generates its own collision terrain there.
- The two games save their progress separately. Reuse the same Minecraft instance and world with the matching Black Myth save. Avoid sharing one bridge world across different Black Myth saves.
- Let Black Myth save and exit normally, then save and exit the Minecraft world.
- To uninstall, close both games, run setup with the original two folders, and click **Uninstall / 卸载**.
- Saves, worlds, backups and the third-party loader are kept. Uninstall restores program files without rolling back later game progress.

Original-file and Black Myth save backups are under `b1/Binaries/Win64/MCX-Wukong-backups`.

This is a public beta. Fresh installations, skin appearance, long story scenes and chapter transitions require further in-game testing. See the [FAQ](FAQ.md) and [known issues](KNOWN-ISSUES.md).
