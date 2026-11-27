import { describe, expect, it } from 'vitest';
import { MAX_FULL_PAGE_HEIGHT } from '../src/config';
import { elementClip, fullPageClip } from '../src/clip';

describe('fullPageClip', () => {
  it('covers the whole content size when it is under the cap', () => {
    expect(fullPageClip({ width: 1280, height: 3000 })).toEqual({ x: 0, y: 0, width: 1280, height: 3000, scale: 1 });
  });

  it('clamps a taller page to the height cap', () => {
    expect(fullPageClip({ width: 1280, height: 50000 })).toEqual({ x: 0, y: 0, width: 1280, height: MAX_FULL_PAGE_HEIGHT, scale: 1 });
  });

  it('keeps a page exactly at the cap whole', () => {
    expect(fullPageClip({ width: 800, height: MAX_FULL_PAGE_HEIGHT }).height).toBe(MAX_FULL_PAGE_HEIGHT);
  });

  it('rounds a fractional content size up so the last row is not cut', () => {
    expect(fullPageClip({ width: 1279.2, height: 2999.5 })).toEqual({ x: 0, y: 0, width: 1280, height: 3000, scale: 1 });
  });

  it('refuses an empty page', () => {
    expect(fullPageClip({ width: 0, height: 3000 })).toBeNull();
    expect(fullPageClip({ width: 1280, height: 0 })).toBeNull();
  });

  it('takes the cap from the argument', () => {
    expect(fullPageClip({ width: 100, height: 900 }, 500).height).toBe(500);
  });
});

describe('elementClip', () => {
  it('adds the scroll offset to the viewport rect to get page coordinates', () => {
    expect(elementClip({ left: 40, top: 100, width: 300, height: 200 }, { x: 0, y: 1500 }, 1)).toEqual({
      x: 40,
      y: 1600,
      width: 300,
      height: 200,
      scale: 1,
    });
  });

  it('adds the horizontal scroll offset too', () => {
    expect(elementClip({ left: 10, top: 10, width: 100, height: 100 }, { x: 250, y: 0 }, 1)?.x).toBe(260);
  });

  it('snaps outward to whole device pixels at a device pixel ratio of 2', () => {
    expect(elementClip({ left: 10.3, top: 20.1, width: 50.2, height: 30 }, { x: 100, y: 0 }, 2)).toEqual({
      x: 110,
      y: 20,
      width: 50.5,
      height: 30.5,
      scale: 1,
    });
  });

  it('snaps outward to whole pixels at a device pixel ratio of 1', () => {
    expect(elementClip({ left: 0.4, top: 0.6, width: 10, height: 10 }, { x: 0, y: 0 }, 1)).toEqual({
      x: 0,
      y: 0,
      width: 11,
      height: 11,
      scale: 1,
    });
  });

  it('clamps an element that runs past the height cap', () => {
    const clip = elementClip({ left: 0, top: 0, width: 100, height: 40000 }, { x: 0, y: 16000 }, 1);
    expect(clip?.y).toBe(16000);
    expect(clip?.height).toBe(MAX_FULL_PAGE_HEIGHT - 16000);
  });

  it('refuses an element that starts below the cap', () => {
    expect(elementClip({ left: 0, top: 0, width: 100, height: 100 }, { x: 0, y: MAX_FULL_PAGE_HEIGHT + 10 }, 1)).toBeNull();
  });

  it('refuses an element without size', () => {
    expect(elementClip({ left: 0, top: 0, width: 0, height: 100 }, { x: 0, y: 0 }, 1)).toBeNull();
    expect(elementClip({ left: 0, top: 0, width: 100, height: 0 }, { x: 0, y: 0 }, 1)).toBeNull();
  });

  it('treats a missing device pixel ratio as 1', () => {
    expect(elementClip({ left: 5.5, top: 5, width: 10, height: 10 }, { x: 0, y: 0 }, 0)?.x).toBe(5);
  });
});
