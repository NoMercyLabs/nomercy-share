// Dark NM tokens, copied from src/NoMercy.Share.App/Theme/NmTokens.Dark.axaml (Nm-Bg, Nm-Gray-*, Nm-Accent-*).

export const logoMark = `<svg class="mark" viewBox="0 0 130 130" aria-hidden="true" xmlns="http://www.w3.org/2000/svg"><path fill="#707070" fill-rule="evenodd" d="M49.9746 104.882C45.1151 108.166 42.6854 109.807 40.6684 109.696C38.9115 109.598 37.2859 108.735 36.2217 107.334C35 105.725 35 102.792 35 96.9276V33.0725C35 27.2077 35 24.2753 36.2217 22.6666C37.2859 21.2653 38.9115 20.4018 40.6684 20.3045C42.6854 20.1929 45.1151 21.8346 49.9747 25.1181L97.2274 57.0456C101.17 59.7098 103.142 61.0419 103.831 62.7154C104.434 64.1789 104.434 65.8212 103.831 67.2847C103.142 68.9583 101.17 70.2904 97.2274 72.9545L49.9746 104.882ZM126 21.0001C128.209 21.0001 130 22.7909 130 25.0001V105C130 107.209 128.209 109 126 109H116.5C114.291 109 112.5 107.209 112.5 105V25.0001C112.5 22.7909 114.291 21.0001 116.5 21.0001H126Z"/><path fill="#EDEDED" fill-rule="evenodd" d="M9.35914 108.676C5.3744 111.369 0 108.514 0 103.705V26.2953C0 21.4863 5.37442 18.6314 9.35916 21.3238L66.6421 60.0285C70.1633 62.4077 70.1633 67.5925 66.6421 69.9716L9.35914 108.676ZM91 21.0001C93.2091 21.0001 95 22.7909 95 25.0001V105C95 107.209 93.2091 109 91 109H81.5C79.2909 109 77.5 107.209 77.5 105V25.0001C77.5 22.7909 79.2909 21.0001 81.5 21.0001H91Z"/></svg>`;

export const styles = `
:root {
  --bg: #0A0A0A;
  --surface: #111111;
  --surface-hover: #191919;
  --border: #2A2A2A;
  --text: #EEEEEE;
  --muted: #B4B4B4;
  --faint: #7B7B7B;
  --accent: #0BD8B6;
  --accent-hover: #0EB39E;
  --accent-fg: #0A0A0A;
  color-scheme: dark;
}
* { box-sizing: border-box; }
html, body { height: 100%; margin: 0; }
body {
  display: flex;
  flex-direction: column;
  min-height: 100dvh;
  background: var(--bg);
  color: var(--text);
  font: 16px/1.5 system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
}
.bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 16px 24px;
  color: var(--faint);
  font-size: 13px;
}
.brand { display: inline-flex; align-items: center; gap: 8px; color: var(--muted); text-decoration: none; }
.mark { width: 18px; height: 18px; }
.stage {
  flex: 1;
  min-height: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0 24px;
}
.media { display: block; max-width: 100%; max-height: calc(100dvh - 160px); object-fit: contain; border-radius: 8px; background: var(--surface); }
audio.media { width: min(100%, 480px); }
.actions { display: flex; flex-wrap: wrap; justify-content: center; gap: 12px; padding: 24px; }
.button {
  display: inline-flex;
  align-items: center;
  min-height: 40px;
  padding: 0 20px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--surface);
  color: var(--text);
  font-weight: 600;
  text-decoration: none;
}
.button:hover { background: var(--surface-hover); }
.button.primary { border-color: var(--accent); background: var(--accent); color: var(--accent-fg); }
.button.primary:hover { background: var(--accent-hover); border-color: var(--accent-hover); }
a:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }
.notice { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 8px; padding: 24px; text-align: center; }
.notice h1 { margin: 0; font-size: 24px; font-weight: 600; }
.notice p { margin: 0; max-width: 420px; color: var(--muted); }
.note { color: var(--muted); }
`;
