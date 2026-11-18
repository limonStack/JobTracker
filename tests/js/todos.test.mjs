import { test } from 'node:test';
import assert from 'node:assert/strict';
import { todayList, carriedLabel } from '../../src/JobTracker.Api/wwwroot/js/todos.js';

const T = (id, plannedOn, doneOn = null) => ({ id, title: id, plannedOn, doneOn });
const TODAY = '2026-11-18';

test('невыполненные с прошлых дней переходят на сегодня с пометкой, сделанные раньше — нет', () => {
  const list = todayList([
    T('old-open', '2026-11-15'),
    T('old-done', '2026-11-15', '2026-11-16'),
    T('today', TODAY),
    T('future', '2026-11-19'),
  ], TODAY);

  assert.deepEqual(list.map(t => [t.id, t.carried]), [['old-open', true], ['today', false]]);
});

test('сделанное сегодня остаётся в списке внизу, даже если записано раньше', () => {
  const list = todayList([
    T('b-done-today', '2026-11-10', TODAY),
    T('a-open', TODAY),
  ], TODAY);

  assert.deepEqual(list.map(t => [t.id, t.carried]), [['a-open', false], ['b-done-today', false]]);
});

test('внутри группы — по дню записи, потом по порядку добавления', () => {
  const list = todayList([T('0002', TODAY), T('0001', TODAY), T('0003', '2026-11-17')], TODAY);
  assert.deepEqual(list.map(t => t.id), ['0003', '0001', '0002']);
});

test('пометка переноса: «со вчера» и «с 15 ноя»', () => {
  assert.equal(carriedLabel('2026-11-17', TODAY), 'со вчера');
  assert.equal(carriedLabel('2026-11-15', TODAY), 'с 15 ноя');
  assert.equal(carriedLabel('2026-10-31', '2026-11-01'), 'со вчера');
});
