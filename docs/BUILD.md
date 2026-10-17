# Build

English | [简体中文](BUILD.zh-CN.md)

Players should use the Players ZIP from Releases.

## Black Myth host

Install the .NET SDK and keep your version-matched game references and CSharpLoader outside the public repository.

```powershell
dotnet build src/DqjCombat/DqjCombat.csproj -c Release -p:Stage=6 -p:GameReferencesDir="PRIVATE_REFERENCE_FOLDER" -p:LoaderReferencePath="FULL_PATH_TO_CSharpModBase.dll" -p:DebugType=None
```

Do not redistribute the game references. Keep the loader's `Develop=1`. Avoid const fields and System.Linq.Expressions in host changes. Check the DLL with `tools/CecilCheck`; its rewrite must complete with no `ASSEMBLY-RESOLVE` errors.

## Minecraft bridge

Use JDK 25 to build. In `bridge/minecraft-1.21.4`, run:

```powershell
.\gradlew.bat build
```

Players use 64-bit Java 21. The release JAR includes main and client code without the development lab helper.

## Compositor

Use Windows x64 and a C++17-capable MinGW-w64 compiler. Run `tools/build-compositor.ps1`. The SDK is pinned to ReShade API 20.

## Setup tool

```powershell
python -m pip install -r tools/requirements-build.txt
python -m PyInstaller --clean --onefile --windowed --name MCX-Wukong-Setup tools/player_setup.py
```

## Package

Keep the public version in `tools/release_info.py` and Minecraft's `gradle.properties` consistent. Native DLL, shader and install-directory names retain their loader contracts.

Refresh reviewed source hashes with `python tools/update_manifest.py`. Add new paths explicitly with `--add RELATIVE_PATH`; remove obsolete entries explicitly. Stage only the manifest paths and PUBLIC-FILES.json. The Git index must match the source allowlist before packaging.

```powershell
python tools/package_release.py --host "HOST_DLL" --addon "COMPOSITOR_ADDON" --reshade "RESHADEx64_DLL" --setup "SETUP_EXE" --output "RELEASE_FOLDER" --staging "PACKAGE_WORK_FOLDER"
```

The staging folder for this version must not already exist. Output is a Players ZIP, a Source ZIP and SHA256SUMS.txt. Minecraft dependencies remain pinned downloads; CSharpLoader and game references are not bundled.

## Offline checks

```powershell
python tools/run_checks.py
```

Passing these checks does not validate live chapter transitions or cutscene appearance.
