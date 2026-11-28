# Development

## Architecture

Every action is an interface in `NoMercy.Share.Core`. Each platform registers its own implementations with dependency injection.

| Project | What it holds |
| --- | --- |
| `NoMercy.Share.Core` | The interfaces (`IScreenCapture`, `IScreenRecorder`, `IWebPageCapture`, `IShareUploader`, `IClipboard`), the settings, the bar view model. No UI toolkit, no OS code. |
| `NoMercy.Share.Platform.Windows` | `AddWindowsPlatform()`: screen capture, recording, web page capture, hotkeys and the credential store. |
| `NoMercy.Share.Storage.S3` | S3-compatible storage; Cloudflare R2 is the first provider (`AddS3Storage()` registers `r2` and `s3`). |
| `NoMercy.Share.Storage.Discord` | Discord as a provider (`AddDiscordStorage()` registers `discord`). |
| `NoMercy.Share.App` | The Avalonia app and the composition root. The clipboard lives here because it needs this UI toolkit. |

Storage is configurable: the provider is chosen by name in settings (`Storage.Provider`, default `r2`), so another host is one more registered uploader. Credentials are named in settings and never stored in the file.

The bar uses the Moooom icon set, and its colors, radii and spacing come from the NM design tokens.

## Build and run

Needs the .NET 10 SDK.

```
dotnet build NoMercy.Share.sln -c Release
dotnet test NoMercy.Share.sln
dotnet run --project src/NoMercy.Share.App
```

Regenerate the icons from the Moooom icon folder:

```
node tools/nm-icons/emit-avalonia-icons.mjs <moooom icons folder> src/NoMercy.Share.App/Icons/icons.json src/NoMercy.Share.App/Icons/MoooomIconData.g.cs
```

## Screenshots

`dotnet run --project tools/NoMercy.Share.Screenshots -- <folder>` renders the bar, the region overlay, the recent list, Settings and the bar with its web page options headless, in the dark and light themes, with made-up content. The pictures in `docs/images` come from those renders and from headless Chrome runs of the extension and the share page on a made-up local page. Keep every capture free of real names, paths, addresses and keys.

## Share page host

`host/` is the Cloudflare Worker behind the share page. See [its README](../host/README.md) for routing, settings and deploy.
