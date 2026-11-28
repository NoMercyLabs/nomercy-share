import { strings } from './strings.js';
import { logoMark, styles } from './theme.js';
import { playerStyles } from './playerTheme.js';
import { videoStage } from './videoStage.js';

export type MediaKind = 'image' | 'video' | 'audio' | 'file';

export interface ViewerModel {
  kind: MediaKind;
  /** The stored media type, so a link preview names the real format (video/webm or video/mp4). */
  contentType: string;
  rawUrl: string;
  pageUrl: string;
  expiryLine: string | null;
  embedOrigins: readonly string[];
}

const HOME_URL = 'https://nomercy.tv';

const escapeHtml = (text: string) =>
  text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');

const bar = (expiryLine: string | null) => `<header class="bar">
  <a class="brand" href="${HOME_URL}">${logoMark}<span>${strings.brand}</span></a>
  ${expiryLine
    ? `<span>${escapeHtml(expiryLine)}</span>`
    : ''}
</header>`;

function document(title: string, head: string, body: string) {
  return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="color-scheme" content="dark">
<title>${escapeHtml(title)}</title>
${head}
<style>${styles}${playerStyles}</style>
</head>
<body>
${body}
</body>
</html>
`;
}

function mediaElement(model: ViewerModel) {
  const raw = escapeHtml(model.rawUrl);

  switch (model.kind) {
    case 'image':
      return `<img class="media" src="${raw}" alt="${strings.fileTitle}">`;
    case 'video':
      return videoStage({
        rawUrl: raw,
        title: escapeHtml(strings.fileTitle),
        embedOrigins: escapeHtml(model.embedOrigins.join(' ')),
      });
    case 'audio':
      return `<audio class="media" src="${raw}" controls preload="metadata"></audio>`;
    case 'file':
      return `<p class="note">${strings.unsupportedType}</p>`;
  }
}

function previewTags(model: ViewerModel) {
  const raw = escapeHtml(model.rawUrl);
  const media = {
    image: `<meta property="og:image" content="${raw}">\n<meta name="twitter:card" content="summary_large_image">\n<meta name="twitter:image" content="${raw}">`,
    video: `<meta property="og:video" content="${raw}">\n<meta property="og:video:type" content="${escapeHtml(model.contentType)}">\n<meta name="twitter:card" content="player">`,
    audio: `<meta property="og:audio" content="${raw}">\n<meta name="twitter:card" content="summary">`,
    file: '<meta name="twitter:card" content="summary">',
  };

  return `<meta property="og:title" content="${strings.fileTitle}">
<meta property="og:type" content="website">
<meta property="og:site_name" content="${strings.siteName}">
<meta property="og:url" content="${escapeHtml(model.pageUrl)}">
${media[model.kind]}`;
}

export function viewerPage(model: ViewerModel) {
  const raw = escapeHtml(model.rawUrl);
  const body = `${bar(model.expiryLine)}
<main class="stage">${mediaElement(model)}</main>
<footer class="actions">
  <a class="button" href="${raw}">${strings.openOriginal}</a>
  <a class="button primary" href="${raw}" download>${strings.download}</a>
</footer>`;

  return document(strings.fileTitle, previewTags(model), body);
}

export function noticePage(title: string, text: string) {
  const body = `${bar(null)}
<main class="notice"><h1>${escapeHtml(title)}</h1><p>${escapeHtml(text)}</p></main>`;

  return document(`${title} - ${strings.siteName}`, '', body);
}

export const homePage = () => noticePage(strings.homeTitle, strings.homeBody);
