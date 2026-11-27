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

## Settings

| Setting | Default | What it does |
| --- | --- | --- |
| `RAW_MAX_AGE_SECONDS` (wrangler.toml `[vars]`) | `3600` | The `max-age` of the raw file. |

## Develop

```sh
npm ci
npm run check     # type check, strict lint, tests
npm run deploy    # wrangler deploy
```

The deploy reads `CLOUDFLARE_ACCOUNT_ID`, `CLOUDFLARE_EMAIL` and `CLOUDFLARE_API_KEY` from the environment. The Worker route `share.nomercy.tv/*` runs in front of the R2 custom domain of the same name.

The router tests run in Node with a fake bucket, not in the Workers runtime.
