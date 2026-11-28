// An https origin, optionally with "*." for every subdomain. Nothing else may reach the page or its policy.
const ORIGIN_PATTERN = /^https:\/\/(?:\*\.)?[a-z0-9-]+(?:\.[a-z0-9-]+)+(?::\d+)?$/;

export function readEmbedOrigins(setting: string) {
  return setting.split(/\s+/).filter((entry) => ORIGIN_PATTERN.test(entry));
}
