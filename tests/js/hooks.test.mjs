// Хук Claude Code .claude/hooks/vacancy-link.mjs: подсказка про /interview-prep по ссылке на вакансию.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { findVacancy, findSection, hint } from '../../.claude/hooks/vacancy-link.mjs';

const files = ['<main><section data-kind="company"><h1 id="25-acme">25. ACME</h1></section></main>'];

test('находит вакансию DOU и слаг компании', () => {
  assert.deepEqual(findVacancy('глянь https://jobs.dou.ua/companies/acme/vacancies/100003/?from=rs пожалуйста'),
    { url: 'https://jobs.dou.ua/companies/acme/vacancies/100003/?from=rs', companySlug: 'acme' });
});

test('находит Djinni, Work.ua и robota.ua без слага компании', () => {
  assert.equal(findVacancy('https://djinni.co/jobs/712345-senior-net/').companySlug, null);
  assert.ok(findVacancy('https://www.work.ua/jobs/6123456/'));
  assert.ok(findVacancy('https://robota.ua/company123/vacancy987654'));
});

test('обычные ссылки — не вакансия', () => {
  assert.equal(findVacancy('https://jobs.dou.ua/companies/acme/'), null);
  assert.equal(findVacancy('https://github.com/x/y'), null);
  assert.equal(hint('как дела?', files), null);
});

test('skill уже вызван — подсказки нет', () => {
  assert.equal(hint('/interview-prep https://jobs.dou.ua/companies/acme/vacancies/1/', files), null);
});

test('подсказка называет готовый раздел компании', () => {
  const text = hint('https://jobs.dou.ua/companies/acme/vacancies/1/', files);
  assert.match(text, /interview-prep/);
  assert.match(text, /«25\. ACME» \(#25-acme\)/);
});

test('раздела нет — подсказка без него', () => {
  const text = hint('https://jobs.dou.ua/companies/acme/vacancies/1/', files);
  assert.match(text, /interview-prep/);
  assert.doesNotMatch(text, /уже есть/);
  assert.equal(findSection('acme', files), null);
});
