# 第一次安装

[English](INSTALL.md) | 简体中文

需要同时运行黑神话和 MC。先把两个游戏分别启动成功，再安装联动。

已有旧版安装的玩家先看[升级说明](UPGRADE.zh-CN.md)，不要直接覆盖。

## 1. 准备版本

| 内容 | 本包要求 | 从哪里准备 |
|---|---|---|
| 系统 | Windows 64 位；Windows 11 为目前测试环境 | 自己的电脑 |
| 黑神话：悟空 | Steam PC 版，build 21393610 | Steam 安装 |
| Minecraft | Java 版 1.21.4 | 登录自己的 Java 版账号，由启动器下载 |
| Java | 64 位 Java 21 | 在启动器的 Java 设置中选择；支持自动下载的启动器可按提示下载 |
| Fabric Loader | 0.19.5 | 导入随包 `.mrpack` 时配置 |
| CSharpLoader | 0.1.0 | [作者下载页](https://www.nexusmods.com/blackmythwukong/mods/664) |
| ReShade | 6.8.0 x64 | 玩家包已带，由安装工具放入游戏 |

MC 基岩版无法使用本包。已有其他版本的 MC 也需要另外建一个 1.21.4 实例。不要把整合包覆盖到平时玩的实例里。

暂未测出最低 CPU、内存和显存要求。双开会同时占用两个游戏的资源：

- MC 可先分配 4 GB 内存，其余留给黑神话和系统。不要把电脑全部内存分给 MC。
- MC 初始窗口为 1280×720、渲染距离 6。先保留随包设置，暂时不要添加光影或额外模组。
- 双开卡顿时，先降低黑神话画质和分辨率。笔记本检查两个游戏是否使用独立显卡。

安装和卸载时，两个游戏都要正常退出。游戏运行时不要替换模组文件。

## 2. 下载玩家包

下载 [MCX-Wukong-v0.2.0-Players.zip](https://github.com/Lassysi/MCX-Wukong/releases/download/v0.2.0/MCX-Wukong-v0.2.0-Players.zip)，解压到单独文件夹。

玩家用这个 ZIP。GitHub 自动生成的 `Source code` 是源码，不是安装包。

解压后应有 `MCX-Wukong-Setup.exe`、`MCX-Wukong-v0.2.0.mrpack`、`payload`、说明和许可文件。不要只取出 EXE，也不要把整个玩家包手工拖进游戏目录。

## 3. 安装黑神话加载器

1. Steam → 黑神话：悟空 → 管理 → 浏览本地文件，记下游戏目录。
2. 从 [CSharpLoader 原站](https://www.nexusmods.com/blackmythwukong/mods/664)下载 0.1.0，按作者说明解压。
3. 检查以下结构：

```text
BlackMythWukong/
  b1/Binaries/Win64/
    b1-Win64-Shipping.exe
    version.dll
    CSharpLoader/
      CSharpModBase.dll
      b1cs.ini
```

不要重复嵌套一层同名文件夹。保留加载器完整文件，安装工具会设置 `Develop=1`、`EnableJit=1`。

建议先使用没有其他人物、画面或玩法模组的黑神话环境。本包尚未验证与这些模组的兼容性。

## 4. 导入 MC 整合包

下面以 [Prism Launcher](https://prismlauncher.org/)为例。其他启动器需要支持 `.mrpack` 导入。

1. 安装启动器，添加并登录自己的 MC Java 账号。
2. 添加实例 → 导入，选择随包的 `MCX-Wukong-v0.2.0.mrpack`。
3. 等待 MC 模组下载完成。保留整合包指定的 Minecraft 1.21.4 和 Fabric 0.19.5。
4. 在实例设置里选择 **64 位 Java 21**，内存可先设为 4096 MiB。
5. 启动这个实例，等到 MC 主菜单后正常退出。首次启动会下载 MC 客户端和运行文件。
6. 从启动器打开这个实例的游戏文件夹，记下位置。需要的是含 `mods` 的文件夹；有些启动器叫它 `.minecraft`。

随包需要的 8 个第三方模组会由启动器下载：Better Combat、Cloth Config、Fabric API、Gun Core、Modern Guns、Mod Menu、Placeholder API、Player Animation Library。另有本项目的联动 JAR。不要自行更新版本或添加模组。

首次导入和启动需要联网。联动 JAR 已随 `.mrpack` 提供，其余模组按包内链接下载，无需逐个手动查找。原版 MC 或缺少指定模组的实例无法使用本项目。

## 5. 运行安装工具

确认两个游戏已退出，打开解压目录中的 `MCX-Wukong-Setup.exe`。

| 选择项 | 选什么 |
|---|---|
| 黑神话安装文件夹 | Steam 打开的 `BlackMythWukong` 根目录，里面有 `b1` |
| MC 整合包游戏文件夹 | 上一步打开的实例游戏目录，里面有 `mods` |
| 原版 MC 1.21.4 客户端 JAR | 优先使用自动找到的文件；手动选择方法见下方 |
| 自选皮肤 PNG | 留空使用项目自绘皮肤；自选时用 64×64、经典四像素手臂 PNG |

皮肤 PNG 可保存在任意文件夹，通过第四项右侧的“Browse / 选择”导入，无需放入游戏目录。已安装后更换皮肤的步骤见[自定义皮肤](SKINS.zh-CN.md)。

客户端 JAR 常在启动器数据目录的以下位置，具体文件名可能不同：

```text
versions/1.21.4/1.21.4.jar
libraries/com/mojang/minecraft/1.21.4/某个客户端.jar
```

选择 MC 客户端本体，不选 `mods` 中的模组、Fabric 安装器或服务端 JAR。安装器会检查文件版本。

“新存档跳过序章”默认勾选，会连序章战斗一起跳过。取消后按原序章流程进行；当前版本的开场外观仍可能有问题。

安装器中的字段、按钮和提示均有中英文。点击“Install / 安装”，等待完成。工具会备份原文件、黑神话存档和已有的联动世界，生成本机材质；首次安装时创建空白 `MCX-Wukong` 世界。无需安装 Python、.NET SDK 或 JDK。

版本不符时先停下，查看报错。不要用旧包覆盖新游戏文件来绕过检查。

## 6. 双开游玩

1. 在启动器启动刚导入的 MC 实例，进入单人游戏里的 **MCX-Wukong** 世界。
2. 保持 MC 运行，使用 Alt+Tab 切换窗口，不要最小化或关闭 MC。
3. 启动黑神话，选择新游戏或继续游戏。日常操作在黑神话窗口中进行。
4. 等待联动接管。首次初始化通常要几十秒；MC 等待时没有地形。
5. 接管后按 T，在 MC 聊天栏输入 `/function mcx:starter`，可领取一次基础装备。枪械和弹药可在已激活土地庙附近的商店购买，见[使用说明](USAGE.zh-CN.md)。

移动和装备使用 MC 按键；H 采集、开箱或操作土地庙，F6 打开黑神话地图，Esc 打开黑神话菜单。长按 E 尝试跳过实机 CG，部分剧情仍无法跳过。

金蝉等剧情变身由黑神话接管，等待原生剧情完成。不要在剧情中强行切换世界或退出其中一个游戏。

## 7. 存档、退出和卸载

- 用新建的联动世界游玩，不要使用平时的 MC 生存世界。联动会生成自己的碰撞地形。
- 黑神话和 MC 各自保存进度。每次游玩都进入同一个 MCX 实例和世界，再读取对应的黑神话存档。
- 不建议让不同的黑神话存档共用同一个联动世界。
- 结束时先让黑神话正常保存并退出，再从 MC 菜单保存并退出世界。
- 卸载时关闭两个游戏，重新运行安装工具，选择原来两个目录，点“卸载”。
- 卸载会保留游戏进度、MC 世界和安装备份。第三方 CSharpLoader 仍按作者说明管理。

安装前的原文件、黑神话存档和已有联动世界的备份保存在 `b1/Binaries/Win64/MCX-Wukong-backups`。卸载只还原程序文件，不会把你后来玩的进度退回旧存档。

这是公开测试版。全新环境安装、皮肤显示、长剧情和章节切换仍需进一步实机测试。遇到问题请先查看[常见问题](FAQ.zh-CN.md)和[已知问题](KNOWN-ISSUES.zh-CN.md)。
