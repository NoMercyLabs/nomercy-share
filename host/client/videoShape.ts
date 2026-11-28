interface ShapeSource {
  readonly videoElement: { readonly videoWidth: number; readonly videoHeight: number } | undefined;
  on(event: 'duration', listener: () => void): void;
}

interface ShapedBox {
  readonly style: { setProperty(name: string, value: string): void };
}

export function followVideoShape(_player: ShapeSource, _box: ShapedBox) {}
