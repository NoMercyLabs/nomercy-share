# NoMercy Share

Capture something from the screen, upload it to a share host, and get the link in the clipboard. One click.

Status: step 2 of the build. The floating bar is movable, and Region, Window and Full screen take a still picture on Windows: a PNG file in the captures folder and the image on the clipboard ("Copied to clipboard"). Upload, recording and web page capture are not built yet; those buttons show "Coming in a later step".

## Still capture (Windows)

The bar hides itself while the picture is taken (`Capture.HideDelayMilliseconds`, default 150) and comes back after. All pictures are in physical pixels.

| Button | How | Why |
| --- | --- | --- |
| Full screen | GDI BitBlt from the screen, of the monitor under the cursor (`Capture.FullScreenArea`: `CursorMonitor` or `VirtualScreen`) | A click on the bar puts the cursor on the bar's monitor; BitBlt is exact and needs no extra API for one still picture. |
| Window | The window that was active before the bar, else the window under the cursor. PrintWindow with PW_RENDERFULLCONTENT, cropped to the DWM frame (no invisible border); BitBlt of the frame if PrintWindow fails | Works while other windows cover it; no extra picking step. |
| Region | One BitBlt freezes the virtual screen, one overlay per monitor shows it dimmed outside the selection. Drag to select, mouse-up or Enter confirms, Esc cancels | Each monitor keeps its own display scale; a selection stays on one monitor. |

Files go to `Capture.Folder` (default: `NoMercy Share` under the user's Pictures folder) as `Capture.FileNamePattern` (default `nomercy-share-<UTC yyyyMMddTHHmmssfffZ>.png`, sortable). All four settings are in `settings.json`.

## The bar

A small borderless, always-on-top pill. Drag it by the handle at the left edge; it remembers its position in `settings.json` in the user's app-data folder (`NoMercy Share`) and comes back on a visible screen.

Buttons: region, window, full screen, record, web page, recent links, settings, close. Icons are the Moooom set; colors, radii and spacing come from the NM design tokens. Every button has a tooltip and an accessible name, and Tab and Enter work.

## Architecture

Every action is an interface in `NoMercy.Share.Core`. Each platform registers its own implementations with dependency injection.

| Project | What it holds |
| --- | --- |
| `NoMercy.Share.Core` | The interfaces (`IScreenCapture`, `IScreenRecorder`, `IWebPageCapture`, `IShareUploader`, `IClipboard`, `IGlobalHotkeys`), the settings, the bar view model. No UI toolkit, no OS code. |
| `NoMercy.Share.Platform.Windows` | `AddWindowsPlatform()`: the Windows implementations (screen capture built; recording, web page and hotkeys are stubs). |
| `NoMercy.Share.Storage.S3` | S3-compatible storage; Cloudflare R2 is the first provider (a stub in step 1). |
| `NoMercy.Share.App` | The Avalonia app and the composition root. The clipboard lives here because it needs this UI toolkit. |

Storage is configurable: the provider is chosen by name in settings (`Storage.Provider`, default `r2`), so another host is one more registered uploader. Credentials are named in settings and never stored in the file. Links can expire: `Expiry.DefaultDays` (default 30, null means never), and an upload can override it.

## Build and run

Needs the .NET 10 SDK.

```
dotnet build NoMercy.Share.sln -c Release
dotnet test NoMercy.Share.sln
dotnet run --project src/NoMercy.Share.App
```

Windows only for now; other systems print a clear message and exit.

Proof images (headless render): `dotnet run --project tools/NoMercy.Share.Screenshots -- <folder>`.

Icons: `node tools/nm-icons/emit-avalonia-icons.mjs <moooom icons folder> src/NoMercy.Share.App/Icons/icons.json src/NoMercy.Share.App/Icons/MoooomIconData.g.cs`

## License

MIT, NoMercy Labs.
