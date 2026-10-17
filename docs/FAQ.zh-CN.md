# 常见问题

[English](FAQ.md) | 简体中文

**只装了一个游戏，能用吗？**

需要同时安装并运行黑神话和 Minecraft Java。

**我是黑神话玩家，不熟悉 MC。**

导入随包 `.mrpack`，不要自己凑模组。进入 `MCX-Wukong` 世界，再打开黑神话。装备可用 `/function mcx:starter` 领取一次；T 打开 MC 聊天栏。

**我是 MC 玩家，黑神话端装在哪里？**

Steam → 黑神话 → 管理 → 浏览本地文件。CSharpLoader 放在 `b1/Binaries/Win64`。安装工具选择游戏根目录。

**安装器说版本不符。**

不要强行替换 DLL。本版只核对了 README 所列游戏版本；其他版本需要重新验证。

**找不到原版 MC JAR。**

在启动器打开游戏文件夹或启动器的数据文件夹，寻找 `versions/1.21.4/1.21.4.jar`。部分启动器放在 `libraries/com/mojang/minecraft/1.21.4`。选择客户端，不选模组 JAR。

**只有 MC，黑神话没有接管。**

确认 MC 已进单人世界；黑神话已读档；两个窗口均未最小化；CSharpLoader 设置为 `Develop=1`、`EnableJit=1`。不要同时开启两个 MC 实例。

**怎么拿枪？**

整合包含 Gun Core 和 Modern Guns。可在联动后输入 `/gamemode creative`，从创造物品栏或作者提供的物品获取方式拿取，再输入 `/gamemode survival` 返回生存。具体配方见 [Modern Guns](https://modrinth.com/datapack/modern-guns)。

**视频里的大圣皮肤怎么没了？**

公开包使用项目自绘皮肤。你可以在安装器选择自己的 64×64、经典四像素手臂 PNG，MC 和黑神话会使用同一张皮肤。

**原来的存档、建筑会怎样？**

安装工具新建联动世界，不覆盖已有世界。黑神话存档会先备份到 `b1/Binaries/Win64/MCX-Wukong-backups`。卸载不回退进度，也不删除建筑。

**我要反馈问题。**

写明版本、发生位置、操作步骤和报错文字。不要上传账号文件、个人存档或完整崩溃转储。

**H 和 E 分别做什么？**

H 用于采集、开箱和土地庙交互。长按 E 仅用于尝试跳过实机剧情。游玩时保持黑神话窗口在前台。
