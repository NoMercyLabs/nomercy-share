/**
 * Moooom icon paths on a 24-unit box, drawn stroked at 1.5. They are copied from the app's generated MoooomIconData.g.cs
 * (macOsWindow, monitor and crossHair) and a test checks every path is still there.
 */
export const ICONS = {
  fullPage:
    'M3 8v8.4c0 1.5 0 2.25.382 2.776q.187.256.442.442C4.35 20 5.1 20 6.6 20h10.8c1.5 0 2.25 0 2.776-.382q.256-.187.442-.442C21 18.65 21 17.9 21 16.4V8M3 8v-.4c0-1.5 0-2.25.382-2.776a2 2 0 0 1 .442-.442C4.35 4 5.1 4 6.6 4h10.8c1.5 0 2.25 0 2.776.382q.256.187.442.442C21 5.35 21 6.1 21 7.6V8M3 8h18M5 6h.01m1.98 0H7m1.99 0H9',
  visible:
    'M4 13c0 .932 0 1.398.152 1.765a2 2 0 0 0 1.083 1.083C5.602 16 6.068 16 7 16h3m-6-3V7.6c0-1.5 0-2.25.382-2.776a2 2 0 0 1 .442-.442C5.35 4 6.1 4 7.6 4h8.8c1.5 0 2.25 0 2.776.382q.256.187.442.442C20 5.35 20 6.1 20 7.6V13M4 13h16m0 0c0 .932 0 1.398-.152 1.765a2 2 0 0 1-1.083 1.083C18.398 16 17.932 16 17 16h-3m-4 0v4m0-4h4m-4 4h4m-4 0H7m7 0v-4m0 4h3',
  element:
    'M20 12h-4m4 0a8 8 0 0 1-8 8m8-8a8 8 0 0 0-8-8m0 16v-4m0 4a8 8 0 0 1-8-8m0 0h4m-4 0a8 8 0 0 1 8-8m0 0v4',
} as const;
