import { STRINGS } from './strings';

const PNG_PREFIX = 'data:image/png;base64,';

/** The base64 payload of a PNG data URL, which is what the app wants. */
export function dataUrlToBase64(dataUrl: string): string {
  if (!dataUrl.startsWith(PNG_PREFIX)) {
    throw new Error(STRINGS.notPng);
  }

  return dataUrl.slice(PNG_PREFIX.length);
}
