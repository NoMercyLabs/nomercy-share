// The part of an R2 bucket the Worker uses. The real R2Bucket binding satisfies it; tests pass a fake.

export interface StoredHead {
  httpEtag: string;
  size: number;
  httpMetadata?: { contentType?: string };
}

export interface StoredObject extends StoredHead {
  body: ReadableStream<Uint8Array> | null;
  range?: { offset?: number; length?: number };
  text: () => Promise<string>;
}

export interface ShareBucket {
  head: (key: string) => Promise<StoredHead | null>;
  get: (key: string, options?: { range?: Headers }) => Promise<StoredObject | null>;
}
