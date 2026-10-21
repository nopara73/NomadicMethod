import assert from 'node:assert/strict';
import test from 'node:test';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { ReactionDrillView } from '../reaction-drill-view.js';

function fixture() {
  const errors = [];
  const view = Object.assign(Object.create(ReactionDrillView.prototype), {
    disposed: false, objectUrls: [], downloads: new AbortController(),
    fail: () => errors.push('unavailable'),
  });
  const video = { loads: 0, load() { this.loads++; } };
  return { view, video, errors };
}

test('a partial download cannot prepare a reaction response', async t => {
  const { view, video, errors } = fixture();
  const originalFetch = globalThis.fetch;
  let stream;
  t.mock.method(globalThis, 'fetch', async () => new Response(new ReadableStream({
    start(controller) { stream = controller; controller.enqueue(new TextEncoder().encode('first')); },
  })));
  const pending = view.loadVideo(video, 'https://fixture.invalid/response.mp4');
  await nextTurn();
  assert.equal(video.src, undefined);
  assert.equal(video.loads, 0);
  stream.enqueue(new TextEncoder().encode('-last'));
  stream.close();
  await pending;
  try {
    assert.equal(await (await originalFetch(video.src)).text(), 'first-last');
    assert.equal(video.loads, 1);
    assert.deepEqual(errors, []);
  } finally { view.objectUrls.forEach(url => URL.revokeObjectURL(url)); }
});

test('leaving an exercise cancels its pending download without a late player or error', async t => {
  const { view, video, errors } = fixture();
  t.mock.method(globalThis, 'fetch', (_, { signal }) => new Promise((_, reject) =>
    signal.addEventListener('abort', () => reject(new DOMException('Cancelled', 'AbortError')), { once: true })));
  const pending = view.loadVideo(video, 'https://fixture.invalid/response.mp4');
  view.disposed = true;
  view.downloads.abort();
  await pending;
  assert.equal(video.loads, 0);
  assert.deepEqual(view.objectUrls, []);
  assert.deepEqual(errors, []);
});

test('an unavailable response fails closed before media is prepared', async t => {
  const { view, video, errors } = fixture();
  t.mock.method(globalThis, 'fetch', async () => new Response('', { status: 503 }));
  await view.loadVideo(video, 'https://fixture.invalid/response.mp4');
  assert.equal(video.loads, 0);
  assert.deepEqual(errors, ['unavailable']);
});

test('intentional preview cancellation is harmless while active playback failures remain errors', async () => {
  const { view, errors } = fixture();
  const old = { play: () => Promise.reject(new DOMException('Switched preview', 'AbortError')) };
  const active = { play: () => Promise.reject(new DOMException('Playback denied', 'NotAllowedError')) };
  view.videos = [old, active];
  view.activeIndex = 1;
  view.play(old);
  await nextTurn();
  assert.deepEqual(errors, []);
  view.play(active);
  await nextTurn();
  assert.deepEqual(errors, ['unavailable']);
});
