import { homePage, noticePage, viewerPage } from './pages.js';
import type { MediaKind } from './pages.js';
import { readExpiry, expiryLine } from './expiry.js';
import type { ShareBucket, StoredObject } from './storage.js';
import { strings } from './strings.js';

export interface RouterEnv {
  bucket: ShareBucket;
  now: () => Date;
  rawMaxAgeSeconds: number;
}

// A share key is the expiry folder, a random id and an optional extension, as the app writes it.
const KEY_PATTERN = /^(?:exp-\d+d|keep)\/[A-Za-z0-9]+(?:\.[a-z0-9]{1,8})?$/;
const SIDECAR_SUFFIX = '.json';
const HTML = 'text/html; charset=utf-8';
const PAGE_SECURITY = 'default-src \'none\'; img-src \'self\'; media-src \'self\'; style-src \'unsafe-inline\'; base-uri \'none\'; form-action \'none\'';

function prefersHtml(accept: string | null) {
  const parts = (accept ?? '').split(',');

  return parts.some((part) => {
    const [type, ...parameters] = part.trim().split(';');
    const refused = parameters.some((parameter) => /^\s*q=0(?:\.0*)?\s*$/.test(parameter));

    return type?.trim().toLowerCase() === 'text/html' && !refused;
  });
}

function page(status: number, html: string) {
  const headers = {
    'content-type': HTML,
    'content-security-policy': PAGE_SECURITY,
    'cache-control': 'no-cache',
    'x-content-type-options': 'nosniff',
    'referrer-policy': 'no-referrer',
  };

  return new Response(html, { status, headers });
}

function kindOf(contentType: string): MediaKind {
  if (contentType.startsWith('image/')) {
    return 'image';
  }

  if (contentType.startsWith('video/')) {
    return 'video';
  }

  return contentType.startsWith('audio/')
    ? 'audio'
    : 'file';
}

function rawResponse(object: StoredObject, wasRanged: boolean, maxAge: number) {
  const headers = new Headers({
    'content-type': object.httpMetadata?.contentType ?? 'application/octet-stream',
    etag: object.httpEtag,
    'cache-control': `public, max-age=${String(maxAge)}`,
    'accept-ranges': 'bytes',
    'x-content-type-options': 'nosniff',
  });
  const range = wasRanged
    ? object.range
    : undefined;

  if (!range) {
    headers.set('content-length', String(object.size));

    return new Response(object.body, { status: 200, headers });
  }

  const offset = range.offset ?? 0;
  const length = range.length ?? object.size - offset;

  headers.set('content-length', String(length));
  headers.set('content-range', `bytes ${String(offset)}-${String(offset + length - 1)}/${String(object.size)}`);

  return new Response(object.body, { status: 206, headers });
}

async function expiryOf(env: RouterEnv, key: string) {
  const sidecar = await env.bucket.get(key + SIDECAR_SUFFIX);

  return sidecar
    ? readExpiry(await sidecar.text())
    : null;
}

async function serve(request: Request, env: RouterEnv, key: string) {
  const expiresAt = await expiryOf(env, key);

  if (expiresAt && expiresAt.getTime() <= env.now().getTime()) {
    return page(410, noticePage(strings.expiredTitle, strings.expiredBody));
  }

  const url = new URL(request.url);
  const wantsViewer = prefersHtml(request.headers.get('accept')) && url.searchParams.get('raw') !== '1';

  if (wantsViewer) {
    const head = await env.bucket.head(key);

    if (!head) {
      return notFound();
    }

    const rawUrl = `${url.origin}/${key}?raw=1`;
    const kind = kindOf(head.httpMetadata?.contentType ?? '');
    const line = expiresAt
      ? expiryLine(expiresAt, env.now())
      : null;

    return page(200, viewerPage({
      kind,
      rawUrl,
      pageUrl: `${url.origin}/${key}`,
      expiryLine: line,
    }));
  }

  const wasRanged = request.headers.has('range');
  const object = await env.bucket.get(key, wasRanged
    ? { range: request.headers }
    : undefined);

  return object
    ? rawResponse(object, wasRanged, env.rawMaxAgeSeconds)
    : notFound();
}

const notFound = () => page(404, noticePage(strings.notFoundTitle, strings.notFoundBody));

function keyOf(pathname: string) {
  try {
    const key = decodeURIComponent(pathname.slice(1));

    return KEY_PATTERN.test(key)
      ? key
      : null;
  } catch {
    return null;
  }
}

async function route(request: Request, env: RouterEnv) {
  const { pathname } = new URL(request.url);

  if (pathname === '/') {
    return page(200, homePage());
  }

  const key = keyOf(pathname);

  return key
    ? serve(request, env, key)
    : notFound();
}

export async function handle(request: Request, env: RouterEnv) {
  if (request.method !== 'GET' && request.method !== 'HEAD') {
    return new Response(strings.methodNotAllowed, { status: 405, headers: { allow: 'GET, HEAD' } });
  }

  const response = await route(request, env);

  return request.method === 'HEAD'
    ? new Response(null, response)
    : response;
}
