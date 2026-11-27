import { elementClip, fullPageClip, type Clip } from './clip';
import { DEBUGGER_PROTOCOL } from './config';
import { dataUrlToBase64 } from './dataUrl';
import { cancelPick, pickElement, type PickedElement } from './picker';
import { STRINGS } from './strings';

interface LayoutMetrics {
  cssContentSize?: { width: number; height: number };
}

interface ScreenshotResult {
  data?: string;
}

/** The visible part of the tab, as base64 PNG. */
export async function captureVisible(windowId: number): Promise<string> {
  const dataUrl = await chrome.tabs.captureVisibleTab(windowId, { format: 'png' });

  return dataUrlToBase64(dataUrl);
}

/** Attaches the debugger for one job and always lets go of it, so the browser's debugging bar goes away. */
async function withDebugger<T>(tabId: number, job: () => Promise<T>): Promise<T> {
  await chrome.debugger.attach({ tabId }, DEBUGGER_PROTOCOL);

  try {
    return await job();
  } finally {
    // The tab may be gone already; there is then nothing left to detach from.
    await chrome.debugger.detach({ tabId }).catch(() => undefined);
  }
}

async function screenshot(tabId: number, clip: Clip): Promise<string> {
  const params = {
    format: 'png',
    captureBeyondViewport: true,
    fromSurface: true,
    clip,
  };
  const result = await chrome.debugger.sendCommand<typeof params, ScreenshotResult>(
    { tabId },
    'Page.captureScreenshot',
    params,
  );

  if (!result?.data) {
    throw new Error(STRINGS.noScreenshot);
  }

  return result.data;
}

/** The whole page, as base64 PNG, cut at the height cap. */
export function captureFullPage(tabId: number): Promise<string> {
  return withDebugger(tabId, async () => {
    const metrics = await chrome.debugger.sendCommand<object, LayoutMetrics>(
      { tabId },
      'Page.getLayoutMetrics',
    );
    const clip = fullPageClip(metrics?.cssContentSize ?? { width: 0, height: 0 });

    if (clip === null) {
      throw new Error(STRINGS.emptyPage);
    }

    return screenshot(tabId, clip);
  });
}

/** One element the user picked, as base64 PNG. */
export function captureElement(tabId: number, picked: PickedElement): Promise<string> {
  const scroll = { x: picked.scrollX, y: picked.scrollY };
  const clip = elementClip(picked, scroll, picked.devicePixelRatio);

  if (clip === null) {
    return Promise.reject(new Error(STRINGS.emptyElement));
  }

  return withDebugger(tabId, () =>
    screenshot(tabId, clip));
}

/** Lets the user pick an element on the page; null when they cancel. */
export async function pickOnPage(tabId: number): Promise<PickedElement | null> {
  const target = { tabId };
  const [injection] = await chrome.scripting.executeScript({ target, func: pickElement });

  return injection?.result ?? null;
}

/** Closes the picker on the page, so its pick answers null. */
export async function cancelPickOnPage(tabId: number): Promise<void> {
  await chrome.scripting.executeScript({ target: { tabId }, func: cancelPick });
}
