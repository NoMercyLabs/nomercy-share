import { strings } from './strings.js';

const HOUR_MS = 3_600_000;
const DAY_MS = 24 * HOUR_MS;

// The app writes the sidecar with PascalCase names; a camelCase writer would work too.
// A sidecar that is missing, unreadable or says "never" gives null: the link does not expire.
export function readExpiry(sidecarText: string) {
  try {
    const parsed: Record<string, string | number | null> = JSON.parse(sidecarText);
    const entry = Object.entries(parsed).find(([name]) => name.toLowerCase() === 'expiresat');
    const time = typeof entry?.[1] === 'string'
      ? Date.parse(entry[1])
      : Number.NaN;

    return Number.isNaN(time)
      ? null
      : new Date(time);
  } catch {
    return null;
  }
}

export function expiryLine(expiresAt: Date, now: Date) {
  const left = expiresAt.getTime() - now.getTime();

  if (left < HOUR_MS) {
    return strings.expiresSoon;
  }

  return left < DAY_MS
    ? strings.expiresInHours(Math.ceil(left / HOUR_MS))
    : strings.expiresInDays(Math.ceil(left / DAY_MS));
}
