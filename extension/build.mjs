// Bundles the extension into dist/, which is what the browser loads unpacked.
import { build } from 'esbuild';
import { copyFileSync, mkdirSync, rmSync } from 'node:fs';

const out = 'dist';

rmSync(out, { recursive: true, force: true });
mkdirSync(out, { recursive: true });

await build({
  entryPoints: ['src/background.ts', 'src/popup.ts'],
  outdir: out,
  bundle: true,
  format: 'esm',
  target: 'chrome120',
  logLevel: 'info',
});

for (const file of ['manifest.json', 'popup.html', 'popup.css']) {
  copyFileSync(file, `${out}/${file}`);
}
