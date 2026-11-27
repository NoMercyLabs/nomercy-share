import { STRINGS } from './strings';

const BROWSER_SCHEMES = new Set(['chrome:', 'edge:', 'about:', 'chrome-extension:', 'edge-extension:', 'devtools:', 'view-source:']);

/** The stores sit on normal https addresses but the browser blocks scripts and debugging there. */
function isStore(address: URL): boolean {
  return (
    address.hostname === 'chromewebstore.google.com' ||
    (address.hostname === 'chrome.google.com' && address.pathname.startsWith('/webstore')) ||
    (address.hostname === 'microsoftedge.microsoft.com' && address.pathname.startsWith('/addons'))
  );
}

/** The reason a tab cannot be captured, or null when it can. */
export function refusalFor(url: string | undefined): string | null {
  if (url === undefined || url === '') {
    return STRINGS.noPage;
  }

  let address: URL;

  try {
    address = new URL(url);
  } catch {
    return STRINGS.otherPage;
  }

  if (BROWSER_SCHEMES.has(address.protocol)) {
    return STRINGS.browserPage;
  }

  if (address.protocol === 'file:') {
    return STRINGS.localFile;
  }

  if (address.protocol !== 'http:' && address.protocol !== 'https:') {
    return STRINGS.otherPage;
  }

  return isStore(address)
    ? STRINGS.store
    : null;
}
