import { PLAYER_SCRIPT_URL } from './playerAssets.js';
import { PLAYER_ELEMENT_ID } from './playerElement.js';

export interface VideoStage {
  /** Already escaped for an attribute, like every field here. */
  rawUrl: string;
  title: string;
  embedOrigins: string;
}

// The plain video works on its own; the player script replaces it once the player is ready.
export const videoStage = (stage: VideoStage) =>
  `<video class="media" src="${stage.rawUrl}" controls playsinline preload="metadata"></video>
<div id="${PLAYER_ELEMENT_ID}" class="player" data-source="${stage.rawUrl}" data-title="${stage.title}" data-embed-origins="${stage.embedOrigins}"></div>
<script type="module" src="${PLAYER_SCRIPT_URL}"></script>`;
