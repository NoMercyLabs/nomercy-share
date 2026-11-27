import { STRINGS } from './strings';

/** What the user is told after a share, and the link when there is one. */
export type ShareOutcome =
  | {
    kind: 'link';
    link: string;
    text: string;
  } |
  { kind: 'local'; text: string } |
  { kind: 'error'; text: string };

/** What the app's native host answers; the browser hands it over untyped, so every field may be missing. */
export interface NativeReply {
  ok?: boolean;
  link?: string;
  path?: string;
  local?: boolean;
  error?: string;
}

const MISSING_HOST = 'native messaging host not found';

function fileName(path: string): string {
  return path.split(/[\\/]/).pop() ?? path;
}

function unexpected() {
  return { kind: 'error', text: STRINGS.badReply } as const satisfies ShareOutcome;
}

/** Turns what the app answered into what the status line shows. */
export function describeReply(reply: NativeReply | null | undefined) {
  if (reply?.ok === true && reply.link) {
    return {
      kind: 'link',
      link: reply.link,
      text: STRINGS.linkCopied,
    } as const satisfies ShareOutcome;
  }

  if (reply?.ok === true && reply.path) {
    return { kind: 'local', text: `${STRINGS.savedOnThisPc}${fileName(reply.path)}.` } as const satisfies ShareOutcome;
  }

  if (reply?.ok === false && reply.error) {
    return { kind: 'error', text: reply.error } as const satisfies ShareOutcome;
  }

  return unexpected();
}

/** Turns a browser error (the app was not found, it exited, it was never asked) into what the status line shows. */
export function describeFailure(message: string | undefined) {
  if (!message) {
    return { kind: 'error', text: STRINGS.noAnswer } as const satisfies ShareOutcome;
  }

  return {
    kind: 'error',
    text: message.toLowerCase().includes(MISSING_HOST)
      ? STRINGS.hostMissing
      : message,
  } as const satisfies ShareOutcome;
}
