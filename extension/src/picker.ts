import type { ViewportRect } from './clip';

/** What the picker hands back: the element's rect in the window, and where the window sits on the page. */
export interface PickedElement extends ViewportRect {
  scrollX: number;
  scrollY: number;
  devicePixelRatio: number;
}

/**
 * Runs inside the page (chrome.scripting serializes it), so it may use nothing from this module. It draws one overlay
 * element with a shadow root, follows the hovered element, and answers with the clicked one, or null on Escape. The
 * overlay and every listener are gone before it answers.
 */
export function pickElement(): Promise<PickedElement | null> {
  return new Promise((resolve) => {
    const host = document.createElement('div');

    host.style.cssText = 'all: initial; position: fixed; inset: 0; z-index: 2147483647; pointer-events: none;';
    const shadow = host.attachShadow({ mode: 'closed' });
    const box = document.createElement('div');

    box.style.cssText =
      'position: fixed; box-sizing: border-box; display: none; outline: 2px solid #0BD8B6; outline-offset: -2px; background: rgba(11, 216, 182, 0.14); border-radius: 2px;';

    shadow.append(box);

    let hovered: Element | null = null;

    const draw = (): void => {
      if (hovered === null) {
        box.style.display = 'none';

        return;
      }

      const rect = hovered.getBoundingClientRect();

      box.style.display = 'block';
      box.style.left = `${rect.left}px`;
      box.style.top = `${rect.top}px`;
      box.style.width = `${rect.width}px`;
      box.style.height = `${rect.height}px`;
    };

    const onMove = (event: MouseEvent): void => {
      const target = document.elementFromPoint(event.clientX, event.clientY);

      hovered = target === host
        ? hovered
        : target;

      draw();
    };

    const stop = (event: Event): void => {
      event.preventDefault();
      event.stopPropagation();
    };

    const finish = (result: PickedElement | null): void => {
      window.removeEventListener('mousemove', onMove, true);
      window.removeEventListener('scroll', draw, true);
      window.removeEventListener('click', onClick, true);
      window.removeEventListener('keydown', onKey, true);
      window.removeEventListener('mousedown', stop, true);
      window.removeEventListener('mouseup', stop, true);
      window.removeEventListener('pointerdown', stop, true);
      window.removeEventListener('pointerup', stop, true);
      host.remove();
      resolve(result);
    };

    function onClick(event: MouseEvent): void {
      stop(event);

      if (hovered === null) {
        return;
      }

      const rect = hovered.getBoundingClientRect();

      finish({
        left: rect.left,
        top: rect.top,
        width: rect.width,
        height: rect.height,
        scrollX: window.scrollX,
        scrollY: window.scrollY,
        devicePixelRatio: window.devicePixelRatio,
      });
    }

    function onKey(event: KeyboardEvent): void {
      if (event.key === 'Escape') {
        stop(event);
        finish(null);
      }
    }

    window.addEventListener('mousemove', onMove, true);
    window.addEventListener('scroll', draw, true);
    window.addEventListener('click', onClick, true);
    window.addEventListener('keydown', onKey, true);
    window.addEventListener('mousedown', stop, true);
    window.addEventListener('mouseup', stop, true);
    window.addEventListener('pointerdown', stop, true);
    window.addEventListener('pointerup', stop, true);
    document.documentElement.append(host);
  });
}

/** Runs inside the page: puts text on the clipboard when the popup that would do it has closed. */
export async function copyInPage(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text);

    return true;
  } catch {
    // The page may not have focus; the clipboardWrite permission lets the older command through.
    const area = document.createElement('textarea');

    area.value = text;
    area.style.cssText = 'position: fixed; opacity: 0;';
    document.documentElement.append(area);
    area.select();
    // eslint-disable-next-line @typescript-eslint/no-deprecated -- the only copy that works without page focus
    const copied = document.execCommand('copy');

    area.remove();

    return copied;
  }
}
