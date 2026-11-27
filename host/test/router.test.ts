import { describe, expect, it } from 'vitest';
import { handle } from '../src/router.js';
import { fakeBucket, textFile } from './fakeBucket.js';

const NOW = new Date('2026-11-27T12:00:00Z');
const PICTURE = new Uint8Array([137, 80, 78, 71, 1, 2, 3, 4, 5, 6]);
const ORIGIN = 'https://share.example.test';

const sidecar = (key: string, expiresAt: string) =>
  textFile(JSON.stringify({
    Key: key,
    UploadedAt: '2026-10-28T12:00:00Z',
    ExpiresAt: expiresAt,
    Bytes: 10,
  }));

const bucket = fakeBucket({
  'exp-30d/live.png': { bytes: PICTURE, contentType: 'image/png' },
  'exp-30d/live.png.json': sidecar('exp-30d/live.png', '2026-12-26T12:00:00Z'),
  'exp-7d/gone.png': { bytes: PICTURE, contentType: 'image/png' },
  'exp-7d/gone.png.json': sidecar('exp-7d/gone.png', '2026-11-20T12:00:00Z'),
  'keep/clip.mp4': { bytes: PICTURE, contentType: 'video/mp4' },
  'keep/clip.mp4.json': sidecar('keep/clip.mp4', 'never'),
  'keep/old.png': { bytes: PICTURE, contentType: 'image/png' },
});

const env = {
  bucket,
  now: () => NOW,
  rawMaxAgeSeconds: 3600,
};

function get(path: string, accept?: string, method = 'GET') {
  const headers = new Headers();

  if (accept) {
    headers.set('accept', accept);
  }

  return handle(new Request(`${ORIGIN}${path}`, { method, headers }), env);
}

describe('html or raw', () => {
  it('serves the viewer to a browser that asks for html', async () => {
    const response = await get('/exp-30d/live.png', 'text/html,application/xhtml+xml,*/*;q=0.8');

    expect(response.status).toBe(200);
    expect(response.headers.get('content-type')).toContain('text/html');
  });

  it('serves the raw file with no Accept header', async () => {
    const response = await get('/exp-30d/live.png');

    expect(response.status).toBe(200);
    expect(response.headers.get('content-type')).toBe('image/png');
    expect(new Uint8Array(await response.arrayBuffer())).toEqual(PICTURE);
  });

  it('serves the raw file to an image request', async () => {
    const response = await get('/exp-30d/live.png', 'image/avif,image/webp,*/*;q=0.8');

    expect(response.headers.get('content-type')).toBe('image/png');
  });

  it('serves the raw file for ?raw=1 even when html is preferred', async () => {
    const response = await get('/exp-30d/live.png?raw=1', 'text/html');

    expect(response.headers.get('content-type')).toBe('image/png');
  });

  it('sends the ETag and the cache header with the raw file', async () => {
    const response = await get('/exp-30d/live.png');

    expect(response.headers.get('etag')).toBe('"etag-exp-30d/live.png"');
    expect(response.headers.get('cache-control')).toBe('public, max-age=3600');
  });

  it('answers a range request with 206 and the slice', async () => {
    const headers = new Headers({ range: 'bytes=2-4' });
    const response = await handle(new Request(`${ORIGIN}/exp-30d/live.png`, { headers }), env);

    expect(response.status).toBe(206);
    expect(response.headers.get('content-range')).toBe('bytes 2-4/10');
    expect(new Uint8Array(await response.arrayBuffer())).toEqual(PICTURE.slice(2, 5));
  });
});

describe('expired, missing and hidden keys', () => {
  it('answers 410 with a branded page for an expired link', async () => {
    const response = await get('/exp-7d/gone.png', 'text/html');

    expect(response.status).toBe(410);
    expect(await response.text()).toContain('expired');
  });

  it('answers 410 for a raw request of an expired link', async () => {
    expect((await get('/exp-7d/gone.png')).status).toBe(410);
    expect((await get('/exp-7d/gone.png?raw=1')).status).toBe(410);
  });

  it('answers 404 with a branded page for a missing key', async () => {
    const response = await get('/exp-30d/nothing.png', 'text/html');

    expect(response.status).toBe(404);
    expect(await response.text()).toContain('NoMercy');
  });

  it('never serves a sidecar', async () => {
    expect((await get('/exp-30d/live.png.json')).status).toBe(404);
    expect((await get('/exp-30d/live.png.json', 'text/html')).status).toBe(404);
    expect((await get('/exp-30d/live.png.json?raw=1')).status).toBe(404);
  });

  it('answers 404 for a path outside the share folders', async () => {
    expect((await get('/etc/passwd')).status).toBe(404);
    expect((await get('/exp-30d/a/b.png')).status).toBe(404);
    expect((await get('/exp-30d/')).status).toBe(404);
    expect((await get('/exp-30d%2Flive.png%00')).status).toBe(404);
  });

  it('serves an object that has no sidecar', async () => {
    expect((await get('/keep/old.png')).status).toBe(200);
  });

  it('serves a keep file with a never sidecar', async () => {
    expect((await get('/keep/clip.mp4')).status).toBe(200);
  });

  it('answers 405 for a method that is not GET or HEAD', async () => {
    expect((await get('/exp-30d/live.png', undefined, 'POST')).status).toBe(405);
  });

  it('answers HEAD without a body', async () => {
    const response = await get('/exp-30d/live.png', undefined, 'HEAD');

    expect(response.status).toBe(200);
    expect(await response.text()).toBe('');
  });
});

describe('the home page', () => {
  it('says what this is', async () => {
    const response = await get('/', 'text/html');

    expect(response.status).toBe(200);
    expect(await response.text()).toContain('Shared via NoMercy');
  });
});

describe('the viewer page', () => {
  it('holds the Open Graph and Twitter tags with the raw URL', async () => {
    const html = await (await get('/exp-30d/live.png', 'text/html')).text();
    const raw = `${ORIGIN}/exp-30d/live.png?raw=1`;

    expect(html).toContain(`<meta property="og:image" content="${raw}">`);
    expect(html).toContain('<meta property="og:site_name" content="NoMercy">');
    expect(html).toContain('<meta name="twitter:card" content="summary_large_image">');
    expect(html).toContain(`<img class="media" src="${raw}"`);
    expect(html).toContain(`href="${raw}"`);
  });

  it('shows a video element and og:video for a video', async () => {
    const html = await (await get('/keep/clip.mp4', 'text/html')).text();

    expect(html).toContain('<video class="media"');
    expect(html).toContain(`<meta property="og:video" content="${ORIGIN}/keep/clip.mp4?raw=1">`);
    expect(html).not.toContain('<img class="media"');
  });

  it('says in how many days the link expires', async () => {
    const html = await (await get('/exp-30d/live.png', 'text/html')).text();

    expect(html).toContain('Expires in 29 days');
  });

  it('shows no expiry line for a keep file', async () => {
    const html = await (await get('/keep/clip.mp4', 'text/html')).text();

    expect(html).not.toContain('Expires');
  });

  it('loads nothing from another origin and sets no cookie', async () => {
    const response = await get('/exp-30d/live.png', 'text/html');

    expect(response.headers.get('set-cookie')).toBeNull();
    expect(response.headers.get('content-security-policy')).toContain('default-src \'none\'');
    expect(await response.text()).not.toMatch(/<script|<link|src="https?:\/\/(?!share\.example\.test)/);
  });
});
