import { describe, expect, it } from 'vitest';
import { SHARE_BUTTONS } from '../client/shareButtons.js';
import { ShareEmbedPlugin } from '../client/shareEmbedPlugin.js';
import { ShareKeyHandlerPlugin } from '../client/shareKeyHandlerPlugin.js';

class RecordingKeys extends ShareKeyHandlerPlugin {
  readonly bound = new Set<string>();

  override bind(combo: string) {
    this.bound.add(combo);
  }

  override unbind(combo: string) {
    this.bound.delete(combo);
  }

  installed() {
    // An empty player config: no switch turns the keys off.
    Object.defineProperty(this, 'player', { value: { options: {} } });
    this.addDefaults();

    return [...this.bound].sort();
  }
}

class OpenEmbed extends ShareEmbedPlugin {
  allows(origin: string) {
    return this.isOriginAllowed(origin);
  }
}

describe('share player settings', () => {
  it('turns off every button that does nothing for one video', () => {
    expect(SHARE_BUTTONS).toEqual({
      play: true,
      mute: true,
      volume: true,
      fullscreen: true,
      settings: true,
      pip: true,
      next: false,
      previous: false,
      theater: false,
      speed: false,
      quality: false,
      subtitles: false,
      audio: false,
      playlist: false,
      chapterPrev: false,
      chapterNext: false,
      seekBack: false,
      seekForward: false,
      aspectRatio: false,
      cast: false,
    });
  });

  it('binds only play, seek, volume, mute, fullscreen, picture in picture and speed keys', () => {
    expect(new RecordingKeys().installed()).toEqual([
      ' ', '1', '3', '6', '9', '=', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'ArrowUp',
      'ColorF0Red', 'ColorF1Green', 'ColorF2Yellow', 'ColorF3Blue', 'Escape', 'F11',
      'MediaFastForward', 'MediaPause', 'MediaPlay', 'MediaPlayPause', 'MediaRewind', '[', ']',
      'alt+ArrowLeft', 'alt+ArrowRight', 'ctrl+ArrowLeft', 'ctrl+ArrowRight', 'f', 'i', 'm',
      'shift+ArrowLeft', 'shift+ArrowRight',
    ].sort());
  });

  it.each([
    ['https://example.org', true],
    ['https://app.example.org', true],
    ['https://a.b.example.org', true],
    ['http://app.example.org', false],
    ['https://evilexample.org', false],
    ['https://example.org.evil.test', false],
    ['null', false],
  ])('lets %s control the player: %s', (origin, allowed) => {
    const embed = new OpenEmbed();

    embed.allowedOrigins(['https://example.org', 'https://*.example.org']);

    expect(embed.allows(origin)).toBe(allowed);
  });

  it('lets no origin in with an empty list', () => {
    expect(new OpenEmbed().allows('https://example.org')).toBe(false);
  });
});
