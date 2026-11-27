import { ICONS } from './icons';
import { SHARE_MODES, type ShareMode } from './modes';
import { STRINGS } from './strings';

const SVG_NAMESPACE = 'http://www.w3.org/2000/svg';

const actions = document.getElementById('actions');
const status = document.getElementById('status');

function setStatus(text: string, tone: '' | 'error' | 'done' = ''): void {
  if (status !== null) {
    status.textContent = text;

    status.className = tone === ''
      ? 'status'
      : `status ${tone}`;
  }
}

function icon(mode: ShareMode): SVGSVGElement {
  const svg = document.createElementNS(SVG_NAMESPACE, 'svg');

  svg.setAttribute('viewBox', '0 0 24 24');
  svg.setAttribute('aria-hidden', 'true');
  const path = document.createElementNS(SVG_NAMESPACE, 'path');

  path.setAttribute('d', ICONS[mode]);
  svg.append(path);

  return svg;
}

/** What the background answers; the browser hands it over untyped, so every field may be missing. */
interface ShareResponse {
  kind?: string;
  text?: string;
  link?: string;
}

function toneOf(kind: string | undefined): '' | 'error' | 'done' {
  if (kind === 'error') {
    return 'error';
  }

  return kind === 'cancelled'
    ? ''
    : 'done';
}

/** The popup stays open for a full page or a visible area, so it puts the link on the clipboard itself. */
async function run(mode: ShareMode, buttons: HTMLButtonElement[]): Promise<void> {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });

  if (tab?.id === undefined) {
    setStatus(STRINGS.noPage, 'error');

    return;
  }

  for (const button of buttons) {
    button.disabled = true;
  }

  setStatus(mode === 'element'
    ? STRINGS.picking
    : STRINGS.sharing);

  try {
    const response: ShareResponse | undefined = await chrome.runtime.sendMessage({
      type: 'share',
      mode,
      tabId: tab.id,
    });

    if (response?.kind === 'link' && response.link) {
      await navigator.clipboard.writeText(response.link);
    }

    setStatus(response?.text ?? STRINGS.badReply, toneOf(response?.kind));
  } catch (error) {
    setStatus(error instanceof Error
      ? error.message
      : String(error), 'error');
  } finally {
    for (const button of buttons) {
      button.disabled = false;
    }
  }
}

if (actions !== null) {
  const buttons: HTMLButtonElement[] = [];

  for (const mode of SHARE_MODES) {
    const button = document.createElement('button');

    button.type = 'button';

    button.className = mode === 'fullPage'
      ? 'primary'
      : '';

    button.setAttribute('aria-label', STRINGS[mode]);
    button.append(icon(mode), document.createTextNode(STRINGS[mode]));

    button.addEventListener('click', () => {
      void run(mode, buttons);
    });

    buttons.push(button);
    actions.append(button);
  }
}
