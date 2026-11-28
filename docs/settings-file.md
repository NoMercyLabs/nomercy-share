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

On the bar, **Screenshot** shows Area, Window and Full screen in the row. **Record** shows the same three targets (Area picked at first), the two sound switches and Start.

How each recording target works. A recording reads one monitor, so a rectangle that crosses monitors is cut at the edge of the one it covers most.

| Record target | How |
| --- | --- |
| Area | You drag it on a frozen picture of the screen after Start. |
| Window | The visible frame of the window a window screenshot would take, as it is at Start. The bar hides while it picks. The recording does not follow the window. |
| Full screen | The monitor under the cursor. `Capture.FullScreenArea` does not change it. |

The bar, its popups, its tooltips and the recording border are excluded from every screenshot and recording (`WDA_EXCLUDEFROMCAPTURE`, Windows 10 version 2004 or later). You still see them. The one exception is `Recording.ShowTool`, which lets the bar, its popups and its tooltips into a running recording.

While a recording runs, a border 3 units thick shows 2 units outside the recorded rectangle, in the theme's accent color, scaled with the monitor it is on. A side with no room on that monitor lies just inside the rectangle; such a side shows only when the exclusion worked. These sizes are not settings.

## Hotkeys

| Setting | Default |
| --- | --- |
| `Hotkeys.Bar` (shows the bar and repeats the last action) | `Win+Shift+S` |
| `Hotkeys.Region` | `Ctrl+PrintScreen` |
| `Hotkeys.FullScreen` | `PrintScreen` |
| `Hotkeys.Window` | `Alt+PrintScreen` |
| `Hotkeys.Record` | `Shift+PrintScreen` |

The Region, FullScreen and Window hotkeys capture at once. The Record hotkey switches the bar to record mode (a hidden bar shows) and stops a running recording.

`Bar.LastAction` is what the Bar hotkey starts again; the app writes it each time you start a capture. `Task` is `Screenshot` (the default), `Recording` or `WebPage`. `Source` is `Region` (the default), `Window` or `FullScreen`.

An empty value turns that hotkey off. With `Win+Shift+S` set, NoMercy Share shows its bar instead of the Snipping Tool; clear the Bar hotkey to give it back to Windows.

`Bar.StartAtSignIn` (default on) starts NoMercy Share hidden when you sign in to Windows, so the hotkeys always work. Start NoMercy Share again to show the bar. The bar's cross hides it; **Quit NoMercy Share** in Settings ends the app. Off removes the sign-in start the next time the app starts.

## Recording

| Setting | Default | What it does |
| --- | --- | --- |
| `Recording.FfmpegPath` | blank | The ffmpeg file to run. Blank uses NoMercy FFmpeg (the media server's copy, then the one Share downloaded), then ffmpeg on the PATH. When none is found, the record button offers to download the latest NoMercy FFmpeg release (about 300 MB) to `%LOCALAPPDATA%\NoMercy Share\ffmpeg`. The download must match the release checksum, or it is thrown away. |
| `Recording.MaxSizeMegabytes` | 50 | The largest recording that gets shared. A longer clip is squeezed harder. |
| `Recording.FrameRate` | 30 | Frames per second. |
| `Recording.MaxWidth`, `Recording.MaxHeight` | 1920, 1080 | The largest video size. 0 and 0 keep the screen's own size. Settings offers 720p, 1080p, 1440p, 4K and the original size; another size you type here is kept. |
| `Recording.Codec` | `h264` | The video codec: `h264`, `vp9`, `hevc` or `av1`. H.264 plays everywhere (MP4). VP9 encodes twice to fit the most picture into the size limit (WebM with Opus sound, CPU only), best for Discord. HEVC and AV1 look sharper at the same size, but not every browser or app plays them. |
| `Recording.Encoder` | `cpu` | What encodes the video: `cpu`, `nvidia`, `intel` or `amd`. Settings lists only the ones that pass a test encode on your PC. If a GPU encode fails, the recording is encoded again on the CPU. |
| `Recording.Speed` | `fast` | `fast`, `balanced` or `quality`. A slower encode gives a sharper picture at the same file size. |
| `Recording.Audio.SystemSound`, `Recording.Audio.Microphone` | off, off | Which sound goes into a recording. The Desktop audio and Microphone switches on the bar save these same settings. |
| `Recording.ShowTool` | off | Shows the bar, its popups and its tooltips in a recording. The **Show bar in video** switch in the Settings window, under Recording, saves this setting. Screenshots never contain them. |
| `Recording.AudioBitrateKbps` | 128 | The highest audio bitrate, taken off the size budget when a recording has sound. A tight budget steps it down: 128 kbps from 760 kbps of total budget, 96 from 520, 64 below that. |
| `Recording.LowBudgetMinHeight`, `Recording.LowBudgetFrameRate` | 480, 15 | When the budget would shrink the picture below this height, the frame rate drops to this rate first, so the picture stays sharper. |
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
| `PhoneWidth`, `TabletWidth` | 390, 834 | The page width of **Phone** and **Tablet**, in CSS pixels. Both load the page as a touch device. |
| `DesktopWidth` | 1280 | The page width of **Desktop**, the choice the window opens with. |
| `FullHdWidth`, `QuadHdWidth`, `UltraHdWidth` | 1920, 2560, 3840 | The page width of **1080p**, **1440p** and **4K**. |
| `Height` | 800 | The height of the visible area, at every width. |
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
