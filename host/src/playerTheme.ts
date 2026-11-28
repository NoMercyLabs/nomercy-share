// The player's place on the page. The player brings its own control styles.
export const playerStyles = `
.player { display: none; width: 100%; height: calc(100dvh - 160px); min-height: 200px; border-radius: 8px; overflow: hidden; }
.player.nomercyplayer { display: block; }
.stage:has(> .player.nomercyplayer) > video.media { display: none; }
/* The player has no option for these two: one share has no subtitles, and the key list names keys that are off here. */
.player #menu-button-subtitleSettings, .player .shortcuts-overlay { display: none !important; }
body:has(.player.nm-embed) .bar, body:has(.player.nm-embed) .actions { display: none; }
body:has(.player.nm-embed) .stage { padding: 0; }
body:has(.player.nm-embed) .player { height: 100dvh; border-radius: 0; }
`;
