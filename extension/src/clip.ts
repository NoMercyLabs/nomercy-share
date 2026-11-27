import { MAX_FULL_PAGE_HEIGHT } from './config';

/** The area of the page to photograph, in CSS pixels of the whole page (not the window). */
export interface Clip {
  x: number;
  y: number;
  width: number;
  height: number;
  scale: number;
}

/** A rectangle in window coordinates, as getBoundingClientRect gives it. */
export interface ViewportRect {
  left: number;
  top: number;
  width: number;
  height: number;
}

export interface Scroll {
  x: number;
  y: number;
}

/** The whole page from its top-left corner, cut at the height cap. */
export function fullPageClip(
  content: { width: number; height: number },
  maxHeight = MAX_FULL_PAGE_HEIGHT,
): Clip | null {
  const width = Math.ceil(content.width);
  const height = Math.min(Math.ceil(content.height), maxHeight);

  if (!Number.isFinite(width) || !Number.isFinite(height) || width <= 0 || height <= 0) {
    return null;
  }

  return {
    x: 0,
    y: 0,
    width,
    height,
    scale: 1,
  };
}

/**
 * One element, in page coordinates. The edges snap outward to whole device pixels, so a fractional rect never loses its
 * last row of pixels to rounding. Null when nothing is left to capture.
 */
export function elementClip(
  rect: ViewportRect,
  scroll: Scroll,
  devicePixelRatio: number,
  maxHeight = MAX_FULL_PAGE_HEIGHT,
): Clip | null {
  const ratio = devicePixelRatio > 0 && Number.isFinite(devicePixelRatio)
    ? devicePixelRatio
    : 1;
  // Rounding to a thousandth first keeps 160.5 * 2 from reading 321.00000000000006 and snapping a pixel too far.
  const device = (value: number): number => Math.round(value * ratio * 1000) / 1000;

  const x = Math.floor(device(rect.left + scroll.x)) / ratio;
  const y = Math.floor(device(rect.top + scroll.y)) / ratio;
  const right = Math.ceil(device(rect.left + scroll.x + rect.width)) / ratio;
  const pageBottom = Math.ceil(device(rect.top + scroll.y + rect.height)) / ratio;
  const bottom = Math.min(pageBottom, maxHeight);

  const width = right - x;
  const height = bottom - y;

  if (rect.width <= 0 || rect.height <= 0 || !(width > 0) || !(height > 0)) {
    return null;
  }

  return {
    x,
    y,
    width,
    height,
    scale: 1,
  };
}
