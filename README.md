# NoMercy Share

Capture something from the screen, upload it to a share host, and get the link in the clipboard. One click.

Status: steps 1 to 4 are built. Step 4 is the viewer: the Cloudflare Worker in `host/` serves share.nomercy.tv. A browser or a chat app preview gets a branded page; `?raw=1` or any other client gets the file itself; an expired link answers 410 and a sidecar `.json` is never served. See `host/README.md`. Steps 1 to 3: The bar, still capture (Region, Window, Full screen) and upload to an S3-compatible host (Cloudflare R2 first) work on Windows, and the share link goes on the clipboard. Global hotkeys work on Windows (`Hotkeys` in settings.json; defaults Win+Shift+S region, PrintScreen full screen, Alt+PrintScreen window, Shift+PrintScreen record; an empty value turns one off). Win+Shift+S opens NoMercy Share's region capture instead of the Snipping Tool; clear the Region hotkey to give it back to Windows. Web page capture works on Windows (see Web page capture). Settings opens the settings window (see Settings).

## Still capture (Windows)

The bar hides itself while the picture is taken (`Capture.HideDelayMilliseconds`, default 150) and comes back after. All pictures are in physical pixels.

| Button | How | Why |
| --- | --- | --- |
| Full screen | GDI BitBlt from the screen, of the monitor under the cursor (`Capture.FullScreenArea`: `CursorMonitor` or `VirtualScreen`) | A click on the bar puts the cursor on the bar's monitor; BitBlt is exact and needs no extra API for one still picture. |
| Window | The window that was active before the bar, else the window under the cursor. PrintWindow with PW_RENDERFULLCONTENT, cropped to the DWM frame (no invisible border); BitBlt of the frame if PrintWindow fails | Works while other windows cover it; no extra picking step. |
| Region | One BitBlt freezes the virtual screen, one overlay per monitor shows it dimmed outside the selection. Drag to select, mouse-up or Enter confirms, Esc cancels | Each monitor keeps its own display scale; a selection stays on one monitor. |

Files go to `Capture.Folder` (default: `NoMercy Share` under the user's Pictures folder) as `Capture.FileNamePattern` (default `nomercy-share-<UTC yyyyMMddTHHmmssfffZ>.png`, sortable). All four settings are in `settings.json`.

## Web page capture (Windows)

The Web page button opens a small window: the address (filled in when the clipboard holds an https address), the mode (Full page, Visible area, One element with a CSS selector) and the width (Desktop or Phone). Enter captures, Escape cancels. The picture is shared like any other capture.

It runs Edge (else Chrome) headless with its own empty profile over the DevTools protocol, and stops it after each capture. Pages open signed out: the profile holds no cookies. Only http and https addresses are opened. Pictures are made at 2 pixels per CSS pixel.

| Setting (`WebCapture`) | Default | What it does |
| --- | --- | --- |
| `BrowserPath` | blank | The browser file; blank finds Edge, then Chrome, in their standard folders. |
| `DesktopWidth`, `PhoneWidth` | 1280, 390 | The page width in CSS pixels. |
| `Height` | 800 | The height of the visible area. |
| `ScaleFactor` | 2 | Picture pixels per CSS pixel. |
| `LoadTimeoutSeconds` | 30 | How long a page gets to load. |
| `SettleMilliseconds` | 500 | The wait after the load, for late layout. |
| `MaxFullPageHeight` | 16384 | A taller page or element is cut off here. |

## Upload and expiry

`Storage.Provider` is `r2` or `s3` (the same S3 client; R2 is the default). It uses the AWS SDK for .NET with a custom `Storage.Endpoint` and path-style addressing: the SDK already signs, retries and reads S3 errors, so a hand-written SigV4 client would be more code to keep correct. R2 needs `Storage.Region` = `auto` (the default).

| Setting | Default | Meaning |
| --- | --- | --- |
| `Storage.Endpoint`, `Storage.Bucket`, `Storage.PublicBaseUrl`, `Storage.CredentialName` | empty | Required. A missing one fails the upload with a notice that names it, for example `Storage.Bucket`. |
| `Storage.KeyLength` | 10 | Length of the random id in the object key (letters and digits). The key never holds the local file name. |
| `Storage.ExpiryBucketDays` | `[1, 7, 30, 90]` | The lifetimes that have a prefix on the host. |
| `Storage.ExpiryPrefixPattern`, `Storage.KeepPrefix`, `Storage.SidecarSuffix` | `exp-{0}d`, `keep`, `.json` | Folder names and the sidecar suffix. |
| `Expiry.DefaultDays` | 30 (null = never) | The lifetime of an upload that names none. |
| `Clipboard.Content` | `Link` | `Link`, `Image` or `Both` after a capture is shared. |

Discord as a provider: `Storage.Provider` = `discord` uploads the file itself to a channel through a webhook (`?wait=true`, the file as `files[0]`) and the Discord file link goes on the clipboard. The webhook address is a secret in the credential store (`Discord.CredentialName`, default `nomercy-share-discord`), never in settings.json. Two limits: Discord links are signed and expire (the `ex` value in the link, shown in the recent list; the default expiry does not apply), and the file size cap is a setting, `Discord.MaxUploadMegabytes` (default 10). A bigger file fails before the upload; recordings are squeezed to the smaller of `Recording.MaxSizeMegabytes` and that cap. `Discord.TimeoutSeconds` (default 10) limits one upload.

The link is `Storage.PublicBaseUrl` + `/` + the key, for example `https://share.example.test/exp-7d/aB3dE5gH7j.png`.

Expiry: the host deletes, the app does not. An upload goes under the smallest prefix that covers its lifetime (`exp-7d/` for 2 to 7 days; longer than the largest goes under the largest) and `never` goes under `keep/`. The object carries the metadata `x-amz-meta-expires-at` (UTC, `2026-12-04T10:00:00Z`, or `never`) and a sidecar `<key>.json` with the key, upload time, expiry, content type and size. The link's expiry is the prefix's deletion time, so it is never earlier than asked. The host needs one lifecycle rule per prefix: on R2, "delete objects after N days" with the prefix `exp-1d/`, `exp-7d/`, `exp-30d/` and `exp-90d/` (N matching the days in the name). Nothing deletes `keep/`.

Credentials: `Storage.CredentialName` names a generic credential in the Windows Credential Manager. The user name is the access key id, the password is the secret:

```
cmdkey /generic:NAME /user:ACCESS_KEY_ID /pass:SECRET
```

A failed upload keeps the PNG, shows `Upload failed: <reason>. The file is kept.` on the bar and leaves the clipboard as it was. The reason is the service's error code or "could not be reached"; no secret is in any message.

## The bar

A small borderless, always-on-top pill. Drag it by the handle at the left edge; it remembers its position in `settings.json` in the user's app-data folder (`NoMercy Share`) and comes back on a visible screen.

Buttons: region, window, full screen, record, web page, recent links, settings, close. Icons are the Moooom set; colors, radii and spacing come from the NM design tokens. Every button has a tooltip and an accessible name, and Tab and Enter work.

## Settings

The bar's Settings button opens one window with four sections: Sharing (provider: Cloudflare R2, S3-compatible or Discord; with Discord only the webhook address; endpoint, bucket, region, public link base URL, access key id, secret access key), Links (default expiry: never, 1, 7, 30 or 90 days), Hotkeys (click a field and press the keys; the cross turns a hotkey off) and Recording (max size in MB, frame rate). Save checks every field, writes `settings.json` and the access key to the credential store (default name `nomercy-share`), and applies it: the next share uses the new host and expiry, the next recording the new limits, and the hotkeys register again. The secret is never shown or loaded back; an empty secret box keeps the stored one. Cancel writes nothing. Set `NOMERCY_SHARE_DATA` to a folder to keep `settings.json` and `recent.json` there instead of the user's app-data folder, for a portable or test copy.

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
