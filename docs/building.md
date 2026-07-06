# Building and Deploying

## Prerequisites

- [Playnite](https://playnite.link) (Desktop mode)
- [.NET Framework 4.8.1](https://dotnet.microsoft.com/download/dotnet-framework/net481) Developer Pack
- [MSBuild 17.14](https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022) (Build Tools for Visual Studio 2022)
- NuGet packages are restored automatically during build

## Dependencies (NuGet)

- `PlayniteSDK.6.15.0` — Playnite SDK
- `Stub.System.Data.SQLite.Core.NetFramework.1.0.119.0` — SQLite for .NET Framework

## Build Instructions

### Close Playnite first

Playnite locks `GamesRecap.dll` while running, so it must be closed before building.

```powershell
Stop-Process -Name "Playnite.DesktopApp" -Force -ErrorAction SilentlyContinue; Start-Sleep 2
```

### Full rebuild (required after resource changes: XAML, images)

```powershell
cd GamesRecapPlugin\GamesRecap
& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" GamesRecap.csproj /t:Clean,Rebuild /p:Configuration=Debug /p:Platform=AnyCPU /v:m
```

### Quick build (code-only changes)

```powershell
cd GamesRecapPlugin\GamesRecap
& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" GamesRecap.csproj /t:Build /p:Configuration=Debug /p:Platform=AnyCPU /v:m /nologo
```

### Build flags

- `/v:m` — shows only warnings and errors
- `/nologo` — skips the MSBuild banner

## Deploy

No file copying is needed — Playnite loads the plugin directly from `bin\Debug\GamesRecap.dll`. Simply open Playnite after building.

## Typical Test Cycle

1. Close Playnite (`Stop-Process`)
2. Build (MSBuild)
3. Open Playnite
4. Open Games Recap (sidebar)
5. Perform test actions
6. Check logs at `<PlayniteDir>\extensions.log`
7. Repeat

## Packaging (.pext)

Generate a `.pext` file for distribution:

```powershell
# TODO: Add pext packaging command
```

The `.pext` file is a ZIP archive containing:
- `GamesRecap.dll`
- `extension.yaml`
- `icon.png`
- All dependency DLLs
- Localization files
