// База знаний: меню, поиск, карточка ответа, прогресс, календарь, режим собеседования, идеи и выделения.
// Логика перенесена из 300questions/ANSWERS.html как есть; вместо localStorage данные хранит сервер:
// store (store.js) отдаёт состояние, загруженное из API, и отправляет изменения обратно.
// Запускается после того, как render.js построил меню и тексты ответов из GET /api/content.
import { startDailyNote } from './motivation.js';

export function startKnowledge(store) {
  const side = document.getElementById('side');
  const input = document.getElementById('filter');
  const info = document.getElementById('search-info');
  const clearBtn = document.getElementById('clear-search');
  const chips = [...document.querySelectorAll('#side .chip[data-mark]')];
  const todoChip = document.getElementById('todo-chip');
  const card = document.getElementById('card');
  const groups = [...document.querySelectorAll('#side details')];

  const esc = s => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  const norm = s => s.toLowerCase().replace(/ё/g, 'е').replace(/[^\p{L}\p{N}#+.]+/gu, ' ').replace(/\s+/g, ' ').trim();

  // ---------- Раскладка: «пкзс» → «grpc», «byltrcs» → «индексы» ----------
  const EN = "qwertyuiop[]asdfghjkl;'zxcvbnm,.`";
  const RU = 'йцукенгшщзхъфывапролджэячсмитьбюё';
  const swap = (s, from, to) => [...s].map(c => { const i = from.indexOf(c); return i < 0 ? c : to[i]; }).join('');

  // ---------- Синонимы, сокращения, английские формулировки ----------
  // Ключ — слово/фраза запроса (для ключей длиннее 3 символов — начало слова), значение — что ещё искать
  const SYN = {
    'gc': ['сборщик мусора', 'сборк', 'garbage'], 'garbage collect': ['сборщик мусора', 'сборк', 'gc'], 'сборщик': ['gc', 'garbage'],
    'di': ['dependency injection', 'внедрени', 'зависимост'], 'ioc': ['dependency injection', 'di'],
    'dependency injection': ['di', 'внедрени'], 'внедрение зависимост': ['dependency injection', 'di'],
    'бд': ['баз', 'sql', 'postgres'], 'база данн': ['бд', 'sql'], 'database': ['баз', 'бд', 'sql'], 'db': ['баз', 'бд', 'sql'],
    'ef': ['entity framework', 'ef core'], 'entity framework': ['ef'], 'orm': ['ef core', 'orm'],
    'transaction': ['транзакц'], 'транзакц': ['transaction'], 'isolation': ['изоляц'], 'изоляц': ['isolation'],
    'index': ['индекс'], 'индекс': ['index'], 'lock': ['блокировк', 'lock'], 'блокировк': ['lock', 'deadlock'],
    'взаимоблокировк': ['deadlock'], 'race condition': ['гонк', 'race'], 'гонк': ['race condition'],
    'thread': ['поток', 'thread'], 'поток': ['thread'], 'thread pool': ['threadpool', 'пул'], 'threadpool': ['пул потоков'],
    'async': ['асинхрон', 'async'], 'асинхрон': ['async'], 'multithread': ['многопоточ', 'поток'], 'многопоточ': ['thread', 'parallel'],
    'parallel': ['параллел', 'parallel'], 'cancel': ['cancellationtoken', 'отмен'], 'отмен': ['cancellation'],
    'exception': ['исключени', 'exception'], 'исключени': ['exception'], 'error handling': ['обработк', 'ошибк', 'исключени'],
    'inheritance': ['наследован'], 'наследован': ['inheritance'], 'polymorphism': ['полиморфизм'], 'полиморф': ['polymorphism'],
    'encapsulation': ['инкапсуляц'], 'инкапсуляц': ['encapsulation'], 'abstraction': ['абстракц'], 'abstract class': ['абстрактн'],
    'interface': ['интерфейс'], 'интерфейс': ['interface'], 'delegate': ['делегат'], 'делегат': ['delegate'],
    'event': ['событи', 'event'], 'событи': ['event'], 'closure': ['замыкан'], 'замыкан': ['closure'], 'lambda': ['лямбд'], 'лямбд': ['lambda'],
    'generic': ['generic', 'обобщ'], 'обобщ': ['generic'], 'boxing': ['упаковк', 'boxing'], 'упаковк': ['boxing'],
    'value type': ['значим', 'value'], 'значим': ['value type', 'struct'], 'reference type': ['ссылочн'], 'ссылочн': ['reference'],
    'stack': ['стек', 'stack'], 'стек': ['stack'], 'heap': ['куч', 'heap'], 'куч': ['heap'],
    'memory leak': ['утечк'], 'leak': ['утечк'], 'утечк': ['leak'], 'memory': ['памят', 'memory'], 'памят': ['memory', 'gc'],
    'struct': ['структур', 'struct'], 'структур': ['struct'], 'record': ['record'], 'immutable': ['неизменя', 'immutable'], 'неизменя': ['immutable'],
    'collection': ['коллекц'], 'коллекц': ['collection', 'list', 'dictionary'], 'dictionary': ['dictionary', 'словар', 'хеш'],
    'словар': ['dictionary'], 'hash': ['хеш', 'hash'], 'хеш': ['hash'], 'string': ['string', 'строк'], 'строк': ['string'],
    'reflection': ['рефлекси'], 'рефлекси': ['reflection'], 'attribute': ['атрибут'], 'атрибут': ['attribute'],
    'serializ': ['сериализ'], 'сериализ': ['serializ', 'json'],
    'cache': ['кеш', 'cache'], 'caching': ['кеш'], 'кеш': ['cache', 'redis'], 'кэш': ['кеш', 'cache'],
    'queue': ['очеред', 'брокер'], 'очеред': ['queue', 'брокер', 'rabbitmq'], 'broker': ['брокер'], 'брокер': ['rabbitmq', 'kafka', 'broker'],
    'microservice': ['микросервис'], 'микросервис': ['microservice'], 'monolith': ['монолит'], 'монолит': ['monolith'],
    'load balanc': ['балансир', 'балансировщик'], 'балансир': ['load balanc'], 'rate limit': ['rate limiting', 'лимит'],
    'authentication': ['аутентификац'], 'аутентификац': ['authentication', 'jwt', 'oauth'], 'authorization': ['авторизац'], 'авторизац': ['authorization'],
    'security': ['безопасн'], 'безопасн': ['security', 'owasp'], 'vulnerab': ['атак', 'owasp', 'уязвим'], 'уязвим': ['owasp', 'атак'],
    'test': ['тест', 'test'], 'тест': ['test', 'unit'], 'mock': ['mock', 'мок'], 'мок': ['mock'],
    'performance': ['производительн'], 'производительн': ['performance', 'benchmark'], 'optimiz': ['оптимизац'], 'оптимизац': ['optimiz', 'производительн'],
    'design pattern': ['паттерн'], 'pattern': ['паттерн'], 'паттерн': ['pattern'], 'шаблон проектир': ['паттерн'],
    'clean code': ['чистый код'], 'чистый код': ['clean code'], 'clean architecture': ['чистая архитектур'], 'чистая архитектур': ['clean architecture'],
    'domain driven': ['ddd'], 'ddd': ['domain', 'агрегат'], 'architecture': ['архитектур'], 'архитектур': ['architecture'],
    'migration': ['миграц'], 'миграц': ['migration'], 'container': ['контейнер', 'docker'], 'контейнер': ['docker', 'container'],
    'cloud': ['облак', 'cloud', 'azure', 'aws'], 'облак': ['cloud', 'azure', 'aws'], 'branch': ['ветвлен', 'git'], 'ветк': ['git', 'branch'],
    'sharding': ['шардинг'], 'шардинг': ['sharding'], 'replication': ['репликац'], 'репликац': ['replication'],
    'consistency': ['консистентн', 'согласованн'], 'консистентн': ['consistency', 'cap'], 'idempoten': ['идемпотентн'], 'идемпотентн': ['idempoten'],
    'timeout': ['таймаут'], 'таймаут': ['timeout'], 'retry': ['retry', 'повтор'],
    'query': ['запрос', 'query'], 'запрос': ['query'], 'window function': ['оконн'], 'оконн': ['window', 'row_number'],
    'pagination': ['пагинац'], 'пагинац': ['pagination', 'offset', 'keyset'], 'join': ['join'],
    'tell me about yourself': ['о себе', 'поведенческ'], 'behavioral': ['поведенческ'], 'поведенческ': ['behavioral', 'star'],
    'salary': ['зарплат'], 'зарплат': ['salary'], 'english': ['английск'], 'английск': ['english'], 'interview': ['собеседован', 'интервью'],
    'algorithm': ['алгоритм'], 'алгоритм': ['algorithm'], 'complexity': ['сложност', 'big o'], 'big o': ['сложност'], 'сложност': ['big o'],
    'sort': ['сортиров'], 'сортиров': ['sort'], 'binary search': ['бинарн'], 'бинарн': ['binary'], 'palindrome': ['палиндром'], 'anagram': ['анаграм'],
    'linked list': ['linkedlist', 'связн'], 'ai': ['ии', 'llm', 'claude'], 'ии': ['ai', 'llm', 'claude'], 'llm': ['ии', 'llm'], 'prompt': ['промпт', 'prompt'],
    'scrum': ['scrum', 'agile'], 'agile': ['scrum', 'kanban'], 'code review': ['code review', 'ревью'], 'ревью': ['code review', 'review'],
  };
  const SYN_KEYS = Object.keys(SYN).sort((a, b) => b.length - a.length);

  // ---------- Пункты меню и прогресс ----------
  // отметки «изучено» — с сервера; save() отправляет только то, что изменилось
  let done = store.done;
  let doneAt = store.doneAt;                         // вопрос → дата отметки «изучено» (ГГГГ-ММ-ДД, местное время)
  const save = () => store.syncMarks(done, doneAt);
  const dayKey = (d = new Date()) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  startDailyNote(document.getElementById('daily-note'), () => dayKey());   // мысль дня под прогрессом

  const items = [];
  groups.forEach(d => {
    const summary = d.querySelector('summary');
    const secNum = +(summary.textContent.match(/^(\d+)\./) || [0, 0])[1];
    const section = norm(summary.textContent);
    const secProgress = document.createElement('span');
    secProgress.className = 'sec-progress';
    summary.appendChild(secProgress);
    d.secProgress = secProgress;
    d.querySelectorAll('li').forEach(li => {
      const a = li.querySelector('a');
      const raw = a.textContent.trim();
      const m = raw.match(/^(\d+)\.(\d+)\s+(.*)$/);
      const mark = (raw.match(/🔥|⭐|🎓/u) || [''])[0];
      const title = (m ? m[3] : raw).replace(/\s*(🔥|⭐|🎓)\s*$/u, '');
      const code = m ? `${m[1]}.${m[2]}` : '';
      const toggle = document.createElement('button');
      toggle.type = 'button';
      toggle.className = 'done-toggle';
      toggle.title = 'Отметить как изученный';
      toggle.addEventListener('click', e => { e.preventDefault(); toggleDone(code); });
      li.insertBefore(toggle, a);
      // значок важности — в отдельный элемент, чтобы режим «фокус» мог его спрятать
      a.innerHTML = a.innerHTML.replace(/\s*(🔥|⭐|🎓)\s*$/u, ' <span class="mark">$1</span>');
      const h3 = document.getElementById(a.getAttribute('href').slice(1));
      if (h3) h3.innerHTML = h3.innerHTML.replace(/\s*(🔥|⭐|🎓)\s*$/u, ' <span class="mark">$1</span>');
      let h3Badge = null, endBtn = null;
      if (h3) {
        // в заголовке — только метка «изучено»; отмечают кнопкой в конце ответа (дочитал — отметил)
        h3Badge = document.createElement('span');
        h3Badge.className = 'done-badge';
        h3Badge.textContent = '✓ изучено';
        h3.appendChild(h3Badge);
        let end = h3.nextElementSibling;
        while (end && !/^H[123]$/.test(end.tagName)) end = end.nextElementSibling;
        const bar = document.createElement('div');
        bar.className = 'done-end';
        endBtn = document.createElement('button');
        endBtn.type = 'button';
        endBtn.className = 'done-btn';
        endBtn.addEventListener('click', () => toggleDone(code));
        bar.appendChild(endBtn);
        h3.parentElement.insertBefore(bar, end);
      }
      items.push({ li, a, d, toggle, h3Badge, endBtn, ul: li.parentElement, order: items.length, mark, title, section, secNum,
                   id: a.getAttribute('href').slice(1), code, key: m ? +m[1] * 1000 + +m[2] : -1,
                   nTitle: norm(title), words: norm(title).split(' '), html: a.innerHTML,
                   body: null, score: 0, rank: 0 });
    });
  });
  const byCode = new Map(items.map(it => [it.code, it]));
  done = new Set([...done].filter(c => byCode.has(c)));

  let backDay = null;                                // «отмечать за прошедший день» — забыл отметить вчера; живёт до перезагрузки страницы
  function toggleDone(code) {
    if (done.has(code)) { done.delete(code); delete doneAt[code]; }
    else { done.add(code); doneAt[code] = backDay || dayKey(); }
    save();
    renderProgress();
    if (todoChip.getAttribute('aria-pressed') === 'true') apply();
    else if (cardItem && cardItem.code === code) showCard(cardItem);
  }

  function renderProgress() {
    const total = items.length, n = items.filter(it => done.has(it.code)).length;
    const pct = total ? Math.round(n * 100 / total) : 0;
    document.getElementById('progress-text').innerHTML =
      `<span>Изучено <b>${n}</b> из ${total}</span><b>${pct}%</b>`;
    document.getElementById('progress-fill').style.width = pct + '%';
    // темы за сегодня — всегда на виду, без открытия календаря
    const markDay = backDay || dayKey();
    const backLabel = backDay && `${+backDay.slice(8)} ${MONTHS_GEN[+backDay.slice(5, 7) - 1]}`;
    const todayN = items.filter(it => done.has(it.code) && doneAt[it.code] === markDay).length;
    const todayEl = document.getElementById('today-count');
    todayEl.innerHTML = `${backDay ? `✏️ За ${backLabel}` : 'Сегодня'} <b>${todayN}</b> / ${GOAL}${todayN >= GOAL ? ' ✓' : ''}`;
    todayEl.classList.toggle('met', todayN >= GOAL);
    todayEl.classList.toggle('backfill', !!backDay);
    // счётчики и полоски на кнопках важности: «0 из 161»
    todoChip.querySelector('.chip-count').textContent = total - n;   // сколько осталось
    for (const chip of chips) {
      const list = items.filter(it => it.mark === chip.dataset.mark);
      const k = list.filter(it => done.has(it.code)).length;
      chip.querySelector('.chip-count').textContent = `${k}/${list.length}`;
      chip.querySelector('.chip-fill').style.width = (list.length ? k * 100 / list.length : 0) + '%';
    }
    for (const it of items) {
      const isDone = done.has(it.code);
      it.li.classList.toggle('done', isDone);
      it.toggle.setAttribute('aria-pressed', isDone);
      it.toggle.textContent = isDone ? '✓' : '';
      it.toggle.title = isDone ? 'Снять отметку' : backDay ? `Отметить изученным за ${backLabel}` : 'Отметить как изученный';
      if (it.h3Badge) it.h3Badge.hidden = !isDone;
      if (it.endBtn) { it.endBtn.textContent = isDone ? '✓ Изучено' : backDay ? `☐ Отметить изученным за ${backLabel}` : '☐ Отметить изученным'; it.endBtn.classList.toggle('is-done', isDone); }
    }
    for (const d of groups) {
      const list = items.filter(it => it.d === d);
      const k = list.filter(it => done.has(it.code)).length;
      d.secProgress.textContent = list.length ? ` ${k}/${list.length}` : '';
      d.secProgress.classList.toggle('complete', list.length > 0 && k === list.length);
    }
    renderCalendar();
  }

  // ---------- Календарь активности: сколько новых тем выучено в каждый день ----------
  const calendar = document.getElementById('calendar');
  const calToggle = document.getElementById('cal-toggle');
    const MONTHS = ['Январь', 'Февраль', 'Март', 'Апрель', 'Май', 'Июнь', 'Июль', 'Август', 'Сентябрь', 'Октябрь', 'Ноябрь', 'Декабрь'];
  const MONTHS_GEN = ['января', 'февраля', 'марта', 'апреля', 'мая', 'июня', 'июля', 'августа', 'сентября', 'октября', 'ноября', 'декабря'];
  const plural = (n, one, few, many) => { const a = n % 10, b = n % 100; return a === 1 && b !== 11 ? one : a >= 2 && a <= 4 && (b < 12 || b > 14) ? few : many; };
  let calMonth = new Date(); calMonth.setDate(1);
  let calDay = null;                                  // выбранный день — список тем под календарём
  const GOAL = store.settings.dailyTheoryGoal;        // цель новых тем в день — из настроек (по умолчанию 10)

  function perDay() {
    const map = new Map();
    for (const [code, day] of Object.entries(doneAt))
      if (done.has(code) && byCode.has(code)) (map.get(day) || map.set(day, []).get(day)).push(byCode.get(code));
    return map;
  }

  // ---------- Тренировки и ИИ: отметка «было/не было» по дням (хранятся отдельно от учёбы) ----------
  function track(kind) {
    const days = store.activityDays(kind);
    return {
      has: key => days.has(key),
      toggle(key) {
        const on = !days.has(key);
        if (on) days.add(key); else days.delete(key);
        store.setActivity(kind, key, on);
      },
    };
  }
  const tracks = { gym: track('workout'), ai: track('ai') };
  const addDays = (date, n) => new Date(date.getFullYear(), date.getMonth(), date.getDate() + n);
  const mondayOf = date => addDays(date, -((date.getDay() + 6) % 7));
  // ---------- Повторение: на следующий день отмечаем, что вчерашние темы повторены (только для списка, без статистики) ----------
  let review = store.review;                          // день → темы, отмеченные повторёнными
  let reviewPop = null;                               // только что отмеченная тема — для анимации галочки
  let rvOpen = false;                                 // развёрнут ли список уже повторённых тем — переживает перерисовку календаря
  function toggleReview(day, code) {
    const set = new Set(review[day] || []);
    const on = !set.has(code);
    if (on) { set.add(code); reviewPop = code; } else set.delete(code);
    review[day] = [...set];
    const cutoff = dayKey(addDays(new Date(), -30));  // старые отметки не нужны (сервер удаляет их сам)
    for (const k of Object.keys(review)) if (k < cutoff || !review[k].length) delete review[k];
    store.setReviewed(day, code, on);
    renderCalendar();
  }

  function toggleTrack(name, key) {
    if (!key || key > dayKey()) return;               // будущие дни не отмечаем (ГГГГ-ММ-ДД сравнивается как дата)
    tracks[name].toggle(key);
    renderCalendar();
  }

  function renderCalendar() {
    if (!calendar) return;
    const days = perDay();
    const { gym, ai } = tracks;
    const y = calMonth.getFullYear(), m = calMonth.getMonth();
    const daysInMonth = new Date(y, m + 1, 0).getDate();
    const today = dayKey();
    // шкала от цели: меньше половины, почти, цель выполнена (ярко), перевыполнена в полтора раза
    const level = n => n === 0 ? 0 : n < GOAL / 2 ? 1 : n < GOAL ? 2 : n < GOAL * 1.5 ? 3 : 4;

    // строки — недели с понедельника; цвет — темы, дужка внизу: левая половина — тренировка, правая — ИИ
    let cells = '';
    for (let wk = mondayOf(new Date(y, m, 1)); wk <= new Date(y, m, daysInMonth); wk = addDays(wk, 7)) {
      for (let i = 0; i < 7; i++) {
        const date = addDays(wk, i);
        if (date.getMonth() !== m) { cells += '<span class="cal-day empty"></span>'; continue; }
        const key = dayKey(date), d = date.getDate();
        const n = (days.get(key) || []).length, g = gym.has(key), a = ai.has(key);
        cells += `<button type="button" class="cal-day l${level(n)}${key === today ? ' today' : ''}${key === calDay ? ' selected' : ''}${key > today ? ' future' : ''}" data-day="${key}"`
          + ` title="${d} ${MONTHS_GEN[m]}: ${n} ${plural(n, 'тема', 'темы', 'тем')} из ${GOAL}${n >= GOAL ? ' ✓' : ''}${g ? ' · тренировка' : ''}${a ? ' · ИИ' : ''}">`
          + `<span class="cal-num">${d}</span>${n ? `<span class="cal-count">${n}</span>` : ''}`
          + (g || a ? `<span class="cal-marks">${g ? '<i class="gym"></i>' : ''}${a ? '<i class="ai"></i>' : ''}</span>` : '') + `</button>`;
      }
    }

    // панель дня: выбранный день, а если ничего не выбрано — сегодня
    const target = calDay || today;
    const [ty, tm, td] = target.split('-').map(Number);
    const targetLabel = target === today ? 'Сегодня' : `${td} ${MONTHS_GEN[tm - 1]}${ty !== y ? ' ' + ty : ''}`;
    const targetN = (days.get(target) || []).length;
    const future = target > today;
    const trackBtn = (name, icon, label) => {
      const on = tracks[name].has(target);
      return `<button type="button" class="cal-track ${name}${on ? ' on' : ''}" data-track="${name}"${future ? ' disabled' : ''}`
        + ` title="${future ? 'Будущий день — отметить нельзя' : on ? 'Снять отметку' : 'Отметить'}">${icon} ${label}${on ? ' ✓' : ''}</button>`;
    };
    // список тем: выбранный день, а если ничего не выбрано — последний день учёбы до сегодня, чтобы повторить его перед новым
    const listDay = calDay || [...days.keys()].filter(k => k < today).sort().pop();
    const dayList = listDay && (days.get(listDay) || []).slice().sort((a, b) => a.key - b.key);
    let listHtml = '', reviewHtml = '';                // повторение — под сеткой месяца; обычный список — внизу
    // день, за который сейчас дозаписываем темы, — обычный список, а не «повторить»
    if (listDay && listDay < today && listDay !== backDay && dayList.length) {
      const got = new Set(review[listDay] || []);
      const k = dayList.filter(it => got.has(it.code)).length, all = k === dayList.length;
      const [, lm, ld] = listDay.split('-').map(Number);
      reviewHtml = `<div class="cal-list review${all ? ' all' : ''}">` +
        `<div class="cal-list-head"><span>${all ? '🎉 Повторено — можно учить новое' : `🔁 Повторить · ${ld} ${MONTHS_GEN[lm - 1]}`}</span><span class="rv-count">${k}/${dayList.length}</span></div>` +
        `<div class="cal-goal-bar rv-bar"><i style="width:${k * 100 / dayList.length}%"></i></div>` +
        // всё повторено — список не пропадает, а сворачивается: темы дня можно открыть и пройтись по ним ещё раз
        (all ? `<details class="rv-done"${rvOpen ? ' open' : ''}><summary>Показать темы</summary>` : '') +
        dayList.map(it => { const on = got.has(it.code);
          return `<div class="rv-item${on ? ' on' : ''}"><button type="button" class="rv-check${on && it.code === reviewPop ? ' pop' : ''}" data-review="${listDay}|${it.code}" aria-pressed="${on}" title="${on ? 'Снять отметку' : 'Повторил'}">✓</button>` +
            `<a href="#${it.id}">${it.code} ${esc(it.title)}</a></div>`; }).join('') + (all ? '</details>' : '') + '</div>';
    } else if (calDay) {
      listHtml = `<div class="cal-list"><div class="cal-list-head">Темы за ${targetLabel.toLowerCase()}</div>` +
        (dayList.length ? '' : `<div class="cal-list-sub">темы не отмечены</div>`) +
        dayList.map(it => `<a href="#${it.id}">${it.code} ${esc(it.title)}</a>`).join('') + '</div>';
    }
    reviewPop = null;

    calendar.innerHTML =
      `<div class="cal-head"><button type="button" class="cal-nav" data-nav="-1" title="Предыдущий месяц">◀</button>` +
      `<span class="cal-title">${MONTHS[m]} ${y}</span>` +
      `<button type="button" class="cal-nav" data-nav="1" title="Следующий месяц">▶</button>` +
      `<button type="button" class="cal-close" title="Закрыть" aria-label="Закрыть календарь">✕</button></div>` +
      `<div class="cal-today">` +
        `<div class="cal-today-head"><span>${targetLabel}${backDay === target ? ' · отмечаю темы' : ''}</span>` +
          // карандаш у прошедшего дня: галочки «изучено» пишутся на этот день, пока не нажать ещё раз
          (target < today ? `<button type="button" class="cal-backfill${backDay === target ? ' on' : ''}" data-backfill="${target}" aria-pressed="${backDay === target}"` +
            ` title="${backDay === target ? 'Снова отмечать темы сегодняшним днём' : `Отмечать изученные темы за ${targetLabel}`}">✏️</button>` : '') + `</div>` +
        `<div class="cal-goal${targetN >= GOAL ? ' met' : ''}"><div class="cal-goal-text">📚 Теория <b>${targetN}</b> / ${GOAL}` +
          (targetN >= GOAL ? ' · цель выполнена ✓' : future ? '' : ` · ещё ${GOAL - targetN}`) +
          `</div><div class="cal-goal-bar"><i style="width:${Math.min(100, targetN * 100 / GOAL)}%"></i></div></div>` +
        `<div class="cal-tracks">${trackBtn('gym', '💪', 'Тренировка')}${trackBtn('ai', '🤖', 'ИИ')}</div>` +
      `</div>` +
      `<div class="cal-week">${['Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб', 'Вс'].map(w => `<span>${w}</span>`).join('')}</div>` +
      `<div class="cal-grid">${cells}</div>` +
      `<div class="cal-legend"><span>темы: <i class="l1"></i>1–${GOAL / 2 - 1} <i class="l2"></i>${GOAL / 2}–${GOAL - 1} <i class="l3"></i>${GOAL}+ ✓ <i class="l4"></i>${GOAL * 1.5}+</span>` +
      `<span><i class="mark gym"></i>тренировка <i class="mark ai"></i>ИИ</span></div>` +
      reviewHtml +
      listHtml;
  }

  if (calendar) {
    calendar.addEventListener('click', e => {
      const t = e.target;
      if (t.closest('[data-review]')) { const [day, code] = t.closest('[data-review]').dataset.review.split('|'); toggleReview(day, code); }
      else if (t.closest('[data-track]')) toggleTrack(t.closest('[data-track]').dataset.track, calDay || dayKey());
      else if (t.closest('[data-backfill]')) { const k = t.closest('[data-backfill]').dataset.backfill; backDay = backDay === k ? null : k; renderProgress(); }
      else if (t.closest('[data-nav]')) { calMonth.setMonth(calMonth.getMonth() + +t.closest('[data-nav]').dataset.nav); renderCalendar(); }
      else if (t.closest('[data-day]')) { const k = t.closest('[data-day]').dataset.day; calDay = calDay === k ? null : k; renderCalendar(); }
      else if (t.closest('.cal-close')) setCalendar(false);
    });
    calendar.addEventListener('toggle', e => { if (e.target.classList.contains('rv-done')) rvOpen = e.target.open; }, true);
    // выбор пользователя хранится в его настройках на сервере — одинаков на всех устройствах
    const setCalendar = (open, remember = true) => {
      calendar.hidden = !open;
      calToggle.setAttribute('aria-pressed', open);
      if (remember) store.saveSettings({ calendarOpen: open });
    };
    calToggle.addEventListener('click', () => setCalendar(calendar.hidden));
    document.getElementById('today-count').addEventListener('click', () => setCalendar(true));
    // клик по тексту календарь не закрывает (мешало читать) — только ✕ или кнопка календаря
    // по умолчанию открыт на широком экране (есть свободное место справа), на узком — по кнопке
    const pref = store.settings.calendarOpen;
    setCalendar(pref ?? window.innerWidth >= 1760, false);
  }

  // ---------- Текст ответа, подсказки, карточка ----------
  function* answerNodes(it) {
    for (let n = document.getElementById(it.id)?.nextElementSibling; n && !/^H[123]$/.test(n.tagName); n = n.nextElementSibling)
      if (!n.classList.contains('done-end')) yield n;   // кнопку «Изучено» в конце ответа не ищем и не показываем в карточке
  }

  function bodyOf(it) {
    if (it.body !== null) return it.body;
    let text = '';
    for (const n of answerNodes(it)) text += ' ' + n.textContent;
    return (it.body = norm(text));
  }

  const listText = n => n.tagName === 'UL' || n.tagName === 'OL'
    ? [...n.children].map(li => li.textContent.trim()).join('; ')
    : n.textContent.trim();
  const isHeading = t => t.length < 60 && /:$/.test(t);

  // Карточка: «Коротко», «Ответ», «Почему», «Ловушка», «Как ответить» — без кода и длинных разборов
  const CARD_LABELS = [/^Коротко:/, /^Ответ:/, /^Почему:/, /^(Как (избежать|исправить|решить|правильно)|Решени[ея]|Правильно)[^:]{0,40}:/,
                       /^(Ловушк[аи]|Подвох)[^:]{0,40}:/, /^Как ответить[^:]{0,40}:/];
  function cardBody(it) {
    const nodes = [...answerNodes(it)];
    const picked = [];
    for (const re of CARD_LABELS) {
      const i = nodes.findIndex(n => n.tagName === 'P' && re.test(n.textContent.trim()));
      if (i < 0 || picked.includes(nodes[i])) continue;
      picked.push(nodes[i]);
      const after = nodes[i].textContent.trim().replace(re, '').trim();
      const next = nodes[i + 1];
      if (next && !picked.includes(next) && ((!after || /:$/.test(after)) && /^(UL|OL|TABLE|BLOCKQUOTE)$/.test(next.tagName)
          || /^(UL|OL)$/.test(next.tagName) && next.children.length <= 8)) picked.push(next);   // короткий список сразу после — тоже в карточку
    }
    if (!picked.length) {
      const first = nodes.find(n => n.tagName !== 'FIGURE' && n.textContent.trim() && !isHeading(n.textContent.trim()));
      if (first) {
        picked.push(first);
        const next = first.nextElementSibling;
        if (next && /^(UL|OL)$/.test(next.tagName)) picked.push(next);
      }
    }
    picked.sort((x, y) => nodes.indexOf(x) - nodes.indexOf(y));
    return picked.map(n => n.cloneNode(true).outerHTML).join('');
  }

  let cardItem = null, cardSuppressed = false;
  function showCard(it) {
    if (!it || cardSuppressed) { hideCard(); return; }
    cardItem = it;
    const isDone = done.has(it.code);
    card.innerHTML =
      `<div class="card-head"><span class="card-code">${it.code}</span><span class="card-title">${esc(it.title)}${it.mark ? ' <span class="mark">' + it.mark + '</span>' : ''}</span>` +
      `<button type="button" class="card-close" title="Закрыть (Esc)" aria-label="Закрыть">✕</button></div>` +
      `<div class="card-body">${cardBody(it) || '<p>Откройте вопрос, чтобы увидеть ответ.</p>'}</div>` +
      `<div class="card-foot"><button type="button" class="card-done${isDone ? ' is-done' : ''}">${isDone ? '✓ Изучено' : '☐ Отметить изученным'}</button>` +
      `<span>Клик или Enter — открыть полностью · ↑↓ — другой вопрос · Esc — закрыть</span></div>`;
    card.querySelector('.card-close').addEventListener('click', () => { cardSuppressed = true; hideCard(); input.focus(); });
    card.querySelector('.card-done').addEventListener('click', () => toggleDone(it.code));
    card.hidden = false;
    card.scrollTop = 0;
  }
  function hideCard() { card.hidden = true; cardItem = null; }

  // ---------- Разбор запроса ----------
  const NUM = '(?:#|q|№)?\\s*';
  const keyMatches = (key, word) => key.length <= 3 ? word === key : word.startsWith(key);

  function parse(raw) {
    const t = raw.trim().replace(/\.$/, '');
    let m;
    if ((m = t.match(new RegExp(`^${NUM}(\\d+)\\.(\\d+)\\s*[-–—]\\s*(?:(\\d+)\\.)?(\\d+)$`, 'i')))) {
      const lo = +m[1] * 1000 + +m[2], hi = +(m[3] ?? m[1]) * 1000 + +m[4];
      return { test: it => it.key >= Math.min(lo, hi) && it.key <= Math.max(lo, hi) };
    }
    if (/^(?:(?:#|q|№)?\d+(?:\.\d+)?[\s,;]+)+(?:#|q|№)?\d+(?:\.\d+)?$/i.test(t)) {
      const parts = t.match(/\d+(?:\.\d+)?/g);
      return { test: it => parts.some(p => p.includes('.') ? p === it.code : +p === it.secNum) };
    }
    if ((m = t.match(new RegExp(`^${NUM}(\\d+)\\.(\\d+)$`, 'i'))))
      return { test: it => it.code === `${+m[1]}.${+m[2]}` };
    if ((m = t.match(new RegExp(`^${NUM}(\\d+)$`, 'i'))))
      return { test: it => it.secNum === +m[1] };

    // 1) раскладка — пословно, до нормализации («;» на английской раскладке — это «ж»)
    const notes = [];
    const fixedWords = [];
    let layoutChanged = false;
    for (const rawTok of t.toLowerCase().split(/\s+/).filter(Boolean)) {
      const variants = [rawTok, swap(rawTok, EN, RU), swap(rawTok, RU, EN)].map(norm).filter(Boolean);
      if (!variants.length) continue;
      const best = pickVariant(variants);
      if (best !== variants[0]) layoutChanged = true;
      fixedWords.push(...best.split(' '));
    }
    if (layoutChanged) notes.push(`раскладка: «${fixedWords.join(' ')}»`);

    // 2) фразы и синонимы: «garbage collector» → сборщик мусора, «di» → внедрение зависимостей
    let words = fixedWords.filter(w => w.length > 1 || /\d/.test(w));
    if (!words.length) words = fixedWords;
    const tokens = [];
    for (let i = 0; i < words.length; i++) {
      let used = 1, alts = [words[i]];
      for (const key of SYN_KEYS) {
        const kw = key.split(' ');
        if (kw.length > words.length - i) continue;
        const slice = words.slice(i, i + kw.length);
        if (kw.every((k, j) => j < kw.length - 1 ? slice[j] === k : keyMatches(k, slice[j]))) {
          used = kw.length;
          alts = [slice.join(' '), ...SYN[key]];
          break;
        }
      }
      tokens.push({ text: words.slice(i, i + used).join(' '), alts: [...new Set(alts)] });
      i += used - 1;
    }

    // 3) опечатки: слово, которого нет нигде, — ищем похожее в названиях (одна-две ошибки)
    for (const tok of tokens) {
      if (tok.text.length < 4 || /\d/.test(tok.text) || tok.text.includes(' ')) continue;
      if (items.some(it => tok.alts.some(a => it.nTitle.includes(a) || it.section.includes(a) || bodyOf(it).includes(a)))) continue;
      const near = fuzzy(tok.text);
      if (near.length) { tok.alts.push(...near); notes.push(`опечатка: «${tok.text}» → «${near[0]}»`); }
    }

    return { tokens, tooShort: tokens.map(x => x.text).join('').length < 2, notes };
  }

  // Слово меняем на другую раскладку, только если набранное не встречается НИГДЕ
  const matchesAll = (parts, field) => items.filter(it => parts.every(p => field(it).includes(p) || it.code === p)).length;
  function pickVariant(variants) {
    const unique = [...new Set(variants)];
    if (unique.length === 1) return unique[0];
    const parts = v => v.split(' ').filter(p => p.length > 1 || /\d/.test(p));
    const inTitles = v => parts(v).length ? matchesAll(parts(v), it => it.nTitle + ' ' + it.section) : 0;
    const anywhere = v => parts(v).length ? matchesAll(parts(v), it => it.nTitle + ' ' + it.section + ' ' + bodyOf(it)) : 0;
    if (inTitles(unique[0]) > 0 || anywhere(unique[0]) > 0) return unique[0];
    const alternatives = unique.slice(1);
    const scores = alternatives.map(v => inTitles(v) * 1000 + anywhere(v));
    const best = Math.max(...scores);
    return best > 0 ? alternatives[scores.indexOf(best)] : unique[0];
  }

  // Нечёткий поиск по словам названий: расстояние Дамерау–Левенштейна (1 ошибка, для длинных слов — 2)
  let vocab = null;
  function osa(a, b) {
    const d = Array.from({ length: a.length + 1 }, (_, i) => [i, ...Array(b.length).fill(0)]);
    for (let j = 1; j <= b.length; j++) d[0][j] = j;
    for (let i = 1; i <= a.length; i++)
      for (let j = 1; j <= b.length; j++) {
        const cost = a[i - 1] === b[j - 1] ? 0 : 1;
        d[i][j] = Math.min(d[i - 1][j] + 1, d[i][j - 1] + 1, d[i - 1][j - 1] + cost);
        if (i > 1 && j > 1 && a[i - 1] === b[j - 2] && a[i - 2] === b[j - 1]) d[i][j] = Math.min(d[i][j], d[i - 2][j - 2] + 1);
      }
    return d[a.length][b.length];
  }
  function fuzzy(word) {
    vocab ??= [...new Set(items.flatMap(it => (it.nTitle + ' ' + it.section).split(' ')).filter(w => w.length >= 4))];
    const max = word.length >= 8 ? 2 : 1;
    return vocab
      .map(w => ({ w, d: Math.min(osa(word, w), w.length > word.length ? osa(word, w.slice(0, word.length)) : Infinity) }))
      .filter(x => x.d <= max)
      .sort((x, y) => x.d - y.d || x.w.length - y.w.length)
      .slice(0, 3)
      .map(x => x.w);
  }

  // ---------- Оценка совпадения и ранжирование ----------
  // короткие сокращения («di», «gc», «ef») — только целым словом, иначе «di» найдётся в «distributed»
  const hasAlt = (text, a) => a.length <= 2 ? (' ' + text + ' ').includes(' ' + a + ' ') : text.includes(a);
  const has = (text, tok) => tok.alts.some(a => hasAlt(text, a));
  function score(it, p) {
    if (p.test) return p.test(it) ? 3 : 0;
    const tok = p.tokens;
    if (tok.every(t => has(it.nTitle, t) || it.code === t.text)) return 3;
    if (tok.every(t => has(it.nTitle, t) || it.section.includes(t.text) || has(it.section, t) || it.code === t.text)) return 2;
    if (tok.map(t => t.text).join('').length >= 3 && tok.every(t => has(it.nTitle, t) || has(it.section, t) || has(bodyOf(it), t))) return 1;
    return 0;
  }
  // точность внутри одинаковой оценки: слово целиком > начало слова > подстрока; короче название — выше
  function precision(it, p) {
    if (!p.tokens) return 0;
    let bonus = 0;
    for (const t of p.tokens) {
      let best = 0;                                   // лучший из синонимов, а не первый совпавший
      for (const a of t.alts) {
        let b = 0;
        if (a.includes(' ') ? it.nTitle.includes(a) : it.words.includes(a)) b = a === t.text ? 5 : 4;   // слово или фраза целиком
        else if (!a.includes(' ') && it.words.some(w => w.startsWith(a))) b = 2;                        // начало слова
        else if (hasAlt(it.nTitle, a)) b = 1;                                                            // подстрока
        best = Math.max(best, b);
      }
      bonus += best;
    }
    if (p.tokens.some(t => t.alts.some(a => it.nTitle.startsWith(a)))) bonus += 1;
    if (it.title.startsWith('Подвох')) bonus -= 0.5;          // при прочих равных — сначала основной вопрос
    return bonus - it.title.length / 500;
  }

  function run(p, marks, todoOnly) {
    let hits = 0, deep = 0;
    for (const it of items) {
      const filtered = (marks.length && !marks.includes(it.mark)) || (todoOnly && done.has(it.code));
      it.score = filtered ? 0 : (p.tokens && !p.tokens.length ? 3 : score(it, p));
      it.rank = it.score ? it.score * 100 + precision(it, p) : 0;
      if (it.score >= 2) hits++; else if (it.score === 1) deep++;
    }
    return { hits, deep };
  }

  function highlight(text, needles) {
    const low = text.toLowerCase().replace(/ё/g, 'е');
    const ranges = [];
    for (const n of needles)
      for (let i = low.indexOf(n); n && i >= 0; i = low.indexOf(n, i + n.length)) ranges.push([i, i + n.length]);
    ranges.sort((a, b) => a[0] - b[0]);
    const merged = [];
    for (const r of ranges) {
      const last = merged[merged.length - 1];
      if (last && r[0] <= last[1]) last[1] = Math.max(last[1], r[1]); else merged.push([...r]);
    }
    let out = '', pos = 0;
    for (const [s, e] of merged) { out += esc(text.slice(pos, s)) + '<mark>' + esc(text.slice(s, e)) + '</mark>'; pos = e; }
    return out + esc(text.slice(pos));
  }

  const nav = groups[0].parentElement;
  function reorder(byRank) {
    const lists = new Map();
    for (const it of items) (lists.get(it.ul) || lists.set(it.ul, []).get(it.ul)).push(it);
    for (const [ul, list] of lists) {
      list.sort((x, y) => (byRank ? y.rank - x.rank : 0) || x.order - y.order);
      list.forEach(it => ul.appendChild(it.li));
    }
    const best = d => Math.max(0, ...items.filter(it => it.d === d).map(it => it.rank));
    groups.map((d, i) => ({ d, i, best: byRank ? best(d) : 0 }))
      .sort((x, y) => y.best - x.best || x.i - y.i)
      .forEach(({ d }) => nav.appendChild(d));
  }

  // ---------- Применение поиска ----------
  let active = -1;
  const visible = () => [...document.querySelectorAll('#side details:not([hidden]) li:not([hidden])')]
    .map(li => items.find(it => it.li === li));

  // ---------- Фильтры 🔥 ⭐ 🎓 ☐ скрывают лишние ответы и в тексте, не только в меню ----------
  // пересчитываем только при смене фильтра: отметка «Изучено» под «☐ не изучено» не прячет ответ, который сейчас читаешь
  let answersKey = '';
  function filterAnswers(marks, todoOnly) {
    const key = marks.join() + (todoOnly ? '|todo' : '');
    if (key === answersKey) return;
    answersKey = key;
    const on = key !== '';
    const out = new Set();
    for (const it of items) {
      const h3 = document.getElementById(it.id);
      if (h3 && on && ((marks.length && !marks.includes(it.mark)) || (todoOnly && done.has(it.code)))) out.add(h3);
    }
    // ответ — заголовок и всё до следующего заголовка; подзаголовок группы — вместе с текстом до первого вопроса;
    // раздел без подходящих вопросов (и шпаргалка без вопросов) скрывается целиком
    const ids = new Set(items.map(it => it.id));
    for (const sec of document.querySelectorAll('main > section')) {
      let head = [], anyInHead = false, anyInSec = false, inAnswer = false, hide = false;
      const closeHead = () => head.forEach(n => n.classList.toggle('flt-out', on && !anyInHead));
      for (const n of sec.children) {
        if (n.tagName === 'H1' || n.tagName === 'H2') { closeHead(); head = n.tagName === 'H2' ? [n] : []; anyInHead = inAnswer = false; }
        else if (n.tagName === 'H3') {
          inAnswer = true;
          hide = on && (out.has(n) || !ids.has(n.id));
          n.classList.toggle('flt-out', hide);
          if (!hide) anyInHead = anyInSec = true;
        }
        else if (inAnswer) n.classList.toggle('flt-out', hide);
        else head.push(n);
      }
      closeHead();
      sec.classList.toggle('flt-out', on && !anyInSec);
    }
  }

  function apply() {
    const raw = input.value;
    const marks = chips.filter(c => c.getAttribute('aria-pressed') === 'true').map(c => c.dataset.mark);
    const todoOnly = todoChip.getAttribute('aria-pressed') === 'true';
    let p = parse(raw);
    const tooShort = p.tokens && p.tooShort && raw.trim() !== '';
    const filtering = (raw.trim() !== '' && !tooShort) || marks.length > 0 || todoOnly;
    const notes = !tooShort && p.notes ? p.notes : [];
    if (tooShort) p = { tokens: [] };
    const res = run(p, marks, todoOnly);
    filterAnswers(marks, todoOnly);

    active = -1;
    clearBtn.hidden = raw === '';
    side.classList.toggle('searching', filtering);
    const needles = p.tokens ? p.tokens.flatMap(t => t.alts) : [];
    for (const it of items) {
      const focusHide = document.body.classList.contains('focus-mode') && res.hits > 0 && it.score === 1;
      const show = (!filtering || it.score > 0) && !focusHide;
      it.li.hidden = !show;
      it.li.classList.toggle('body-hit', filtering && it.score === 1);
      it.li.classList.remove('active');
      it.a.innerHTML = filtering && it.score >= 2 && needles.length
        ? `${it.code} ${highlight(it.title, needles)}${it.mark ? ' <span class="mark">' + it.mark + '</span>' : ''}`
        : it.html;
    }
    const byText = raw.trim() !== '' && !tooShort;
    // 🔥 ⭐ 🎓 без поиска текстом: полностью изученные разделы скрыты (в режиме собеседования — нет)
    const hideDone = marks.length > 0 && !byText && !document.body.classList.contains('focus-mode');
    reorder(byText);                      // по релевантности — только при поиске текстом; фильтры 🔥 ⭐ 🎓 ☐ — по номерам
    for (const d of groups) {
      const shown = [...d.querySelectorAll('li')].filter(li => !li.hidden);
      const any = shown.length > 0 && !(hideDone && shown.every(li => li.classList.contains('done')));
      d.hidden = filtering && !any;
      d.open = filtering && any;
    }

    if (tooShort) info.textContent = 'Введите хотя бы 2 символа, номер вопроса (4.7) или раздела (4)';
    else if (!filtering) info.textContent = '';
    else if (!res.hits && !res.deep) info.textContent = 'Ничего не найдено — попробуйте другое слово или номер';
    else info.textContent = `Найдено: ${res.hits}` + (res.deep ? ` · ещё ${res.deep} в тексте ответов` : '') + (notes.length ? ` · ${notes.join(' · ')}` : '');
    info.classList.toggle('nothing', !tooShort && filtering && !res.hits && !res.deep);   // в режиме «фокус» видна только эта строка

    // карточка лучшего результата — сразу, без перехода
    const list = visible();
    if (filtering && raw.trim() && list.length) {
      active = list.indexOf(list.reduce((b, x) => (!b || x.rank > b.rank ? x : b), null));
      setActive(list, false);
      showCard(list[active]);
    } else hideCard();
  }

  function setActive(list, scroll = true) {
    items.forEach(it => it.li.classList.remove('active'));
    const it = list[active];
    if (!it) return;
    it.li.classList.add('active');
    if (scroll) it.li.scrollIntoView({ block: 'nearest' });
  }

  function go(it) {
    const target = document.getElementById(it.id);
    if (location.hash === '#' + it.id) target?.scrollIntoView({ block: 'start' });   // тот же адрес — прокрутить вручную
    else location.hash = it.id;
    document.body.classList.remove('open');
    cardSuppressed = true;
    hideCard();
    input.focus({ preventScroll: true });          // ↑↓ сразу снова листают результаты
  }

  // ↑↓ — листать результаты, Enter — открыть вопрос из карточки полностью. Работает, где бы ни был фокус.
  let lastKeyNav = 0;
  function navKey(e) {
    const list = visible();
    if (!input.value.trim() || !list.length) return false;
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      lastKeyNav = performance.now();
      active = active < 0 ? 0 : e.key === 'ArrowDown' ? Math.min(active + 1, list.length - 1) : Math.max(active - 1, 0);
      setActive(list);
      cardSuppressed = false;
      showCard(list[active]);
      return true;
    }
    if (e.key === 'Enter') {
      const it = !card.hidden && cardItem ? cardItem
        : active >= 0 ? list[active] : list.reduce((b, x) => (!b || x.rank > b.rank ? x : b), null);
      if (it) { e.preventDefault(); go(it); return true; }
    }
    return false;
  }

  input.addEventListener('input', () => { cardSuppressed = false; apply(); });
  input.addEventListener('keydown', e => {
    if (navKey(e)) return;
    if (e.key === 'Escape') {
      if (!card.hidden && input.value) { cardSuppressed = true; hideCard(); }
      else { input.value = ''; cardSuppressed = false; apply(); }
    }
  });

  // клик в любое место карточки — открыть вопрос полностью (кроме кнопок и ссылок внутри)
  card.addEventListener('click', e => {
    if (e.target.closest('button, a') || !cardItem) return;
    go(cardItem);
  });

  // наведение мышью на результат — карточка этого вопроса.
  // Только при настоящем движении мыши: при прокрутке списка стрелками курсор «наезжает» на пункты сам.
  for (const it of items)
    it.li.addEventListener('mousemove', () => {
      if (!side.classList.contains('searching') || !input.value.trim()) return;
      if (performance.now() - lastKeyNav < 700 || cardItem === it) return;
      const list = visible();
      active = list.indexOf(it);
      setActive(list, false);
      cardSuppressed = false;
      showCard(it);
    });

  clearBtn.addEventListener('click', () => { input.value = ''; cardSuppressed = false; apply(); input.focus(); });

  // 🔥 ⭐ 🎓 — выбрана только одна; повторный клик по активной снимает фильтр
  chips.forEach(chip => chip.addEventListener('click', () => {
    const wasPressed = chip.getAttribute('aria-pressed') === 'true';
    chips.forEach(c => c.setAttribute('aria-pressed', 'false'));
    if (!wasPressed) chip.setAttribute('aria-pressed', 'true');
    apply();
  }));
  todoChip.addEventListener('click', () => {
    todoChip.setAttribute('aria-pressed', todoChip.getAttribute('aria-pressed') === 'true' ? 'false' : 'true');
    apply();
  });

  document.addEventListener('keydown', e => {
    if (e.target === input) return;                                     // поле поиска обрабатывает само
    const el = document.activeElement;
    const typing = /^(INPUT|TEXTAREA|SELECT)$/.test(el?.tagName ?? '') || el?.isContentEditable;
    if ((e.key === '/' && !typing) || ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k')) {
      e.preventDefault();
      document.body.classList.add('open');
      input.focus();
      input.select();
      return;
    }
    if (typing) return;
    if (e.key === 'Enter' && el && /^(BUTTON|A)$/.test(el.tagName) && !card.contains(el)) return;   // Enter на кнопке — нажать её
    if (navKey(e)) return;
    if (e.key === 'Escape' && !card.hidden) { cardSuppressed = true; hideCard(); }
  });

  // Режим «фокус» для реального собеседования: прячет галочки, прогресс и календарь (отметки не стираются)
  const focusBtn = document.getElementById('focus-toggle');
  function setFocus(on, remember = true) {
    document.body.classList.toggle('focus-mode', on);
    focusBtn.setAttribute('aria-pressed', on);
    focusBtn.title = on ? 'Режим собеседования включён — показать отметки и прогресс' : 'Режим собеседования: скрыть отметки, прогресс и календарь';
    focusBtn.querySelector('small').textContent = on ? 'включён — нажми, чтобы выйти' : 'скрыть отметки и прогресс';
    if (on) { todoChip.setAttribute('aria-pressed', 'false'); chips.forEach(c => c.setAttribute('aria-pressed', 'false')); }
    apply();   // скрытый фильтр не должен прятать вопросы
    if (remember) store.saveSettings({ focusMode: on });
  }
  focusBtn.addEventListener('click', () => setFocus(!document.body.classList.contains('focus-mode')));
  if (store.settings.focusMode) setFocus(true, false);

  // ---------- Собеседования: панель откликов. Содержимое рисует jobs.js (данные в API), здесь — только открыть/закрыть ----------
  const jobsPanel = document.getElementById('jobs');
  const jobsBtn = document.getElementById('jobs-toggle');
  const escAttr = s => esc(s).replace(/"/g, '&quot;');
  const fmtDate = key => key ? key.split('-').reverse().join('.') : '';
  const setJobs = open => {
    if (open) setIdeas(false);                        // панели стоят на одном месте — открыта одна
    jobsPanel.hidden = !open;
    jobsBtn.setAttribute('aria-pressed', open);
  };
  jobsBtn.addEventListener('click', () => setJobs(jobsPanel.hidden));
  jobsPanel.addEventListener('click', e => { if (e.target.closest('[data-act="close"]')) setJobs(false); });
  document.addEventListener('keydown', e => { if (e.key === 'Escape' && !jobsPanel.hidden && !e.target.closest('#jobs input, #jobs textarea, #jobs select')) setJobs(false); });

  // ---------- Идеи для проектов: что хочу применить на практике ----------
  // хранятся в браузере, как отметки и отклики; «⬇ Markdown» — выгрузка и резервная копия
  const ideasPanel = document.getElementById('ideas');
  const ideasBtn = document.getElementById('ideas-toggle');
    const IDEA_ST = [
    { id: 'new', label: '💡 идея', color: '#eab308' },
    { id: 'doing', label: '🔧 пробую', color: '#4fa3e0' },
    { id: 'done', label: '✅ применил', color: '#22c55e' },
  ];
  const ideaSt = id => IDEA_ST.find(s => s.id === id) || IDEA_ST[0];
  let ideas = [];
  ideas = store.ideas;                                // в прежнем формате { id, d, t, n, p, q, s, done }
  const saveIdeas = () => store.syncIdeas(ideas);    // на сервер уходит только изменённое
  const openIdeas = new Set();                        // раскрытые идеи
  let ideasFilter = 'all';
  // номер вопроса «11.24» — ссылка на ответ, если такой вопрос есть
  const qLink = q => !q ? '<span class="idea-gap"></span>'
    : document.getElementById('q' + q) ? `<a class="idea-q" href="#q${escAttr(q)}" data-act="goq" title="Открыть вопрос ${escAttr(q)}">↗ ${esc(q)}</a>`
    : `<span class="idea-tag">${esc(q)}</span>`;

  function renderIdeas() {
    if (ideasPanel.hidden) return;
    const count = f => ideas.filter(x => f === 'all' || x.s === f).length;
    const filters = [['all', 'все'], ...IDEA_ST.map(s => [s.id, s.label])]
      .map(([f, l]) => `<button type="button" class="jf${f === ideasFilter ? ' on' : ''}" data-act="filter" data-f="${f}"><b>${count(f)}</b>${l}</button>`).join('');
    // сначала «пробую», потом новые идеи, применённые — в конце; внутри — свежие сверху
    const order = { doing: 0, new: 1, done: 2 };
    const list = ideas.filter(x => ideasFilter === 'all' || x.s === ideasFilter)
      .sort((a, b) => order[a.s] - order[b.s] || b.d.localeCompare(a.d));
    const rows = list.map(x => {
      const st = ideaSt(x.s), opened = openIdeas.has(x.id);
      const options = IDEA_ST.map(s => `<option value="${s.id}"${s.id === x.s ? ' selected' : ''}>${s.label}</option>`).join('');
      return `<div class="idea${x.s === 'done' ? ' done' : ''}" data-idea="${x.id}">` +
        `<div class="idea-row" data-act="toggle">` +
          `<select class="idea-st" data-act="status" style="--st:${st.color}" aria-label="Статус">${options}</select>` +
          `<span class="idea-title" title="${escAttr(x.t)}">${esc(x.t)}</span>` +
          (x.p ? `<span class="idea-tag">${esc(x.p)}</span>` : '<span class="idea-gap"></span>') +
          qLink(x.q) +
          `<span class="job-arrow">${opened ? '▾' : '▸'}</span>` +
        `</div>` +
        (opened ? `<div class="idea-detail">` +
          `<label class="job-field">Идея<input data-act="title" value="${escAttr(x.t)}"></label>` +
          `<label class="job-field">Как применить<textarea data-act="note" rows="4" placeholder="Где применить, с чего начать, ссылки, что почитать">${esc(x.n)}</textarea></label>` +
          `<div class="idea-fields"><label class="job-field">Проект<input data-act="project" value="${escAttr(x.p)}" placeholder="pet-проект, работа, новый сервис…"></label>` +
          `<label class="job-field">Вопрос<input data-act="question" value="${escAttr(x.q)}" placeholder="11.24"></label></div>` +
          `<span class="job-empty">Записано ${fmtDate(x.d)}${x.done ? ` · применено ${fmtDate(x.done)}` : ''}</span>` +
          `<button type="button" class="jobs-btn danger" data-act="del">Удалить идею</button>` +
        `</div>` : x.n ? `<div class="idea-text">${esc(x.n.length > 160 ? x.n.slice(0, 160) + '…' : x.n)}</div>` : '') +
      `</div>`;
    }).join('');
    ideasPanel.innerHTML =
      `<div class="jobs-head"><span class="jobs-title">💡 Идеи для проектов</span>` +
        `<button type="button" class="jobs-btn" data-act="md" title="Скачать все идеи файлом Markdown — заодно резервная копия">⬇ Markdown</button>` +
        `<button type="button" class="cal-close" data-act="close" title="Закрыть" aria-label="Закрыть">✕</button></div>` +
      `<form class="idea-add" data-act="add"><input name="t" placeholder="Новая идея: что хочу попробовать на практике — Enter, чтобы записать" required autocomplete="off">` +
        `<button type="submit" class="jobs-btn primary">Записать</button></form>` +
      `<div class="jobs-funnel">${filters}</div>` +
      `<div class="jobs-list">${rows || `<div class="job-empty">${ideasFilter === 'all'
        ? 'Идей пока нет. Прочитали ответ и захотелось попробовать в проекте — запишите сюда; клик по идее — подробности, проект и номер вопроса.'
        : 'В этом статусе идей нет'}</div>`}</div>`;
  }

  function exportIdeas() {
    const lines = [`# Идеи для проектов — ${fmtDate(dayKey())}`, ''];
    for (const st of IDEA_ST) {
      const list = ideas.filter(x => x.s === st.id).sort((a, b) => a.d.localeCompare(b.d));
      if (!list.length) continue;
      lines.push(`## ${st.label}`, '');
      for (const x of list) {
        lines.push(`- [${x.s === 'done' ? 'x' : ' '}] **${x.t}**` + [x.p && `проект: ${x.p}`, x.q && `вопрос ${x.q}`, fmtDate(x.d)].filter(Boolean).map(v => ` · ${v}`).join(''));
        if (x.n) lines.push(...x.n.split('\n').map(l => '  ' + l));
      }
      lines.push('');
    }
    const a = document.createElement('a');
    a.href = URL.createObjectURL(new Blob([lines.join('\n')], { type: 'text/markdown;charset=utf-8' }));
    a.download = `идеи-${dayKey()}.md`;
    a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 1000);
  }

  function setIdeas(open) {
    if (open && !jobsPanel.hidden) { jobsPanel.hidden = true; jobsBtn.setAttribute('aria-pressed', false); }
    ideasPanel.hidden = !open;
    ideasBtn.setAttribute('aria-pressed', open);
    renderIdeas();
    if (open) ideasPanel.querySelector('.idea-add input').focus();
  }
  ideasBtn.addEventListener('click', () => setIdeas(ideasPanel.hidden));
  ideasPanel.addEventListener('click', e => {
    const t = e.target.closest('[data-act]');
    if (!t) return;
    const x = ideas.find(i => i.id === t.closest('[data-idea]')?.dataset.idea);
    switch (t.dataset.act) {
      case 'toggle': if (x) { if (openIdeas.has(x.id)) openIdeas.delete(x.id); else openIdeas.add(x.id); renderIdeas(); } break;
      case 'filter': ideasFilter = ideasFilter === t.dataset.f ? 'all' : t.dataset.f; renderIdeas(); break;
      case 'goq': setIdeas(false); break;             // переход делает сам браузер, панель не должна закрывать ответ
      case 'md': exportIdeas(); break;
      case 'close': setIdeas(false); break;
      case 'del': if (confirm(`Удалить идею «${x.t}»?`)) { ideas = ideas.filter(i => i !== x); saveIdeas(); renderIdeas(); } break;
    }
  });
  ideasPanel.addEventListener('change', e => {
    const t = e.target, x = ideas.find(i => i.id === t.closest('[data-idea]')?.dataset.idea);
    if (!x) return;
    const v = t.value.trim();
    if (t.dataset.act === 'status') { x.s = t.value; x.done = t.value === 'done' ? dayKey() : ''; }
    else if (t.dataset.act === 'title') { if (!v) return; x.t = v; }
    else if (t.dataset.act === 'note') x.n = t.value.replace(/\s+$/, '');
    else if (t.dataset.act === 'project') x.p = v;
    else if (t.dataset.act === 'question') x.q = v.replace(/^#?q?/i, '');
    else return;
    saveIdeas(); renderIdeas();
  });
  ideasPanel.addEventListener('submit', e => {
    e.preventDefault();
    const t = (new FormData(e.target).get('t') || '').toString().trim();
    if (!t) return;
    ideas.push({ id: store.newId(), d: dayKey(), t, n: '', p: '', q: '', s: 'new', done: '' });
    saveIdeas(); renderIdeas();
    ideasPanel.querySelector('.idea-add input').focus();
  });
  document.addEventListener('keydown', e => { if (e.key === 'Escape' && !ideasPanel.hidden && !e.target.closest('#ideas input, #ideas textarea')) setIdeas(false); });

  // аккордеон: открыт только один раздел (вне поиска — при поиске раскрыты все разделы с совпадениями)
  for (const d of groups)
    d.addEventListener('toggle', () => {
      if (!d.open || side.classList.contains('searching')) return;
      for (const other of groups) if (other !== d && other.open) other.open = false;
    });

  // клик по названию раздела раскрывает/сворачивает дерево, при раскрытии — переход к началу раздела
  for (const d of groups) {
    const link = d.querySelector('summary a');
    if (!link) continue;
    link.addEventListener('click', e => {
      e.preventDefault();
      d.open = !d.open;
      if (!d.open) return;
      const id = link.getAttribute('href').slice(1);
      if (location.hash === '#' + id) document.getElementById(id)?.scrollIntoView({ block: 'start' });
      else location.hash = id;
    });
  }

  // ---------- Выделения маркером: выделил текст в ответе — выбрал цвет ----------
  // хранятся в браузере отдельно от прогресса и видны всегда, в том числе в режиме собеседования и в карточке ответа;
  // привязка — по самому тексту и его окружению, поэтому правки в других местах раздела выделения не сбивают
    const HL_COLORS = { yellow: 'Жёлтый', green: 'Зелёный', pink: 'Розовый' };
  const HL_SKIP = '.done-end, .done-badge, .anchor';            // вставки скрипта и «#» у заголовков в текст не входят
  const HL_BLOCK = /^(SECTION|DIV|UL|OL|TABLE|THEAD|TBODY|TFOOT|TR|FIGURE|BLOCKQUOTE|DETAILS)$/;
  let hlList = [];
  hlList = store.highlights;                          // в прежнем формате { id, sec, text, prefix, suffix, start, end, color }
  const hlSave = () => store.syncHighlights(hlList);

  const sections = [...document.querySelectorAll('main > section')];
  const secKey = sec => sec.querySelector('h1[id]')?.id || '';
  const secByKey = new Map(sections.map(s => [secKey(s), s]));

  function hlTexts(sec) {
    const nodes = [];
    const w = document.createTreeWalker(sec, NodeFilter.SHOW_TEXT,
      { acceptNode: n => n.parentElement.closest(HL_SKIP) ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_ACCEPT });
    for (let n; (n = w.nextNode());) nodes.push(n);
    return nodes;
  }

  // точка выделения (узел + смещение) → позиция в тексте раздела
  function hlOffset(nodes, container, offset) {
    const point = document.createRange();
    point.setStart(container, offset);
    let pos = 0;
    for (const t of nodes) {
      if (t === container) return pos + offset;
      if (point.comparePoint(t, t.length) > 0) break;
      pos += t.length;
    }
    return pos;
  }

  // где выделение сейчас: совпадение текста, лучше всего — с тем же окружением, иначе ближайшее к прежнему месту
  function hlFind(text, h) {
    let best = -1, bestScore = -Infinity;
    for (let i = text.indexOf(h.text); i >= 0; i = text.indexOf(h.text, i + 1)) {
      const score = (text.slice(Math.max(0, i - h.prefix.length), i) === h.prefix ? 2 : 0)
                  + (text.startsWith(h.suffix, i + h.text.length) ? 2 : 0) - Math.abs(i - h.start) / 1e7;
      if (score > bestScore) { bestScore = score; best = i; }
    }
    return best;
  }

  function hlWrap(sec, h) {
    let pos = 0;
    for (let t of hlTexts(sec)) {
      const from = pos, len = t.length;
      pos += len;
      if (pos <= h.start) continue;
      if (from >= h.end) break;
      if (!t.data.trim() && HL_BLOCK.test(t.parentElement.tagName)) continue;   // переносы строк между абзацами не красим
      const s = Math.max(h.start - from, 0), e = Math.min(h.end - from, len);
      if (e < len) t.splitText(e);
      if (s > 0) t = t.splitText(s);
      const m = document.createElement('mark');
      m.className = 'hl hl-' + h.color;
      m.dataset.hl = h.id;
      t.replaceWith(m);
      m.appendChild(t);
    }
  }

  function hlRender(sec) {
    sec.querySelectorAll('mark.hl').forEach(m => m.replaceWith(...m.childNodes));
    sec.normalize();
    const key = secKey(sec);
    const text = hlTexts(sec).map(t => t.data).join('');
    for (const h of hlList) {
      if (h.sec !== key) continue;
      const i = hlFind(text, h);
      if (i < 0) continue;                                       // текст ответа переписан — выделение ждёт в хранилище
      h.start = i; h.end = i + h.text.length;
      hlWrap(sec, h);
    }
  }

  const hlBar = document.createElement('div');
  hlBar.className = 'hl-bar';
  hlBar.hidden = true;
  document.body.appendChild(hlBar);
  let hlTarget = null;   // { sec, start, end } — новое выделение, или { h } — клик по уже выделенному

  function hlShowBar(rect) {
    hlBar.hidden = false;
    const w = hlBar.offsetWidth, ht = hlBar.offsetHeight;
    const top = rect.top - ht - 8 >= 4 ? rect.top - ht - 8 : rect.bottom + 8;
    const left = Math.min(Math.max(rect.left + rect.width / 2 - w / 2, 4), innerWidth - w - 4);
    hlBar.style.top = top + 'px';
    hlBar.style.left = left + 'px';
  }
  function hlHideBar() { hlBar.hidden = true; hlTarget = null; }

  function hlBarHtml(current, canDelete = !!current) {
    return Object.entries(HL_COLORS).map(([c, name]) =>
      `<button type="button" class="${c}${c === current ? ' on' : ''}" data-color="${c}" title="${name}" aria-label="${name}"></button>`).join('') +
      (canDelete ? '<button type="button" class="hl-del" data-del="1" title="Снять выделение">✕ убрать</button>' : '');
  }

  // уже выделенные куски, которые задевает новое выделение мышью
  const hlOverlap = (key, start, end) =>
    hlList.filter(x => x.sec === key && x.start < end && x.end > start && document.querySelector(`main mark[data-hl="${x.id}"]`));

  function hlFromSelection() {
    const sel = getSelection();
    if (!sel.rangeCount || sel.isCollapsed) return null;
    const r = sel.getRangeAt(0);
    const el = n => n.nodeType === 1 ? n : n.parentElement;
    const sec = el(r.commonAncestorContainer).closest('main > section');
    if (!sec) return null;
    const nodes = hlTexts(sec);
    const text = nodes.map(t => t.data).join('');
    let start = hlOffset(nodes, r.startContainer, r.startOffset), end = hlOffset(nodes, r.endContainer, r.endOffset);
    while (start < end && /\s/.test(text[start])) start++;
    while (end > start && /\s/.test(text[end - 1])) end--;
    if (end - start < 2) return null;
    return { sec, text, start, end, rect: r.getBoundingClientRect() };
  }

  let hlDown = false, hlTimer = 0;
  function hlCheckSelection() {
    clearTimeout(hlTimer);
    hlTimer = setTimeout(() => {
      if (hlDown) return;
      const s = hlFromSelection();
      if (!s) { if (hlTarget && !hlTarget.h) hlHideBar(); return; }
      hlTarget = s;
      const over = hlOverlap(secKey(s.sec), s.start, s.end);
      hlBar.innerHTML = hlBarHtml(over.length === 1 ? over[0].color : null, over.length > 0);
      hlShowBar(s.rect);
    }, 120);
  }
  document.addEventListener('pointerdown', e => { if (!hlBar.contains(e.target)) { hlDown = true; hlHideBar(); } });
  document.addEventListener('pointerup', () => { hlDown = false; hlCheckSelection(); });
  document.addEventListener('selectionchange', hlCheckSelection);   // на телефоне выделение меняют ручками, без pointerup
  hlBar.addEventListener('pointerdown', e => e.preventDefault());   // клик по кнопке не снимает выделение текста

  // клик по выделенному — сменить цвет или убрать
  document.querySelector('main').addEventListener('click', e => {
    const m = e.target.closest('mark.hl');
    if (!m || !getSelection().isCollapsed) return;
    const h = hlList.find(x => x.id === m.dataset.hl);
    if (!h) return;
    hlTarget = { h };
    hlBar.innerHTML = hlBarHtml(h.color);
    hlShowBar(m.getBoundingClientRect());
  });

  hlBar.addEventListener('click', e => {
    const b = e.target.closest('button');
    if (!b || !hlTarget) return;
    if (hlTarget.h) {
      const h = hlTarget.h;
      if (b.dataset.del) hlList = hlList.filter(x => x !== h);
      else h.color = b.dataset.color;
      hlSave();
      hlRender(secByKey.get(h.sec));
    } else {
      const { sec, text } = hlTarget;
      let { start, end } = hlTarget;
      const key = secKey(sec);
      // пересечение с прежними выделениями: «убрать» снимает их, цвет — сливает всё в одно выделение нового цвета
      const overlap = hlOverlap(key, start, end);
      for (const x of overlap) { start = Math.min(start, x.start); end = Math.max(end, x.end); }
      hlList = hlList.filter(x => !overlap.includes(x));
      if (!b.dataset.del) hlList.push({ id: store.newId(), sec: key, color: b.dataset.color,
                    text: text.slice(start, end), prefix: text.slice(Math.max(0, start - 30), start), suffix: text.slice(end, end + 30),
                    start, end, at: dayKey() });
      hlSave();
      getSelection().removeAllRanges();
      hlRender(sec);
    }
    hlHideBar();
  });
  addEventListener('scroll', () => { if (!hlBar.hidden) hlHideBar(); }, { passive: true });
  document.addEventListener('keydown', e => { if (e.key === 'Escape' && !hlBar.hidden) hlHideBar(); });

  for (const key of new Set(hlList.map(h => h.sec))) if (secByKey.has(key)) hlRender(secByKey.get(key));

  document.getElementById('menu').onclick = () => document.body.classList.toggle('open');
  document.querySelectorAll('#side li a').forEach(a => a.addEventListener('click', () => document.body.classList.remove('open')));

  // ссылка на вопрос, скрытый фильтром 🔥 ⭐ 🎓 ☐ (например, «вопросы 6.5, 11.24» в тексте), — показать этот ответ, фильтр не сбрасывать
  function revealAnswer(id) {
    const h3 = id && document.getElementById(id);
    if (!h3 || h3.tagName !== 'H3' || !h3.closest('.flt-out')) return;
    h3.closest('section')?.classList.remove('flt-out');
    h3.classList.remove('flt-out');
    for (let n = h3.nextElementSibling; n && !/^H[1-3]$/.test(n.tagName); n = n.nextElementSibling) n.classList.remove('flt-out');
  }
  document.addEventListener('click', e => {
    const a = e.target.closest('a[href^="#"]');
    if (a) revealAnswer(decodeURIComponent(a.getAttribute('href').slice(1)));   // до перехода — браузер прокрутит к уже видимому
  }, true);
  // меню следует за переходами — по ссылке, кнопками «назад»/«вперёд» (в том числе на мышке) и при открытии
  // по ссылке: раскрыт раздел текущего места, текущий вопрос подсвечен и виден в меню.
  // Во время поиска меню показывает результаты — его не трогаем.
  function syncMenu(id) {
    const it = items.find(x => x.id === id);
    const d = it ? it.d : groups.find(g => g.querySelector('summary a')?.getAttribute('href') === '#' + id);
    items.forEach(x => x.li.classList.toggle('current', x === it));
    if (!d || side.classList.contains('searching')) return;
    if (!d.open) d.open = true;                      // аккордеон сам свернёт остальные разделы
    (it ? it.li : d.querySelector('summary')).scrollIntoView({ block: 'nearest' });
  }
  addEventListener('hashchange', () => {
    const id = decodeURIComponent(location.hash.slice(1)), el = document.getElementById(id);
    if (el?.closest('.flt-out')) { revealAnswer(id); el.scrollIntoView({ block: 'start' }); }
    syncMenu(id);
  });

  renderProgress();
  apply();
  revealAnswer(decodeURIComponent(location.hash.slice(1)));
  if (location.hash) syncMenu(decodeURIComponent(location.hash.slice(1)));
  setTimeout(() => input.focus({ preventScroll: true }), 0);   // вкладка сразу готова к вводу
}
