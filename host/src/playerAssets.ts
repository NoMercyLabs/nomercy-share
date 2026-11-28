import manifest from '../package.json' with { type: 'json' };

/** The pinned player release; package.json is the one place that names it. */
export const PLAYER_VERSION = manifest.dependencies['@nomercy-entertainment/nomercy-video-player'];

// The build writes the bundle to public/_player/; the version in the query gives each release a new address.
export const PLAYER_SCRIPT_URL = `/_player/player.js?v=${PLAYER_VERSION}`;
