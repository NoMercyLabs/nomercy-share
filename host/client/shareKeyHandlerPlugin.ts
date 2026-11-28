import { KeyHandlerPlugin } from '@nomercy-entertainment/nomercy-video-player/plugins/key-handler';

/** The player's keys without the groups that do nothing for one shared video. */
export class ShareKeyHandlerPlugin extends KeyHandlerPlugin {
  protected override addDefaults() {
    super.addDefaults();
    this.unbind('MediaStop');

    this.bind('i', () => {
      this.player.togglePip();
    });
  }

  // Subtitle and audio track cycling.
  protected override addMediaKeys() {}

  protected override addNextPrevKeys() {}

  protected override addChapterKeys() {}

  protected override addFrameAdvanceKey() {}

  protected override addShowTimeKey() {}

  protected override addSubtitleSizeKeys() {}

  protected override addAspectRatioKeys() {}

  protected override addStopKey() {}

  // The shortcut list would name keys that are off here.
  protected override addHelpKey() {}
}
