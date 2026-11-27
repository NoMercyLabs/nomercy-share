import { isShareMode, SHARE_MODES, type ShareMode } from './modes';
import { share, type ShareResult } from './share';
import { STRINGS } from './strings';

const MENU_PREFIX = 'share-';

const MENU_TITLES = {
  fullPage: STRINGS.fullPage,
  visible: STRINGS.visible,
  element: STRINGS.element,
} as const satisfies Record<ShareMode, string>;

/** Shows a result on the toolbar button, for a share started where no popup is open to show it. */
async function announce(tabId: number, result: ShareResult): Promise<void> {
  const failed = result.kind === 'error';

  await chrome.action.setBadgeBackgroundColor({ tabId, color: failed
    ? '#FF9592'
    : '#0BD8B6' });

  await chrome.action.setBadgeText({ tabId, text: failed
    ? '!'
    : '✓' });

  await chrome.action.setTitle({ tabId, title: result.text });
}

interface ShareRequest {
  mode: ShareMode;
  tabId: number;
}

/** What the popup sends; the browser hands messages over untyped, so every field may be missing. */
interface ShareMessage {
  type?: string;
  mode?: string;
  tabId?: number;
}

function readRequest(message: ShareMessage | null | undefined): ShareRequest | null {
  if (message?.type !== 'share' || typeof message.tabId !== 'number' || !isShareMode(message.mode)) {
    return null;
  }

  return { mode: message.mode, tabId: message.tabId };
}

type Incoming = ShareMessage | null | undefined;

chrome.runtime.onMessage.addListener((message: Incoming, _sender, sendResponse) => {
  const request = readRequest(message);

  if (request === null) {
    return false;
  }

  void share(request.mode, request.tabId).then(async (result) => {
    sendResponse(result);

    // The popup closes when the page is clicked to pick an element, so the toolbar button carries the result.
    if (request.mode === 'element') {
      await announce(request.tabId, result);
    }
  });

  return true;
});

chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.removeAll(() => {
    for (const mode of SHARE_MODES) {
      chrome.contextMenus.create({
        id: `${MENU_PREFIX}${mode}`,
        title: MENU_TITLES[mode],
        contexts: ['action'],
      });
    }
  });
});

chrome.contextMenus.onClicked.addListener((info, tab) => {
  const mode = String(info.menuItemId).slice(MENU_PREFIX.length);

  if (tab?.id === undefined || !isShareMode(mode)) {
    return;
  }

  const tabId = tab.id;

  void share(mode, tabId).then((result) => announce(tabId, result));
});
