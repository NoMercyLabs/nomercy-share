import { nmplayer } from '@nomercy-entertainment/nomercy-video-player';
import { DesktopUiPlugin } from '@nomercy-entertainment/nomercy-video-player/plugins/desktop-ui';
import { PLAYER_ELEMENT_ID } from '../src/playerElement.js';
import { SHARE_BUTTONS } from './shareButtons.js';
import { ShareEmbedPlugin } from './shareEmbedPlugin.js';
import { ShareKeyHandlerPlugin } from './shareKeyHandlerPlugin.js';
import { followVideoShape } from './videoShape.js';

function startPlayer(mount: HTMLElement) {
  const source = mount.dataset['source'] ?? '';
  const title = mount.dataset['title'] ?? '';
  const allowedOrigins = (mount.dataset['embedOrigins'] ?? '').split(' ').filter(Boolean);
  const player = nmplayer(PLAYER_ELEMENT_ID)
    .addPlugin(DesktopUiPlugin, { buttons: SHARE_BUTTONS, hideTitle: true })
    .addPlugin(ShareKeyHandlerPlugin)
    .addPlugin(ShareEmbedPlugin, { allowedOrigins })
    .setup({
      playlist: [{
        id: 'share',
        title,
        url: source,
      }],
    });

  followVideoShape(player, mount);

  player.on('ready', () => {
    // The hidden fallback video must not keep loading beside the player.
    mount.parentElement?.querySelector(':scope > video.media')?.remove();
    player.item(0, { autoplay: false });
  });
}

const mount = document.getElementById(PLAYER_ELEMENT_ID);

if (mount) {
  startPlayer(mount);
}
