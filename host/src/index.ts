import { handle } from './router.js';
import type { ShareBucket } from './storage.js';

interface Env {
  BUCKET: ShareBucket;
  RAW_MAX_AGE_SECONDS?: string;
  EMBED_ORIGINS?: string;
}

const DEFAULT_RAW_MAX_AGE_SECONDS = 3600;

function maxAgeOf(setting: string | undefined) {
  const seconds = Number.parseInt(setting ?? '', 10);

  return Number.isNaN(seconds) || seconds < 0
    ? DEFAULT_RAW_MAX_AGE_SECONDS
    : seconds;
}

export default {
  fetch: (request: Request, env: Env) =>
    handle(request, {
      bucket: env.BUCKET,
      now: () => new Date(),
      rawMaxAgeSeconds: maxAgeOf(env.RAW_MAX_AGE_SECONDS),
      embedOrigins: env.EMBED_ORIGINS ?? '',
    }),
};
