# JulianBar for Windows

Windows port of the macOS JulianBar menu bar app. Shows today's date as `yyD`
(2-digit year + day of year, e.g. `26284`) in the system tray, refreshed every 60 seconds.

- The icon shows the year stacked over the day of year. Hover for the full date.
- Right-click: **Start with Windows** (toggle) and **Quit App**.

## Get the exe
Push to GitHub, open the **Actions** tab, run/open "Build Windows exe", and download the
`JulianBar-win-x64` artifact. No install needed.

## Keep it on the taskbar
Windows hides new tray icons in the `^` overflow. Click `^` and drag the JulianBar icon down
next to the clock (or Settings > Personalization > Taskbar > Other system tray icons > JulianBar: On).

## Build locally
```
dotnet publish JulianBar.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o out
```
