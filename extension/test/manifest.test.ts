import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { ICONS } from '../src/icons';

const here = (path: string): string => new URL(path, import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1');

const manifest: unknown = JSON.parse(readFileSync(here('../manifest.json'), 'utf8'));
const publicKey = readFileSync(here('../manifest-key.txt'), 'utf8').trim();

function field(name: string): unknown {
  return typeof manifest === 'object' && manifest !== null ? Reflect.get(manifest, name) : undefined;
}

/** Chrome derives the extension id from the first 16 bytes of the SHA-256 of the DER key, each nibble written as a to p. */
function extensionId(base64Key: string): string {
  const hash = createHash('sha256').update(Buffer.from(base64Key, 'base64')).digest('hex').slice(0, 32);
  return [...hash].map((digit) => String.fromCharCode(97 + Number.parseInt(digit, 16))).join('');
}

describe('manifest', () => {
  it('carries the public key from manifest-key.txt', () => {
    expect(field('key')).toBe(publicKey);
  });

  it('gives the extension the id the app registers for its native host', () => {
    expect(extensionId(publicKey)).toBe('pffggmajnklhkjjgldpojacknecfkmlc');
    expect(extensionId(String(field('key')))).toBe('pffggmajnklhkjjgldpojacknecfkmlc');
  });

  it('asks for exactly the permissions the capture needs', () => {
    expect(field('permissions')).toEqual(['activeTab', 'scripting', 'nativeMessaging', 'debugger', 'clipboardWrite', 'contextMenus']);
  });

  it('asks for no host access', () => {
    expect(field('host_permissions')).toBeUndefined();
    expect(field('optional_host_permissions')).toBeUndefined();
    expect(field('content_scripts')).toBeUndefined();
  });

  it('is a Manifest V3 extension named NoMercy Share with a popup', () => {
    expect(field('manifest_version')).toBe(3);
    expect(field('name')).toBe('NoMercy Share');
    expect(field('action')).toMatchObject({ default_popup: 'popup.html' });
  });
});

describe('icons', () => {
  it('are the generated Moooom paths of the app', () => {
    const generated = readFileSync(here('../../src/NoMercy.Share.App/Icons/MoooomIconData.g.cs'), 'utf8');
    for (const path of Object.values(ICONS)) {
      expect(generated).toContain(`"${path}"`);
    }
  });
});
