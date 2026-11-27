import type { ShareBucket, StoredHead, StoredObject } from '../src/storage.js';

export interface FakeFile {
  bytes: Uint8Array<ArrayBuffer>;
  contentType: string;
}

const encoder = new TextEncoder();

export function textFile(content: string, contentType = 'application/json') {
  return { bytes: encoder.encode(content), contentType } as const satisfies FakeFile;
}

function sliceFor(file: FakeFile, range: Headers | undefined) {
  const match = /^bytes=(\d+)-(\d*)$/.exec(range?.get('range') ?? '');

  if (!match) {
    return undefined;
  }

  const offset = Number(match[1]);
  const end = match[2]
    ? Number(match[2])
    : file.bytes.length - 1;

  return { offset, length: Math.min(end, file.bytes.length - 1) - offset + 1 };
}

export function fakeBucket(files: Record<string, FakeFile>) {
  const head = (key: string): StoredHead | null => {
    const file = files[key];

    if (!file) {
      return null;
    }

    return {
      httpEtag: `"etag-${key}"`,
      size: file.bytes.length,
      httpMetadata: { contentType: file.contentType },
    };
  };

  return {
    head: (key) => Promise.resolve(head(key)),
    get: (key, options) => {
      const file = files[key];
      const info = head(key);

      if (!file || !info) {
        return Promise.resolve(null);
      }

      const range = sliceFor(file, options?.range);
      const bytes = range
        ? file.bytes.slice(range.offset, range.offset + range.length)
        : file.bytes;
      const stored: StoredObject = {
        ...info,
        range,
        body: new Blob([bytes]).stream(),
        text: () => Promise.resolve(new TextDecoder().decode(file.bytes)),
      };

      return Promise.resolve(stored);
    },
  } as const satisfies ShareBucket;
}
