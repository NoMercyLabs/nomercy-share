import { describe, expect, it } from 'vitest';
import { followVideoShape } from '../client/videoShape.js';
import { playerStyles } from '../src/playerTheme.js';
import { styles } from '../src/theme.js';

function shapeAfterMetadata(videoWidth: number, videoHeight: number) {
  const set = new Map<string, string>();
  const listeners: (() => void)[] = [];

  followVideoShape(
    {
      videoElement: { videoWidth, videoHeight },
      on: (_event, listener) => {
        listeners.push(listener);
      },
    },
    { style: { setProperty: (name, value) => set.set(name, value) } },
  );

  for (const listener of listeners) {
    listener();
  }

  return set.get('--video-shape');
}

describe('the player box', () => {
  it.each([
    [1280, 720, '1280 / 720'],
    [720, 1280, '720 / 1280'],
  ])('takes the shape of a %i by %i video', (width, height, shape) => {
    expect(shapeAfterMetadata(width, height)).toBe(shape);
  });

  it('keeps the default shape while the video size is unknown', () => {
    expect(shapeAfterMetadata(0, 0)).toBeUndefined();
  });

  it('is 16:9 until then and never taller than the page leaves room for', () => {
    expect(playerStyles).toContain('aspect-ratio: var(--video-shape, 16 / 9);');
    expect(playerStyles).toContain('max-height: max(200px, calc(100dvh - var(--page-chrome-height)));');
  });

  it('fills the whole frame when embedded', () => {
    expect(playerStyles).toContain('height: 100dvh; max-height: none; aspect-ratio: auto; border-radius: 0;');
  });

  it('shares one page offset with the plain media', () => {
    expect(styles.match(/160px/g)).toHaveLength(1);
    expect(styles).toContain('--page-chrome-height: 160px;');
    expect(styles).toContain('max-height: calc(100dvh - var(--page-chrome-height));');
    expect(playerStyles).not.toContain('160px');
  });
});
