# Settings file

Everything in the Settings window is stored in `settings.json`, and a few options exist only there. The file lives in the `NoMercy Share` folder inside your roaming app data folder. Set the `NOMERCY_SHARE_DATA` environment variable to a folder to keep `settings.json` and `recent.json` there instead, for a portable or test copy.

The bar remembers its position in the same file.

## Capture

The bar hides itself while the picture is taken and comes back after. All pictures are in physical pixels.

| Setting | Default | What it does |
| --- | --- | --- |
| `Capture.HideDelayMilliseconds` | 150 | How long the bar stays hidden before the picture is taken. |
| `Capture.FullScreenArea` | `CursorMonitor` | `CursorMonitor` captures the monitor under the cursor, `VirtualScreen` captures every monitor in one picture. |
| `Capture.Folder` | `NoMercy Share` under your Pictures folder | Where files are saved. |
| `Capture.FileNamePattern` | `nomercy-share-<UTC yyyyMMddTHHmmssfffZ>` | The file name, sortable, with `.png` added. |

How each button captures:

| Button | How |
| --- | --- |
| Full screen | GDI BitBlt from the screen, of the monitor under the cursor. A click on the bar puts the cursor on the bar's monitor, and BitBlt is exact for one still picture. |
| Window | The window that was active before the bar, else the window under the cursor. PrintWindow with PW_RENDERFULLCONTENT, cropped to the DWM frame (no invisible border); BitBlt of the frame if PrintWindow fails. Works while other windows cover it. |
| Region | One BitBlt freezes the virtual screen and one overlay per monitor shows it dimmed outside the selection. Each monitor keeps its own display scale, so a selection stays on one monitor. |

## Hotkeys

| Setting | Default |
| --- | --- |
| `Hotkeys.Region` | `Win+Shift+S` |
| `Hotkeys.FullScreen` | `PrintScreen` |
| `Hotkeys.Window` | `Alt+PrintScreen` |
| `Hotkeys.Record` | `Shift+PrintScreen` |

An empty value turns that hotkey off. With `Win+Shift+S` set, NoMercy Share opens its own region capture instead of the Snipping Tool; clear the Region hotkey to give it back to Windows.

## Recording

| Setting | Default | What it does |
| --- | --- | --- |
| `Recording.FfmpegPath` | blank | The ffmpeg file to run. Blank looks for ffmpeg on the PATH. |
| `Recording.MaxSizeMegabytes` | 50 | The largest recording that gets shared. A longer clip is squeezed harder. |
| `Recording.FrameRate` | 30 | Frames per second. |
| `Recording.MaxWidth`, `Recording.MaxHeight` | 1920, 1080 | The largest video size. |
| `Recording.Audio.SystemSound`, `Recording.Audio.Microphone` | off, off | Which sound goes into a recording. |
| `Recording.AudioBitrateKbps` | 128 | The audio bitrate, taken off the size budget when a recording has sound. |
| `Recording.SizeHeadroom` | 0.95 | The part of the size limit the video may use. |
| `Recording.TargetBitsPerPixel` | 0.05 | The most bits spent on one pixel of one frame. |
| `Recording.MaxCompressAttempts` | 2 | How many more times a video that is still over the limit is encoded. |
| `Recording.StopTimeoutSeconds`, `Recording.CompressTimeoutSeconds` | 15, 600 | How long ffmpeg gets to close the file after the stop, and to compress it. |
| `Recording.FirstFrameWaitMilliseconds`, `Recording.StartupCheckMilliseconds` | 3000, 1000 | How long a recording with sound waits for the first video frame, and how long ffmpeg is watched after the start. |

## Web page capture

The Web page button runs Edge (else Chrome) headless with its own empty profile over the DevTools protocol, and stops it after each capture. Pictures are made at 2 pixels per CSS pixel.

| Setting (`WebCapture`) | Default | What it does |
| --- | --- | --- |
| `BrowserPath` | blank | The browser file; blank finds Edge, then Chrome, in their standard folders. |
| `DesktopWidth`, `PhoneWidth` | 1280, 390 | The page width in CSS pixels. |
| `Height` | 800 | The height of the visible area. |
| `ScaleFactor` | 2 | Picture pixels per CSS pixel. |
| `LoadTimeoutSeconds` | 30 | How long a page gets to load. |
| `SettleMilliseconds` | 500 | The wait after the load, for late layout. |
| `MaxFullPageHeight` | 16384 | A taller page or element is cut off here. |

## Recent links

| Setting | Default | What it does |
| --- | --- | --- |
| `Recent.MaxEntries` | 50 | How many links the list keeps. |
| `Recent.KeepExpiredDays` | 7 | How many days an expired link stays in the list. |

## Sharing and expiry

The storage options (`Storage.*`, `Discord.*`, `Expiry.DefaultDays`, `Clipboard.Content`) are on [Storage hosts](storage-hosts.md).
