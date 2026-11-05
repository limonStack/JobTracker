import { test } from 'node:test';
import assert from 'node:assert/strict';
import { PHRASES, nextIndex } from '../../src/JobTracker.Api/wwwroot/js/motivation.js';

test('первый показ — случайная фраза из списка', () => {
  assert.equal(nextIndex(NaN, 10, () => 0.55), 5);
  assert.equal(nextIndex(undefined, 10, () => 0.999), 9);
});

test('дальше — следующая по кругу: повторов нет, пока список не пройден целиком', () => {
  const seen = new Set();
  let i = nextIndex(NaN);
  for (let k = 0; k < PHRASES.length; k++) { seen.add(PHRASES[i]); i = nextIndex(i); }
  assert.equal(seen.size, PHRASES.length);
  assert.equal(nextIndex(PHRASES.length - 1), 0);
});

test('фразы короткие и без повторов', () => {
  assert.equal(new Set(PHRASES).size, PHRASES.length);
  for (const p of PHRASES) assert.ok(p.length <= 90, p);
});
