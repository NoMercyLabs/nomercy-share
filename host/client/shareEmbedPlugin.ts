import { EmbedPlugin } from '@nomercy-entertainment/nomercy-video-player/plugins';

const WILDCARD = 'https://*.';

/** The embed bridge, with "https://*.domain" entries through its documented origin override. */
export class ShareEmbedPlugin extends EmbedPlugin {
  protected override isOriginAllowed(origin: string) {
    return this.allowedOrigins().some((entry) => {
      if (!entry.startsWith(WILDCARD)) {
        return entry === origin;
      }

      return origin.startsWith('https://') && origin.endsWith(`.${entry.slice(WILDCARD.length)}`);
    });
  }
}
