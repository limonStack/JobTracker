// node --test tests/js — store.js переводит изменения в формате старого сайта в запросы API.
// fetch подменён: тест проверяет, какие запросы уходят на сервер, без браузера и без сервера.
import { test, beforeEach } from 'node:test';
import assert from 'node:assert/strict';
import { loadStore } from '../../src/JobTracker.Api/wwwroot/js/store.js';

let calls, created;
const state = () => ({
  today: '2026-11-04',
  marks: [{ code: '7.1', day: '2026-11-03' }, { code: '7.2', day: null }],
  activities: [{ kind: 'workout', day: '2026-11-03' }],
  reviews: [{ day: '2026-11-03', code: '7.1' }],
  // пояс как у машины, где идёт тест: иначе store при загрузке сам отправит новый пояс на сервер
  settings: { dailyTheoryGoal: 10, weeklyWorkoutGoal: 3, calendarOpen: null, focusMode: false, timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone },
});

beforeEach(() => {
  calls = [];
  created = 0;
  globalThis.fetch = async (url, { method, body }) => {
    calls.push([method, url, body ? JSON.parse(body) : undefined]);
    const json = method !== 'GET' ? (url.endsWith('/api/ideas') || url.endsWith('/api/highlights') ? { id: `srv-${++created}` } : null)
      : url === '/api/study' ? state()
      : url === '/api/ideas' ? [{ id: 'i1', title: 'Outbox', note: '', project: '', question: '', status: 'new', createdOn: '2026-11-01', doneOn: null }]
      : [{ id: 'h1', section: '07-architecture', text: 'SOLID', prefix: '', suffix: '', start: 10, color: 'yellow' }];
    return { ok: true, status: json === null ? 204 : 200, json: async () => json };
  };
});

const settle = () => new Promise(r => setTimeout(r, 20));
const writes = () => calls.filter(([m]) => m !== 'GET');

test('loads marks in the old format: Set + doneAt without undated marks', async () => {
  const store = await loadStore();

  assert.deepEqual([...store.done], ['7.1', '7.2']);
  assert.deepEqual(store.doneAt, { '7.1': '2026-11-03' });
  assert.deepEqual(store.review, { '2026-11-03': ['7.1'] });
  assert.ok(store.activityDays('workout').has('2026-11-03'));
  assert.equal(store.ideas[0].t, 'Outbox');
  assert.equal(store.highlights[0].end, 15);
});

test('syncMarks sends only the difference', async () => {
  const store = await loadStore();
  const { done, doneAt } = store;

  done.add('1.1'); doneAt['1.1'] = '2026-11-04';     // новая
  doneAt['7.1'] = '2026-11-02';                       // перенесли день
  done.delete('7.2');                                  // сняли
  store.syncMarks(done, doneAt);
  await settle();

  assert.deepEqual(writes(), [
    ['PUT', '/api/study/marks/7.1', { day: '2026-11-02' }],
    ['PUT', '/api/study/marks/1.1', { day: '2026-11-04' }],
    ['DELETE', '/api/study/marks/7.2', undefined],
  ]);

  store.syncMarks(done, doneAt);                      // ничего не менялось — запросов нет
  await settle();
  assert.equal(writes().length, 3);
});

test('new idea: POST, then later edits go to the server id', async () => {
  const store = await loadStore();
  const ideas = store.ideas;

  ideas.push({ id: 'tmp', d: '2026-11-04', t: 'CQRS', n: '', p: '', q: '', s: 'new', done: '' });
  store.syncIdeas(ideas);
  ideas.at(-1).p = 'JobTracker';
  store.syncIdeas(ideas);
  ideas.at(-1).s = 'doing';
  store.syncIdeas(ideas);
  store.syncIdeas(ideas.filter(x => x.id !== 'i1'));   // удалили старую
  await settle();

  // каждое изменение уходит один раз, в своём виде; правки новой идеи — уже по id от сервера
  assert.deepEqual(writes(), [
    ['POST', '/api/ideas', { title: 'CQRS' }],
    ['PUT', '/api/ideas/srv-1', { title: 'CQRS', note: '', project: 'JobTracker', question: '' }],
    ['PUT', '/api/ideas/srv-1/status', { status: 'doing' }],
    ['DELETE', '/api/ideas/i1', undefined],
  ]);
});

test('highlights: create, recolor, delete', async () => {
  const store = await loadStore();
  const list = store.highlights;

  list.push({ id: 'tmp', sec: '05-linq', text: 'yield', prefix: 'a', suffix: 'b', start: 3, end: 8, color: 'green' });
  store.syncHighlights(list);
  list[0].color = 'pink';
  store.syncHighlights(list);
  store.syncHighlights([]);
  await settle();

  assert.deepEqual(writes().map(([m, u]) => `${m} ${u}`), [
    'POST /api/highlights',
    'PUT /api/highlights/h1',
    'DELETE /api/highlights/h1',
    'DELETE /api/highlights/srv-1',
  ]);
  assert.equal(writes()[1][2].color, 'pink');
});

test('settings: partial change sends full settings; activities and reviews go straight to the API', async () => {
  const store = await loadStore();

  store.saveSettings({ calendarOpen: true });
  store.setActivity('ai', '2026-11-04', true);
  store.setReviewed('2026-11-03', '7.1', false);
  await settle();

  assert.deepEqual(writes(), [
    ['PUT', '/api/settings', { ...state().settings, calendarOpen: true }],
    ['PUT', '/api/study/activities/ai/2026-11-04', undefined],
    ['DELETE', '/api/study/reviews/2026-11-03/7.1', undefined],
  ]);
});
