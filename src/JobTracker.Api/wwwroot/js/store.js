// Данные пользователя для базы знаний. Читаются из локального снимка (sync.js), поэтому страница
// работает без сервера; изменения применяются к снимку и уходят в очередь на сервер.
// Старый код (knowledge.js) работает с данными в прежнем формате localStorage — здесь он переводится
// в формат API. Списки (идеи, выделения, отметки) сравниваются с последним сохранённым состоянием,
// и в очередь попадает только разница. Новые записи сразу получают настоящий id (UUID v7 из браузера).
import { uuidv7 } from './sync.js';

const today = () => { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`; };

const ideaFrom = x => ({ id: x.id, d: x.createdOn, t: x.title, n: x.note, p: x.project, q: x.question, s: x.status, done: x.doneOn || '' });
const ideaTo = x => ({ id: x.id, title: x.t, note: x.n, project: x.p, question: x.q, status: x.s, createdOn: x.d, doneOn: x.done || null });
const ideaKey = x => JSON.stringify([x.t, x.n, x.p, x.q]);
const hlFrom = h => ({ id: h.id, sec: h.section, text: h.text, prefix: h.prefix, suffix: h.suffix, start: h.start, end: h.start + h.text.length, color: h.color });
const hlBody = h => ({ section: h.sec, text: h.text, prefix: h.prefix, suffix: h.suffix, start: h.start, color: h.color });
const hlKey = h => JSON.stringify(hlBody(h));
const enc = encodeURIComponent;

export function loadStore(sync) {
  const snap = sync.state;
  const study = snap.study;
  const settings = study.settings;
  // «сегодня» сервер считает по часовому поясу пользователя — сообщаем ему пояс браузера
  const zone = Intl.DateTimeFormat().resolvedOptions().timeZone;
  if (zone && settings.timeZone !== zone) { settings.timeZone = zone; sync.enqueue('PUT', '/api/settings', { ...settings }, 'настройки'); }

  // ---- отметки «изучено»: Set + { код: день }, как в старом сайте ----
  const done = new Set(study.marks.map(m => m.code));
  const doneAt = Object.fromEntries(study.marks.filter(m => m.day).map(m => [m.code, m.day]));
  let savedMarks = new Map(study.marks.map(m => [m.code, m.day || null]));

  // ---- треки и повторение ----
  const activities = { workout: new Set(), ai: new Set() };
  for (const a of study.activities) activities[a.kind]?.add(a.day);
  const review = {};
  for (const r of study.reviews) (review[r.day] ||= []).push(r.code);
  const saveActivities = () => {
    sync.state.study.activities = Object.entries(activities).flatMap(([kind, days]) => [...days].map(day => ({ kind, day })));
  };
  const saveReviews = () => {
    sync.state.study.reviews = Object.entries(review).flatMap(([day, codes]) => codes.map(code => ({ day, code })));
  };

  // ---- идеи и выделения ----
  const ideas = snap.ideas.map(ideaFrom);
  let savedIdeas = new Map(ideas.map(x => [x.id, { key: ideaKey(x), s: x.s }]));
  const highlights = snap.highlights.map(hlFrom);
  let savedHighlights = new Map(highlights.map(h => [h.id, hlKey(h)]));

  return {
    study, settings, done, doneAt, review, ideas, highlights,
    newId: uuidv7,
    activityDays: kind => activities[kind],

    syncMarks(currentDone, currentAt) {
      const next = new Map([...currentDone].map(c => [c, currentAt[c] || null]));
      sync.state.study.marks = [...next].map(([code, day]) => ({ code, day }));
      // день отметки отправляем явно: офлайн-отметка за вторник уйдёт на сервер в среду, но останется вторником
      for (const [code, day] of next)
        if (!savedMarks.has(code) || savedMarks.get(code) !== day)
          sync.enqueue('PUT', `/api/study/marks/${enc(code)}`, { day: day ?? today() }, `изучено ${code}`);
      for (const code of savedMarks.keys())
        if (!next.has(code)) sync.enqueue('DELETE', `/api/study/marks/${enc(code)}`, undefined, `снять «изучено» ${code}`);
      savedMarks = next;
    },

    setActivity(kind, day, on) {
      saveActivities();
      sync.enqueue(on ? 'PUT' : 'DELETE', `/api/study/activities/${kind}/${day}`, undefined, `${kind === 'ai' ? 'ИИ' : 'тренировка'} ${day}`);
    },

    setReviewed(day, code, on) {
      saveReviews();
      sync.enqueue(on ? 'PUT' : 'DELETE', `/api/study/reviews/${day}/${enc(code)}`, undefined, `повторение ${code}`);
    },

    saveSettings(patch) {
      Object.assign(settings, patch);
      sync.enqueue('PUT', '/api/settings', { ...settings }, 'настройки');
    },

    syncIdeas(current) {
      sync.state.ideas = current.map(ideaTo);
      const next = new Map();
      for (const x of current) {
        const key = ideaKey(x), was = savedIdeas.get(x.id);
        next.set(x.id, { key, s: x.s });
        // PUT создаёт идею с этим id или обновляет; день записи — как в браузере
        if (!was || was.key !== key)
          sync.enqueue('PUT', `/api/ideas/${x.id}`, { title: x.t, note: x.n, project: x.p, question: x.q, createdOn: x.d }, `идея «${x.t}»`);
        if ((was?.s ?? 'new') !== x.s)
          sync.enqueue('PUT', `/api/ideas/${x.id}/status`, { status: x.s, day: x.done || today() }, `статус идеи «${x.t}»`);
      }
      for (const id of savedIdeas.keys())
        if (!next.has(id)) sync.enqueue('DELETE', `/api/ideas/${id}`, undefined, 'удалить идею');
      savedIdeas = next;
    },

    syncHighlights(current) {
      sync.state.highlights = current.map(h => ({ id: h.id, ...hlBody(h) }));
      const next = new Map();
      for (const h of current) {
        const key = hlKey(h);
        next.set(h.id, key);
        if (savedHighlights.get(h.id) !== key)
          sync.enqueue('PUT', `/api/highlights/${h.id}`, hlBody(h), 'выделение');
      }
      for (const id of savedHighlights.keys())
        if (!next.has(id)) sync.enqueue('DELETE', `/api/highlights/${id}`, undefined, 'снять выделение');
      savedHighlights = next;
    },
  };
}
