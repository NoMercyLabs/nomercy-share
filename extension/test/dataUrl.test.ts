import { describe, expect, it } from 'vitest';
import { dataUrlToBase64 } from '../src/dataUrl';

describe('dataUrlToBase64', () => {
  it('returns the base64 part of a PNG data URL', () => {
    expect(dataUrlToBase64('data:image/png;base64,iVBORw0KGgo=')).toBe('iVBORw0KGgo=');
  });

  it('keeps a long payload whole', () => {
    const payload = 'A'.repeat(100000);

    expect(dataUrlToBase64(`data:image/png;base64,${payload}`)).toBe(payload);
  });

  it('refuses something that is not a data URL', () => {
    expect(() => dataUrlToBase64('https://example.com/a.png')).toThrow('The capture is not a PNG data URL.');
  });

  it('refuses a data URL that is not base64 PNG', () => {
    expect(() => dataUrlToBase64('data:text/plain,hello')).toThrow('The capture is not a PNG data URL.');
    expect(() => dataUrlToBase64('data:image/jpeg;base64,/9j/4AAQ')).toThrow('The capture is not a PNG data URL.');
  });
});
