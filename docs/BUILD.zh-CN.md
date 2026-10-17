# 编译

[English](BUILD.md) | 简体中文

玩家下载 Releases，不需要编译。

## 黑神话端

1. 安装 .NET SDK，准备自己游戏版本对应的 C# 引用和 CSharpLoader。
2. 将引用放在仓库外的私有文件夹。
3. 执行：

```powershell
dotnet build src/DqjCombat/DqjCombat.csproj -c Release -p:Stage=6 -p:GameReferencesDir="引用文件夹" -p:LoaderReferencePath="CSharpModBase.dll完整路径" -p:DebugType=None
```

引用不随仓库提供，也不要上传。加载器要求保持 `Develop=1`。新增代码不要使用 const 字段或 System.Linq.Expressions。发布前用 `tools/CecilCheck` 检查 DLL，须出现 `### Write 完成`，且没有 `ASSEMBLY-RESOLVE`。

## MC 端

安装 JDK 25，在 `bridge/minecraft-1.21.4` 执行：

```powershell
.\gradlew.bat build
```

运行用 Java 21 或更高版本。正式 JAR 只包含 `main` 和 `client`，没有测试辅助包。

## 合成器

Windows x64，安装支持 C++17 的 MinGW-w64。执行 `tools/build-compositor.ps1`。SDK 固定为 ReShade API 20；不要自行替换版本。

## 玩家安装工具

```powershell
python -m pip install -r tools/requirements-build.txt
python -m PyInstaller --clean --onefile --windowed --name MCX-Wukong-Setup tools/player_setup.py
```

安装工具与 `bundle.json`、`payload`、依赖清单放在同一文件夹。`tools/verify_public.py` 检查公开文件边界；`tools/package_release.py` 按明确清单制作源码 ZIP、玩家 ZIP 和 `.mrpack`。

## 打包

修改源码后执行 `python tools/update_manifest.py`。新文件需用 `--add 相对路径` 加入清单；删文件时手动删掉清单中的对应项。Git 暂存区也必须与清单一致。

准备已检查的黑神话端 DLL、合成器、ReShade 6.8.0 x64 和安装工具 EXE，执行：

```powershell
python tools/package_release.py --host "DqjCombat.dll完整路径" --addon "DqjMCCompositor.addon64完整路径" --reshade "ReShade64.dll完整路径" --setup "MCX-Wukong-Setup.exe完整路径" --output "交付文件夹" --staging "空的打包工作目录"
```

打包工作目录中的本版文件夹必须不存在，避免混入旧版文件。交付目录只生成两个 ZIP 和 SHA256SUMS.txt。不要直接压缩开发目录。第三方 MC 模组只写入下载清单，CSharpLoader 和游戏引用不打包。

## 离线检查

```powershell
python tools/run_checks.py
```

检查通过不代表已完成跨章节实机验证。

对外版本统一在 `tools/release_info.py` 和 MC 的 `gradle.properties` 设置，必须保持一致。原生加载器依赖的 DLL、着色器和安装目录名称保持不变；不要为了显示名称修改这些接口。
