// Every user-visible string of the host, in English, in one place.

const plural = (count: number, unit: string) => `${String(count)} ${unit}${count === 1
  ? ''
  : 's'}`;

export const strings = {
  siteName: 'NoMercy',
  brand: 'Shared via NoMercy',
  openOriginal: 'Open original',
  download: 'Download',
  fileTitle: 'Shared file',
  homeTitle: 'NoMercy Share',
  homeBody: 'People share screenshots and clips with NoMercy Share. Open a link someone sent you to see it here.',
  expiredTitle: 'This link has expired',
  expiredBody: 'The person who shared it set a time limit, and the time is up.',
  notFoundTitle: 'Nothing here',
  notFoundBody: 'This link does not lead to a file. Check that it was copied in full.',
  methodNotAllowed: 'Method not allowed',
  expiresSoon: 'Expires in less than an hour',
  expiresInHours: (hours: number) => `Expires in ${plural(hours, 'hour')}`,
  expiresInDays: (days: number) => `Expires in ${plural(days, 'day')}`,
  unsupportedType: 'This file type has no preview. Use Open original or Download.',
} as const;
