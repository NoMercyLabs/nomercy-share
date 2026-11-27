import { describe, expect, it } from 'vitest';
import { refusalFor } from '../src/urls';

describe('refusalFor', () => {
  it.each([
    'chrome://settings',
    'chrome://extensions/',
    'edge://flags',
    'edge://extensions',
    'about:blank',
    'chrome-extension://abcdefghijklmnopabcdefghijklmnop/popup.html',
    'devtools://devtools/bundled/inspector.html',
    'view-source:https://example.com/',
    'file:///C:/notes/page.html',
    'https://chromewebstore.google.com/detail/some-extension/abc',
    'https://chrome.google.com/webstore/detail/some-extension/abc',
    'https://microsoftedge.microsoft.com/addons/detail/some-extension/abc',
  ])('refuses %s', (url) => {
    expect(refusalFor(url)).not.toBeNull();
  });

  it.each(['https://example.com/', 'http://127.0.0.1:8080/page', 'https://chrome.google.com/other', 'https://docs.example.org/a?b=c#d'])(
    'allows %s',
    (url) => {
      expect(refusalFor(url)).toBeNull();
    },
  );

  it('refuses a tab without an address', () => {
    expect(refusalFor(undefined)).toBe('There is no page to capture in this tab.');
    expect(refusalFor('')).toBe('There is no page to capture in this tab.');
  });

  it('refuses an address that does not parse', () => {
    expect(refusalFor('not a url')).not.toBeNull();
  });

  it('gives each kind of page its own words', () => {
    expect(refusalFor('chrome://settings')).toBe('Browser pages cannot be captured.');
    expect(refusalFor('file:///C:/a.html')).toBe('Local files cannot be captured.');
    expect(refusalFor('https://chromewebstore.google.com/')).toBe('The extension store does not allow capture.');
  });
});
