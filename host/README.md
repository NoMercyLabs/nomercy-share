# share.nomercy.tv host

A Cloudflare Worker in front of the R2 bucket `nomercy-share`. It keeps every link as the app writes it (`/<prefix>/<id>.<ext>`) and adds a viewer.

## Routing

One GET handler (`src/router.ts`). HEAD works too; other methods answer 405.

| Request | Answer |
| --- | --- |
| `/` | A small page that says what this is. |
| A key outside `exp-<N>d/` or `keep/`, or any `.json` key | 404 page. A sidecar is never served. |
| The sidecar says the link expired | 410, a calm page (a raw request gets 410 too). |
| `Accept` lists `text/html`, no `?raw=1` | The viewer page, with Open Graph and Twitter tags. |
| A link preview bot (Discordbot, Twitterbot, Slackbot-LinkExpanding, TelegramBot, facebookexternalhit) in `User-Agent`, no `?raw=1` | The viewer page, whatever `Accept` says. |
| Anything else, or `?raw=1` | The object, with its content type, ETag, a cache header and byte ranges. |
| No such object | 404 page. |

A missing or unreadable sidecar means the link does not expire.

## Video player

A video share plays in the NoMercy video player; the plain `<video controls>` stays when the player script does not load. Images, audio and other files have no script.

The code is in `client/`. `npm run build:player` bundles it with the pinned npm packages into `public/_player/` (not committed); wrangler runs that before `dev` and `deploy` and serves the files from this host. The player version is the one in `package.json`.

| Keys | Effect |
| --- | --- |
| Space, media play and pause keys | Play or pause |
| Left, Right (with Shift, Alt or Ctrl for other steps), media rewind and forward | Seek |
| `1` `3` `6` `9`, TV color buttons | Jump forward |
| Up, Down, `m` | Volume, mute |
| `f`, F11, Escape | Fullscreen |
| `i` | Picture in picture |
| `[` `]` `=` | Slower, faster, normal speed |

## Embed a share

Every video share that has not expired can be embedded, `keep/` links for good. An expired one answers 410 in the frame too.

```html
<iframe src="https://share.nomercy.tv/keep/<id>.mp4" allow="autoplay; fullscreen; picture-in-picture" allowfullscreen></iframe>
```

Only this host and the origins in `EMBED_ORIGINS` may frame a video share. In a frame the page shows only the player. Only a listed origin can control it.

A parent page:

```html
<iframe id="share" src="https://share.nomercy.tv/keep/<id>.mp4" allow="autoplay; fullscreen; picture-in-picture" allowfullscreen></iframe>
<script>
  const SHARE = 'https://share.nomercy.tv';
  const frame = document.getElementById('share');
  const tell = (action, extra = {}) => frame.contentWindow.postMessage({ type: 'nm:command', action, ...extra }, SHARE);

  addEventListener('message', (event) => {
    if (event.origin !== SHARE || event.data?.type !== 'nm:event') return;
    console.log(event.data.name, event.data.data); // ready, play, pause, ended, time, volume, mute
  });

  // After "ready": tell('play'), tell('pause'), tell('seek', { time: 60 }),
  // tell('volume', { level: 50 }) (0 to 100), tell('mute'), tell('unmute').
</script>
```

A browser may refuse `play` until the viewer has clicked on one of the two pages.

For local runs use `npx wrangler dev --local-upstream 127.0.0.1:8787`. Without it wrangler builds the file addresses from the live route.

## Settings

| Setting | Default | What it does |
| --- | --- | --- |
| `RAW_MAX_AGE_SECONDS` (wrangler.toml `[vars]`) | `3600` | The `max-age` of the raw file. |
| `EMBED_ORIGINS` (wrangler.toml `[vars]`) | `https://nomercy.tv https://*.nomercy.tv` | Space separated https origins that may frame a video share and control it. `*.` means every subdomain. |

## Develop

```sh
npm ci
npm run check     # type check, strict lint, tests
npm run deploy    # wrangler deploy
```

The deploy reads `CLOUDFLARE_ACCOUNT_ID`, `CLOUDFLARE_EMAIL` and `CLOUDFLARE_API_KEY` from the environment. The Worker route `share.nomercy.tv/*` runs in front of the R2 custom domain of the same name.

The router tests run in Node with a fake bucket, not in the Workers runtime.
