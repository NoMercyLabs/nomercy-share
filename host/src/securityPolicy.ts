export const PAGE_SECURITY = 'default-src \'none\'; img-src \'self\'; media-src \'self\'; style-src \'unsafe-inline\'; base-uri \'none\'; form-action \'none\'';

/** The video page also runs this host's player script, and only the listed origins may frame it. */
export function videoPageSecurity(embedOrigins: readonly string[]) {
  const frames = ['\'self\'', ...embedOrigins].join(' ');

  return `${PAGE_SECURITY}; script-src 'self'; connect-src 'self'; frame-ancestors ${frames}`;
}
