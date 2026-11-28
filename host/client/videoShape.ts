interface ShapeSource {
  readonly videoElement: { readonly videoWidth: number; readonly videoHeight: number } | undefined;
  on(event: 'duration', listener: () => void): void;
}

interface ShapedBox {
  readonly style: { setProperty(name: string, value: string): void };
}

/** Gives the player's box the video's own shape once its size is known; the page style holds 16:9 until then. */
export function followVideoShape(player: ShapeSource, box: ShapedBox) {
  player.on('duration', () => {
    const video = player.videoElement;

    if (video?.videoWidth && video.videoHeight) {
      box.style.setProperty('--video-shape', `${video.videoWidth} / ${video.videoHeight}`);
    }
  });
}
