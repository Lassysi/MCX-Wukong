# FAQ

English | [简体中文](FAQ.zh-CN.md)

**Can I use only one game?**

Both Black Myth: Wukong and Minecraft Java must be installed and running on the same PC.

**Are Minecraft mods required, and are they included?**

Use the included modpack. The Players ZIP contains the `.mrpack` and MCX bridge JAR. On import, the launcher downloads the other eight pinned mods, so initial installation requires internet access. Unmodded Minecraft is unsupported. You do not need to find each mod separately.

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

Near an activated shrine, right-click with an empty main hand or emeralds to buy the supported guns and matching ammunition. All base gun families are available from chapter one. You can also use `/gamemode creative` to find items in the creative inventory, then `/gamemode survival` to return. See [Modern Guns](https://modrinth.com/datapack/modern-guns) for the author's item and crafting information.

**Are commands and creative mode available?**

The world created by setup has commands enabled. After takeover, press T to open Minecraft chat. Use `/gamemode creative`, `/gamemode survival` or `/give` as needed. During story scenes, transformations or chapter loading, wait until Black Myth returns control before using them.

**Does equipment affect damage and defense?**

Weapon and armor attributes affect combat. Incoming Black Myth damage and armor reduction use adjusted bridge rules, so vanilla Minecraft values do not apply directly. Higher-tier equipment may make early encounters easier.

**Why is the demo's Monkey King skin different?**

The public package uses a project-drawn skin. Select a classic 64×64 PNG in setup's fourth field, **Custom skin PNG**, to use it in both games. The file can be stored anywhere. Minecraft account skins are not imported automatically. See [custom skins](SKINS.md) for initial import and later changes.

**Are my saves and builds kept?**

Setup creates a separate world only when absent and backs up Black Myth saves and existing marked MCX worlds under `b1/Binaries/Win64/MCX-Wukong-backups`. Uninstall keeps progress and Minecraft builds.

**What do H and E do?**

H is for native interactions. Hold E to try skipping a story scene. Play with Black Myth focused.

**How do I report a problem?**

Include the public version, location, reproduction steps and relevant error text. Keep account files, personal saves and full crash dumps private.

**How do I use the new shop, repairs and enchanting?**

Activate a shrine with H. Nearby, right-click with an empty main hand or emeralds to open the shop. Put damaged equipment in the main inventory and use the repair trades at the top; resting no longer spends emeralds. For enchanting, hold equipment in the main hand and an enchanted book in the offhand, then right-click near the shrine. See [usage](USAGE.md).

**How do I keep my saves when upgrading?**

Back up both games, uninstall with the old setup tool, import the new modpack into a new instance, copy the old MCX world, then install the new version. Do not copy old mods or installation records. See [upgrade](UPGRADE.md).

**Why does a bow not fire, and why does right-click not raise my shield?**

Check arrows or matching ammunition and load the crossbow first. A ranged main-hand weapon takes priority over shield use. Switch to a melee weapon to use the shield.
