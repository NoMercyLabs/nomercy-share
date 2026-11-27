import { captureElement, captureFullPage, captureVisible, pickOnPage } from './capture';
import { HOST_NAME } from './config';
import type { ShareMode } from './modes';
import { copyInPage } from './picker';
import { describeFailure, describeReply, type NativeReply, type ShareOutcome } from './reply';
import { STRINGS } from './strings';
import { refusalFor } from './urls';

/** What a share ends in: the app's outcome, or nothing when the user cancelled the pick. */
export type ShareResult = ShareOutcome | { kind: 'cancelled'; text: string };

async function sendToApp(
  pngBase64: string,
  pageUrl: string,
  mode: ShareMode,
): Promise<ShareOutcome> {
  const request = {
    type: 'share',
    pngBase64,
    pageUrl,
    mode,
  };

  try {
    const reply: NativeReply | null | undefined =
      await chrome.runtime.sendNativeMessage(HOST_NAME, request);

    return describeReply(reply);
  } catch (error) {
    return describeFailure(error instanceof Error
      ? error.message
      : String(error));
  }
}

/** The popup closes the moment the user clicks the page, so a picked element puts the link on the clipboard from the page itself. */
async function copyFromPage(tabId: number, link: string): Promise<ShareOutcome> {
  const [injection] = await chrome.scripting.executeScript({
    target: { tabId },
    func: copyInPage,
    args: [link],
  });

  return injection?.result === true
    ? {
        kind: 'link',
        link,
        text: STRINGS.linkCopied,
      }
    : {
        kind: 'link',
        link,
        text: `${STRINGS.copyFailed}${link}`,
      };
}

/** Captures the tab in one mode and shares it through the app. */
export async function share(mode: ShareMode, tabId: number): Promise<ShareResult> {
  try {
    const tab = await chrome.tabs.get(tabId);
    const refusal = refusalFor(tab.url);

    if (refusal !== null || tab.url === undefined) {
      return { kind: 'error', text: refusal ?? STRINGS.noPage };
    }

    let pngBase64: string;

    if (mode === 'element') {
      const picked = await pickOnPage(tabId);

      if (picked === null) {
        return { kind: 'cancelled', text: STRINGS.pickCancelled };
      }

      pngBase64 = await captureElement(tabId, picked);
    } else {
      pngBase64 = mode === 'visible'
        ? await captureVisible(tab.windowId)
        : await captureFullPage(tabId);
    }

    const outcome = await sendToApp(pngBase64, tab.url, mode);

    return mode === 'element' && outcome.kind === 'link'
      ? await copyFromPage(tabId, outcome.link)
      : outcome;
  } catch (error) {
    return {
      kind: 'error',
      text: error instanceof Error
        ? error.message
        : String(error),
    };
  }
}
