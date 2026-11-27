import { describe, expect, it } from 'vitest';
import { describeFailure, describeReply } from '../src/reply';

describe('describeReply', () => {
  it('reports a link', () => {
    expect(describeReply({ ok: true, link: 'https://share.example/abc123', local: false })).toEqual({
      kind: 'link',
      link: 'https://share.example/abc123',
      text: 'Link copied.',
    });
  });

  it('reports a local save by the file name of a Windows path', () => {
    expect(describeReply({ ok: true, path: 'C:\\Users\\someone\\Pictures\\shot-001.png', local: true })).toEqual({
      kind: 'local',
      text: 'Saved on this PC: shot-001.png.',
    });
  });

  it('reports a local save by the file name of a path with slashes', () => {
    expect(describeReply({ ok: true, path: '/home/someone/shot-002.png', local: true }).text).toBe('Saved on this PC: shot-002.png.');
  });

  it('shows the error text the app gave', () => {
    expect(describeReply({ ok: false, error: 'The upload failed: no room.' })).toEqual({ kind: 'error', text: 'The upload failed: no room.' });
  });

  it('does not trust ok without a link or a path', () => {
    expect(describeReply({ ok: true }).kind).toBe('error');
  });

  it('answers an unexpected reply with a fixed text', () => {
    const text = 'NoMercy Share sent an unexpected reply.';
    expect(describeReply(undefined)).toEqual({ kind: 'error', text });
    expect(describeReply('hello')).toEqual({ kind: 'error', text });
    expect(describeReply({ ok: false })).toEqual({ kind: 'error', text });
  });
});

describe('describeFailure', () => {
  it('tells the user to start the app when the host is missing', () => {
    expect(describeFailure('Specified native messaging host not found.')).toEqual({
      kind: 'error',
      text: 'Start NoMercy Share once so the browser can find it.',
    });
  });

  it('shows any other browser error as it is', () => {
    expect(describeFailure('Native host has exited.')).toEqual({ kind: 'error', text: 'Native host has exited.' });
  });

  it('has a fixed text when the browser gave no message', () => {
    expect(describeFailure(undefined)).toEqual({ kind: 'error', text: 'NoMercy Share did not answer.' });
  });
});
