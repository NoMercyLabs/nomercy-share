import type { DesktopUiOptions } from '@nomercy-entertainment/nomercy-video-player/plugins/desktop-ui';

// One file with one video and at most one audio track: no queue, chapters, tracks or quality levels.
// Speed lives in the settings menu, so it needs no button; seeking uses the bar and the keys.
export const SHARE_BUTTONS = {
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
} as const satisfies DesktopUiOptions['buttons'];
