# Custom skins

English | [简体中文](SKINS.zh-CN.md)

Setup applies the same skin to the block character in Minecraft and Black Myth. Leave the field blank to use the project-drawn skin.

## File requirements

- **64×64 pixel PNG**.
- **Steve's classic four-pixel arms** layout. Alex's three-pixel slim arms, legacy 64×32 skins and other sizes are unsupported.
- Select the skin PNG itself, not a preview screenshot, resource-pack ZIP or character model.

Store the file in any folder. It does not belong in `mods`, `resourcepacks` or the Black Myth folder. Setup does not automatically import the skin on your Minecraft account.

## Import during initial installation

1. Exit Black Myth and Minecraft normally.
2. Open `MCX-Wukong-Setup.exe` from the fully extracted player package.
3. Select the Black Myth folder, Minecraft instance game folder and vanilla client JAR as described in the [installation guide](INSTALL.md).
4. Click **Browse / 选择** beside the fourth field, **Custom skin PNG / 自选皮肤 PNG**, and select your PNG.
5. Click **Install / 安装**, then follow the guide to start both games.

Setup generates the Black Myth character appearance and updates the skin inside the MCX bridge JAR. No manual texture replacement is required.

## Change an existing installation

1. Exit both games normally. Keep the complete player package and original installation folders.
2. Open the same version of setup and select the original Black Myth and Minecraft instance game folders.
3. Click **Uninstall / 卸载**. Saves, Minecraft worlds and builds are kept.
4. Confirm the installation paths and vanilla client JAR, then select the new PNG in the fourth field.
5. Click **Install / 安装**. Continue with the same bridge world and matching Black Myth save.

To restore the project skin, follow these steps and leave the fourth field blank when reinstalling.

## Notes

- Live skin switching is unsupported. Editing the original PNG also requires reinstallation to apply the change.
- Do not edit `payload/skin.png`, modify the bridge JAR or replace `steve.mesh` manually. Setup checks file integrity; manual changes may prevent installation or removal.
- Both games use the same skin file, but rendering differences may affect its appearance.
- Some real-time cutscenes or story transformations may still show the original character. Importing a skin does not resolve every scene; see [known issues](KNOWN-ISSUES.md).
- Confirm the necessary rights before redistributing a skin.
