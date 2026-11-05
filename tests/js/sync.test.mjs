// node --test: офлайн-очередь и синхронизация (js/sync.js) на хранилище в памяти и подменённом fetch.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createSync, uuidv7 } from '../../src/JobTracker.Api/wwwroot/js/sync.js';
import { memoryStorage } from '../../src/JobTracker.Api/wwwroot/js/db.js';

const snapshot = (extra = {}) => ({ contentVersion: 'v1', applications: [], study: { marks: [] }, ideas: [], highlights: [], ...extra });
const json = (status, body) => ({ ok: status >= 200 && status < 300, status, json: async () => body });

/** Сервер, которого можно выключить: down — fetch падает как при выключенном сервере. */
function server() {
  const s = { down: false, sent: [], reply: () => json(204), data: snapshot() };
  s.fetch = async (url, init = {}) => {
    if (s.down) throw new TypeError('Failed to fetch');
    if (url === '/api/sync') return json(200, structuredClone(s.data));
    s.sent.push(`${init.method} ${url}`);
    return s.reply(init.method, url);
  };
  return s;
}

const settle = () => new Promise(r => setTimeout(r, 10));

test('offline edits wait in the queue and go out in order when the server is back', async () => {
  const srv = server();
  const storage = memoryStorage();
  const sync = createSync({ storage, fetchImpl: srv.fetch });
  await sync.init();
  await sync.syncNow();
  srv.down = true;

  await sync.enqueue('PUT', '/api/study/marks/7.1', { day: '2026-11-04' });
  await sync.enqueue('PUT', '/api/ideas/a', { title: 'x' });
  await settle();
  assert.deepEqual(sync.status(), { online: false, pending: 2, failed: [] });

  srv.down = false;
  srv.data = snapshot({ ideas: [{ id: 'a', title: 'x' }] });
  assert.equal(await sync.syncNow(), true);

  assert.deepEqual(srv.sent, ['PUT /api/study/marks/7.1', 'PUT /api/ideas/a']);
  assert.deepEqual(sync.status(), { online: true, pending: 0, failed: [] });
  assert.equal(sync.state.ideas[0].id, 'a');     // свежий снимок применён
});

test('queue and snapshot survive a page reload', async () => {
  const srv = server();
  const storage = memoryStorage();
  const first = createSync({ storage, fetchImpl: srv.fetch });
  await first.init();
  await first.syncNow();
  srv.down = true;
  first.state.ideas.push({ id: 'offline' });
  await first.enqueue('PUT', '/api/ideas/offline', { title: 'офлайн' });

  const reloaded = createSync({ storage, fetchImpl: srv.fetch });   // та же база браузера, новая страница
  await reloaded.init();

  assert.equal(reloaded.status().pending, 1);
  assert.equal(reloaded.state.ideas[0].id, 'offline');
});

test('rejected change (400) is reported and dropped; the rest still goes', async () => {
  const srv = server();
  srv.reply = (method, url) => url.includes('bad') ? json(400, { detail: 'Нельзя отметить будущий день' }) : json(204);
  const sync = createSync({ storage: memoryStorage(), fetchImpl: srv.fetch });
  await sync.init();
  srv.down = true;
  await sync.enqueue('PUT', '/api/study/marks/bad', {}, 'изучено 7.1');
  await sync.enqueue('PUT', '/api/study/marks/good', {});
  srv.down = false;

  await sync.syncNow();

  assert.deepEqual(srv.sent, ['PUT /api/study/marks/bad', 'PUT /api/study/marks/good']);
  assert.deepEqual(sync.status().failed, ['изучено 7.1: Нельзя отметить будущий день']);
  assert.equal(sync.status().pending, 0);
});

test('server error (500) stops the queue to keep the order; deleting what is already gone (404) is fine', async () => {
  const srv = server();
  let fail = true;
  srv.reply = (method, url) => url.endsWith('/second') && fail ? json(500) : method === 'DELETE' ? json(404) : json(204);
  const sync = createSync({ storage: memoryStorage(), fetchImpl: srv.fetch });
  await sync.init();
  srv.down = true;
  await sync.enqueue('DELETE', '/api/ideas/first');
  await sync.enqueue('PUT', '/api/ideas/second', {});
  await sync.enqueue('PUT', '/api/ideas/third', {});
  srv.down = false;

  assert.equal(await sync.syncNow(), false);
  assert.equal(sync.status().pending, 2);              // second и third ждут

  fail = false;
  assert.equal(await sync.syncNow(), true);
  assert.deepEqual(srv.sent, ['DELETE /api/ideas/first', 'PUT /api/ideas/second', 'PUT /api/ideas/second', 'PUT /api/ideas/third']);
});

test('fresh snapshot is not applied while local changes are still queued', async () => {
  const srv = server();
  const sync = createSync({ storage: memoryStorage(), fetchImpl: srv.fetch });
  await sync.init();
  await sync.syncNow();
  srv.reply = () => json(503);                        // сервер отвечает, но очередь не принимает
  sync.state.ideas.push({ id: 'local' });
  await sync.enqueue('PUT', '/api/ideas/local', {});
  await settle();

  srv.data = snapshot();                               // сервер ещё не знает об идее
  await sync.syncNow();

  assert.equal(sync.state.ideas.length, 1);            // локальная правка не откатилась
});

test('snapshot is replaced in place: modules keep the same state object', async () => {
  const srv = server();
  const sync = createSync({ storage: memoryStorage(), fetchImpl: srv.fetch });
  await sync.init();
  await sync.syncNow();
  const ref = sync.state;
  let replaced = 0;
  sync.onReplaced(() => replaced++);

  srv.data = snapshot({ ideas: [{ id: 'from-other-device' }] });
  await sync.syncNow();

  assert.equal(sync.state, ref);
  assert.equal(ref.ideas[0].id, 'from-other-device');
  assert.equal(replaced, 1);
});

test('uuidv7: valid format, version 7, time-ordered', async () => {
  const a = uuidv7();
  await new Promise(r => setTimeout(r, 2));
  const b = uuidv7();

  assert.match(a, /^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
  assert.ok(a < b);
});
