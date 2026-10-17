param([string]$Compiler='g++',[string]$Output)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(!$Output){$Output=Join-Path $root 'dist\DqjMCCompositor.addon64'}
New-Item -ItemType Directory -Path (Split-Path -Parent $Output) -Force | Out-Null
$sources=@('addon.cpp','compositor.cpp','host_state.cpp','depth_probe.cpp','depth_trace.cpp','pose_trace.cpp') | ForEach-Object {Join-Path $root ('bridge\compositor\'+$_)}
& $Compiler -std=c++17 -O2 -shared -static-libgcc -static-libstdc++ ('-I'+(Join-Path $root 'bridge\compositor\include')) -Wno-attributes @sources -o $Output
if($LASTEXITCODE -ne 0){throw 'Compositor build failed'}
Write-Output $Output
