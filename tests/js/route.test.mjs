// Маршрут по «Карте подготовки»: порядок, диапазоны «9.14–9.19», пометка «новое», без повторов.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { routeFromCells } from '../../src/JobTracker.Api/wwwroot/js/route.js';

const T = text => ({ text }), Q = code => ({ code });
const all = () => true;

test('ссылки по порядку, диапазон разворачивается', () => {
  const r = routeFromCells([[T('Разделы '), T('9'), T(' и '), T('10'), T('; особенно '), Q('9.14'), T('–'), Q('9.17'), T(', '), Q('10.13')]], all);
  assert.deepEqual(r.map(x => x.code), ['9.14', '9.15', '9.16', '9.17', '10.13']);
  assert.ok(r.every(x => !x.isNew));
});

test('«новое» — до следующей точки с запятой', () => {
  const r = routeFromCells([[Q('12.8'), T('; новое — '), Q('12.26'), T(', '), Q('12.27'), T('; EF Core — '), Q('8.5')]], all);
  assert.deepEqual(r.map(x => [x.code, x.isNew]), [['12.8', false], ['12.26', true], ['12.27', true], ['8.5', false]]);
});

test('повторы и несуществующие вопросы пропускаются', () => {
  const r = routeFromCells([[Q('1.1'), T(', '), Q('1.2')], [Q('1.1'), T(', '), Q('99.1')]], c => c !== '99.1');
  assert.deepEqual(r.map(x => x.code), ['1.1', '1.2']);
});

test('дефис между ссылками разных разделов — не диапазон', () => {
  const r = routeFromCells([[Q('9.19'), T('–'), Q('10.2')]], all);
  assert.deepEqual(r.map(x => x.code), ['9.19', '10.2']);
});
