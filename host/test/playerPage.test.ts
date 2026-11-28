import { describe, expect, it } from 'vitest';
import { PLAYER_SCRIPT_URL } from '../src/playerAssets.js';
import { handle } from '../src/router.js';
import type { ShareBucket } from '../src/storage.js';
import { fakeBucket, textFile } from './fakeBucket.js';

const NOW = new Date('2026-11-27T12:00:00Z');
const ORIGIN = 'https://share.example.test';
const BYTES = new Uint8Array([0, 0, 0, 24, 102, 116, 121, 112, 1, 2]);
const ORIGINAL_SECURITY = 'default-src \'none\'; img-src \'self\'; media-src \'self\'; style-src \'unsafe-inline\'; base-uri \'none\'; form-action \'none\'';

const touched: string[] = [];
const inner = fakeBucket({
  'keep/clip.mp4': { bytes: BYTES, contentType: 'video/mp4' },
  'keep/clip.mp4.json': textFile(JSON.stringify({ ExpiresAt: 'never' })),
  'exp-7d/gone.mp4': { bytes: BYTES, contentType: 'video/mp4' },
  'exp-7d/gone.mp4.json': textFile(JSON.stringify({ ExpiresAt: '2026-11-20T12:00:00Z' })),
  'keep/shot.png': { bytes: BYTES, contentType: 'image/png' },
  'keep/song.mp3': { bytes: BYTES, contentType: 'audio/mpeg' },
  'keep/data.zip': { bytes: BYTES, contentType: 'application/zip' },
});
const bucket = {
  head: (key) => {
    touched.push(key);

    return inner.head(key);
  },
  get: (key, options) => {
    touched.push(key);

    return inner.get(key, options);
  },
} as const satisfies ShareBucket;

function get(path: string, headers: Record<string, string> = { accept: 'text/html' }, embedOrigins = 'https://example.org https://*.example.org') {
  return handle(new Request(`${ORIGIN}${path}`, { headers }), {
    bucket,
    now: () => NOW,
    rawMaxAgeSeconds: 3600,
    embedOrigins,
  });
}

const policyOf = (response: Response) => response.headers.get('content-security-policy') ?? '';

describe('the video page', () => {
  it('loads the player from this host and keeps a plain video as the fallback', async () => {
    const html = await (await get('/keep/clip.mp4')).text();

    expect(PLAYER_SCRIPT_URL).toMatch(/^\/_player\/player\.js\?v=\d+\.\d+\.\d+$/);
    expect(html).toContain(`<script type="module" src="${PLAYER_SCRIPT_URL}"></script>`);
    expect(html).toContain(`<video class="media" src="${ORIGIN}/keep/clip.mp4?raw=1" controls playsinline preload="metadata"></video>`);
    expect(html).toContain(`<div id="share-player" class="player" data-source="${ORIGIN}/keep/clip.mp4?raw=1" data-title="Shared file" data-embed-origins="https://example.org https://*.example.org"></div>`);
  });

  it('allows only its own scripts, no eval and no remote script origin', async () => {
    const policy = policyOf(await get('/keep/clip.mp4'));
    const scripts = policy.split(';').map((part) => part.trim()).find((part) => part.startsWith('script-src'));

    expect(policy).toContain('default-src \'none\'');
    expect(policy).not.toContain('unsafe-eval');
    expect(scripts).toBe('script-src \'self\'');
  });

  it('lets only this host and the configured origins frame it', async () => {
    expect(policyOf(await get('/keep/clip.mp4'))).toContain('frame-ancestors \'self\' https://example.org https://*.example.org');
  });

  it('drops a setting entry that is not an https origin', async () => {
    const response = await get('/keep/clip.mp4', { accept: 'text/html' }, '* http://a.example.org https://bad.example.org;script-src https://ok.example.org');

    expect(policyOf(response)).toContain('frame-ancestors \'self\' https://ok.example.org');
    expect(policyOf(response)).not.toContain('*');
    expect(await response.text()).toContain('data-embed-origins="https://ok.example.org"');
  });

  it.each(['/keep/shot.png', '/keep/song.mp3', '/keep/data.zip'])('leaves %s as it was', async (path) => {
    const response = await get(path);

    expect(policyOf(response)).toBe(ORIGINAL_SECURITY);
    expect(await response.text()).not.toContain('<script');
  });

  it('answers 410 without a player for an expired video', async () => {
    const response = await get('/exp-7d/gone.mp4');

    expect(response.status).toBe(410);
    expect(await response.text()).not.toContain('<script');
  });
});

describe('what a video share still does', () => {
  it('keeps the Open Graph and Twitter tags', async () => {
    const html = await (await get('/keep/clip.mp4')).text();
    const tags = html.split('\n').filter((line) => line.startsWith('<meta property=') || line.startsWith('<meta name="twitter:'));

    expect(tags).toEqual([
      '<meta property="og:title" content="Shared file">',
      '<meta property="og:type" content="website">',
      '<meta property="og:site_name" content="NoMercy">',
      `<meta property="og:url" content="${ORIGIN}/keep/clip.mp4">`,
      `<meta property="og:video" content="${ORIGIN}/keep/clip.mp4?raw=1">`,
      '<meta property="og:video:type" content="video/mp4">',
      '<meta name="twitter:card" content="player">',
    ]);
  });

  it('gives a preview bot the viewer page', async () => {
    const response = await get('/keep/clip.mp4', { 'user-agent': 'Discordbot/2.0', accept: '*/*' });

    expect(await response.text()).toContain('<meta property="og:video"');
  });

  it('answers ?raw=1 with the file and the same headers', async () => {
    const response = await get('/keep/clip.mp4?raw=1');

    expect([...response.headers]).toEqual([
      ['accept-ranges', 'bytes'],
      ['cache-control', 'public, max-age=3600'],
      ['content-length', '10'],
      ['content-type', 'video/mp4'],
      ['etag', '"etag-keep/clip.mp4"'],
      ['x-content-type-options', 'nosniff'],
    ]);

    expect(new Uint8Array(await response.arrayBuffer())).toEqual(BYTES);
  });

  it('answers a range request with the slice', async () => {
    const response = await get('/keep/clip.mp4?raw=1', { range: 'bytes=4-7' });

    expect(response.status).toBe(206);
    expect(response.headers.get('content-range')).toBe('bytes 4-7/10');
    expect(new Uint8Array(await response.arrayBuffer())).toEqual(BYTES.slice(4, 8));
  });

  it.each(['/_player/player.js', '/_player/missing.js', '/keep/a/b.mp4', '/other/clip.mp4', '/keep/clip.mp4.json'])('never reaches the bucket for %s', async (path) => {
    touched.length = 0;

    expect((await get(path)).status).toBe(404);
    expect(touched).toEqual([]);
  });
});
