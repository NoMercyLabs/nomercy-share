/** How a tab is captured; these are the words the app's native host accepts as the mode. */
export type ShareMode = 'fullPage' | 'visible' | 'element';

export const SHARE_MODES = ['fullPage', 'visible', 'element'] as const satisfies readonly ShareMode[];

export function isShareMode(value: unknown): value is ShareMode {
  // A type guard takes anything: the browser delivers messages and menu ids untyped.
  return SHARE_MODES.some((mode) => mode === value);
}
