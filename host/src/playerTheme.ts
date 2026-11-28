// The player's place on the page. The player brings its own control styles.
export const playerStyles = `
.player { display: none; width: 100%; aspect-ratio: var(--video-shape, 16 / 9); max-height: max(200px, calc(100dvh - var(--page-chrome-height))); border-radius: 8px; overflow: hidden; }
.player.nomercyplayer { display: block; }
.stage:has(> .player.nomercyplayer) > video.media { display: none; }
/* The player has no option for these two: one share has no subtitles, and the key list names keys that are off here. */
.player #menu-button-subtitleSettings, .player .shortcuts-overlay { display: none !important; }
body:has(.player.nm-embed) .bar, body:has(.player.nm-embed) .actions { display: none; }
body:has(.player.nm-embed) .stage { padding: 0; }
body:has(.player.nm-embed) .player { height: 100dvh; max-height: none; aspect-ratio: auto; border-radius: 0; }
`;
