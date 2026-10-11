# JulianBar for Windows

Windows port of the macOS JulianBar menu bar app. A small widget docked on the taskbar,
just left of the clock, showing today's date as zero-padded `YYDDD` (e.g. `26284`, `26005`),
refreshed every 60 seconds.

- Hover for the full date.
- Right-click: **Start with Windows** (toggle) and **Quit App**.
- Follows the Windows light/dark theme and re-docks if the taskbar moves or resizes.

## Get the exe
Push to GitHub, open the **Actions** tab, run/open "Build Windows exe", and download the
`JulianBar-win-x64` artifact. No install needed.

## Build locally
```
dotnet publish JulianBar.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o out
```
