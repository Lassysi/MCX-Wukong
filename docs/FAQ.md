# FAQ

English | [简体中文](FAQ.zh-CN.md)

**Can I use only one game?**

Both Black Myth: Wukong and Minecraft Java must be installed and running on the same PC.

**I play Black Myth and do not know Minecraft.**

Import the included `.mrpack`. Enter the MCX-Wukong world, then start Black Myth. Press T to open Minecraft chat and use `/function mcx:starter` for a one-time equipment kit.

**I play Minecraft. Where does the Black Myth part go?**

Open Black Myth's local files from Steam. Install CSharpLoader into `b1/Binaries/Win64`; select the game root in the MCX setup tool.

**Setup rejects my version.**

The package is pinned to the versions in the README. Other builds need validation. Do not force-copy DLLs around the check.

**I cannot find the client JAR.**

Launch the imported instance to the main menu once, then exit. Look in the launcher's data folder under `versions/1.21.4` or `libraries/com/mojang/minecraft/1.21.4`. Select the vanilla client, not a mod JAR.

**The bridge does not take over.**

Minecraft must be inside the bridge world and Black Myth must have loaded a save. Keep both windows unminimized. Check CSharpLoader's `Develop=1` and `EnableJit=1`. Run only one Minecraft instance.

**How do I get guns?**

The pack includes Gun Core and Modern Guns. After takeover, use `/gamemode creative` to find items in the creative inventory, then `/gamemode survival` to return. See [Modern Guns](https://modrinth.com/datapack/modern-guns) for the author's item and crafting information.

**Why is the demo's Monkey King skin different?**

The public package uses a project-drawn skin. Setup accepts your own classic 64x64 skin PNG and uses it in both games.

**Are my saves and builds kept?**

Setup creates a separate world and backs up Black Myth saves under `b1/Binaries/Win64/MCX-Wukong-backups`. Uninstall keeps progress and Minecraft builds.

**What do H and E do?**

H is for native interactions. Hold E to try skipping a story scene. Play with Black Myth focused.

**How do I report a problem?**

Include the public version, location, reproduction steps and relevant error text. Keep account files, personal saves and full crash dumps private.
