import { test } from 'node:test';
import assert from 'node:assert/strict';
import { PHRASES, phraseFor } from '../../src/JobTracker.Api/wwwroot/js/motivation.js';

test('в один день — одна и та же фраза', () => {
  assert.equal(phraseFor('2026-11-05'), phraseFor('2026-11-05'));
});

test('дни подряд не повторяются, пока список не пройден целиком', () => {
  const seen = new Set();
  const d = new Date(Date.UTC(2026, 0, 1));
  for (let i = 0; i < PHRASES.length; i++) {
    seen.add(phraseFor(d.toISOString().slice(0, 10)));
    d.setUTCDate(d.getUTCDate() + 1);
  }
  assert.equal(seen.size, PHRASES.length);
});

test('переход через месяц и год — следующая фраза по списку', () => {
  const i = key => PHRASES.indexOf(phraseFor(key));
  assert.equal((i('2026-12-31') + 1) % PHRASES.length, i('2027-01-01'));
  assert.equal((i('2027-02-28') + 1) % PHRASES.length, i('2027-03-01'));
});

test('фразы короткие и без повторов', () => {
  assert.equal(new Set(PHRASES).size, PHRASES.length);
  for (const p of PHRASES) assert.ok(p.length <= 90, p);
});
