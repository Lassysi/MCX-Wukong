# Upgrade from an earlier version

English | [简体中文](UPGRADE.zh-CN.md)

Update both bridge components together. Keep the previous player package, uninstall with its setup tool, then install v0.2.0 in a separate new Minecraft instance. Never replace a DLL or JAR while either game is running.

1. Save and exit both games normally.
2. Back up Black Myth's `b1/Saved/SaveGames` and the old instance's entire `saves/MCX-Wukong` folder. Keep the old player package and installation backups.
3. Run the **old version's** `MCX-Wukong-Setup.exe` with the original Black Myth and Minecraft folders, then click **Uninstall**. Saves, worlds and builds remain.
4. Extract the new Players ZIP into a new folder. Import the new `.mrpack` as a **new MC instance**, start it to the main menu once and exit normally.
5. To retain your MCX world, copy the old instance's entire `saves/MCX-Wukong` folder into the new instance's `saves` folder before entering the world. If a same-name world already exists at the destination, stop and preserve both; do not merge or overwrite them.
6. Run the new setup tool with the original Black Myth folder, the **new MC instance** game folder and the vanilla Minecraft 1.21.4 client JAR. Select your skin PNG again if needed, then install.
7. Enter `MCX-Wukong` in the new instance, start Black Myth and load the matching save. Keep backups and the old instance until you confirm you can continue.

An existing marked MCX world is not recreated. Before installing, the tool also backs up native saves and any existing MCX world under Black Myth's `b1/Binaries/Win64/MCX-Wukong-backups/<installation-id>/`. It never automatically restores these over later progress.

Do not copy old `mods`, the full `config` folder or `mcx-install-id.json` into the new instance. The installer checks pinned dependencies and rejects extra JARs.

Early development installs without `MCX-Wukong-install.json` are outside the public installer's upgrade path. Restore a clean game environment or contact the maintainer rather than deleting loaders, saves or mod files blindly.

To roll back, exit both games and uninstall with the new setup tool first. Preserve current progress, then follow the old version's installation guide. Do not overlay an old ZIP or combine mismatched save snapshots from the two games.
