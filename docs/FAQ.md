# FAQ

English | [简体中文](FAQ.zh-CN.md)

**Which download do I need?**
Players.zip. Extract the whole folder; keep setup, `.mrpack` and `payload` together. Source.zip is for developers.

**Where are the MC folder and client JAR?**
Import the `.mrpack` first; opening Prism alone creates no instance. In Prism, open that instance's Minecraft folder and select the folder containing `mods`. The vanilla JAR is usually under Prism's `libraries/com/mojang/minecraft/1.21.4/`; it is not `java.exe`. See [installation](INSTALL.md).

**Why are the hands/body missing in the empty MC world?**
The new bridge world waits in spectator mode. Start Black Myth and load a save; the bridge changes mode after connecting. If it stays that way, check the bridge logs.

**How do I use Wukong's native spell for a quest?**
Press F8, cast it normally, then press F8 to return. Switch requests wait during cutscenes/loading. The native character can still take damage.

**Why can't I place blocks?**
Check for Adventure mode. `/gamemode survival` restores ordinary placement. Story gates remain native and cannot be forced open with building tools.

**Where do I buy ammunition or repair gear?**
Near an activated shrine, right-click with an empty hand or emeralds. Ammo is beside the matching guns. Put damaged gear in the main inventory for repair trades; resting no longer repairs automatically. See [usage](USAGE.md).

**How do I change my skin?**
Use setup's fourth field with a classic-arm 64×64 PNG. An existing installation must be uninstalled first; keep your saves. Follow [skins](SKINS.md).

**Can I play Bedrock or multiplayer?**
No. This package requires the specified Java instance and both games on one Windows PC.

**The bridge won't recover, or performance is poor.**
Keep MC open and unminimized, stay in the paired world, and let story/loading finish. Check the [known issues](KNOWN-ISSUES.md) and [performance guide](PERFORMANCE.md). Report the version, location, reproduction steps and relevant error lines; keep account data and saves private.
