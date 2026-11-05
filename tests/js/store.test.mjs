// node --test: store.js переводит изменения в формате старого сайта в снимок и очередь (без сервера).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { loadStore } from '../../src/JobTracker.Api/wwwroot/js/store.js';

const zone = Intl.DateTimeFormat().resolvedOptions().timeZone;

/** Подмена sync.js: снимок в памяти, очередь — просто список. */
function fakeSync() {
  const queue = [];
  return {
    queue,
    state: {
      study: {
        today: '2026-11-04',
        marks: [{ code: '7.1', day: '2026-11-03' }, { code: '7.2', day: null }],
        activities: [{ kind: 'workout', day: '2026-11-03' }],
        reviews: [{ day: '2026-11-03', code: '7.1' }],
        // пояс как у машины теста: иначе store при загрузке сам отправит новый пояс
        settings: { dailyTheoryGoal: 10, weeklyWorkoutGoal: 3, calendarOpen: null, focusMode: false, timeZone: zone },
      },
      ideas: [{ id: 'i1', title: 'Outbox', note: '', project: '', question: '', status: 'new', createdOn: '2026-11-01', doneOn: null }],
      highlights: [{ id: 'h1', section: '07-architecture', text: 'SOLID', prefix: '', suffix: '', start: 10, color: 'yellow' }],
      applications: [],
    },
    enqueue(method, url, body) { queue.push([method, url, body]); },
  };
}

test('loads data in the old site format', () => {
  const store = loadStore(fakeSync());

  assert.deepEqual([...store.done], ['7.1', '7.2']);
  assert.deepEqual(store.doneAt, { '7.1': '2026-11-03' });     // без даты — в календаре не считается
  assert.deepEqual(store.review, { '2026-11-03': ['7.1'] });
  assert.ok(store.activityDays('workout').has('2026-11-03'));
  assert.equal(store.ideas[0].t, 'Outbox');
  assert.equal(store.highlights[0].end, 15);
});

test('marks: only the difference is queued, with the day of the change; snapshot follows', () => {
  const sync = fakeSync();
  const store = loadStore(sync);
  const { done, doneAt } = store;

  done.add('1.1'); doneAt['1.1'] = '2026-11-04';
  doneAt['7.1'] = '2026-11-02';
  done.delete('7.2');
  store.syncMarks(done, doneAt);
  store.syncMarks(done, doneAt);                                 // повтор без изменений — ничего

  assert.deepEqual(sync.queue, [
    ['PUT', '/api/study/marks/7.1', { day: '2026-11-02' }],
    ['PUT', '/api/study/marks/1.1', { day: '2026-11-04' }],
    ['DELETE', '/api/study/marks/7.2', undefined],
  ]);
  assert.deepEqual(sync.state.study.marks, [{ code: '7.1', day: '2026-11-02' }, { code: '1.1', day: '2026-11-04' }]);
});

test('new idea: PUT with its own id (no POST), status with its day; delete', () => {
  const sync = fakeSync();
  const store = loadStore(sync);
  const ideas = store.ideas;
  const id = store.newId();

  ideas.push({ id, d: '2026-11-04', t: 'CQRS', n: '', p: '', q: '', s: 'new', done: '' });
  store.syncIdeas(ideas);
  Object.assign(ideas.at(-1), { s: 'done', done: '2026-11-04' });
  store.syncIdeas(ideas);
  store.syncIdeas(ideas.filter(x => x.id !== 'i1'));

  assert.deepEqual(sync.queue, [
    ['PUT', `/api/ideas/${id}`, { title: 'CQRS', note: '', project: '', question: '', createdOn: '2026-11-04' }],
    ['PUT', `/api/ideas/${id}/status`, { status: 'done', day: '2026-11-04' }],
    ['DELETE', '/api/ideas/i1', undefined],
  ]);
  assert.deepEqual(sync.state.ideas.map(x => [x.id, x.status, x.doneOn]), [[id, 'done', '2026-11-04']]);
});

test('highlights: create, recolor, delete — all by id', () => {
  const sync = fakeSync();
  const store = loadStore(sync);
  const list = store.highlights;
  const id = store.newId();

  list.push({ id, sec: '05-linq', text: 'yield', prefix: 'a', suffix: 'b', start: 3, end: 8, color: 'green' });
  store.syncHighlights(list);
  list[0].color = 'pink';
  store.syncHighlights(list);
  store.syncHighlights([]);

  assert.deepEqual(sync.queue.map(([m, u]) => `${m} ${u}`), [
    `PUT /api/highlights/${id}`, 'PUT /api/highlights/h1', 'DELETE /api/highlights/h1', `DELETE /api/highlights/${id}`,
  ]);
  assert.equal(sync.queue[1][2].color, 'pink');
});

test('settings, activities and reviews update the snapshot and the queue', () => {
  const sync = fakeSync();
  const store = loadStore(sync);

  store.saveSettings({ calendarOpen: true });
  store.activityDays('ai').add('2026-11-04');
  store.setActivity('ai', '2026-11-04', true);
  store.review['2026-11-03'] = [];
  store.setReviewed('2026-11-03', '7.1', false);

  assert.deepEqual(sync.queue, [
    ['PUT', '/api/settings', { ...sync.state.study.settings }],
    ['PUT', '/api/study/activities/ai/2026-11-04', undefined],
    ['DELETE', '/api/study/reviews/2026-11-03/7.1', undefined],
  ]);
  assert.equal(sync.state.study.settings.calendarOpen, true);
  assert.ok(sync.state.study.activities.some(a => a.kind === 'ai' && a.day === '2026-11-04'));
  assert.deepEqual(sync.state.study.reviews, []);
});
