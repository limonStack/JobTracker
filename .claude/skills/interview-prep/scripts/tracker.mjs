// Отклик в трекере JobTracker для вакансии, под которую готовимся.
//   node tracker.mjs --company "Acme" --url "https://…" --note "…" --event "Подготовлен раздел 25"
//   [--base http://localhost:5196]
// Ищет отклик по ссылке, затем по названию компании. Нет — создаёт (PUT /api/applications/{id}),
// есть — только добавляет событие. Запросы идемпотентны: id генерирует клиент, как во фронтенде.
import { randomBytes } from 'node:crypto';

const args = Object.fromEntries(process.argv.slice(2).reduce((acc, a, i, all) => {
  if (a.startsWith('--')) acc.push([a.slice(2), all[i + 1]?.startsWith('--') ? 'true' : all[i + 1]]);
  return acc;
}, []));
const base = (args.base ?? 'http://localhost:5196').replace(/\/$/, '');
if (!args.company) { console.error('Нужен --company'); process.exit(2); }

// UUID v7 — как генерирует фронтенд (время в первых 48 битах)
function uuidv7() {
  const b = randomBytes(16);
  const ms = BigInt(Date.now());
  for (let i = 0; i < 6; i++) b[i] = Number((ms >> BigInt(8 * (5 - i))) & 0xffn);
  b[6] = (b[6] & 0x0f) | 0x70;
  b[8] = (b[8] & 0x3f) | 0x80;
  const h = b.toString('hex');
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`;
}

const pad = n => String(n).padStart(2, '0');
const now = new Date();
const today = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;   // день пользователя, не UTC

async function call(method, path, body) {
  const res = await fetch(base + path, {
    method,
    headers: body ? { 'Content-Type': 'application/json' } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!res.ok) throw new Error(`${method} ${path} → ${res.status} ${await res.text()}`);
  return res.status === 204 ? null : res.json();
}

let list;
try {
  list = await call('GET', '/api/applications');
} catch (e) {
  console.error(`Сервер JobTracker недоступен (${base}): ${e.cause?.code ?? e.message}.\nЗапустите: dotnet run --project src/JobTracker.Api`);
  process.exit(3);
}

const norm = u => (u ?? '').trim().toLowerCase().replace(/^https?:\/\/(www\.)?/, '').replace(/[?#].*$/, '').replace(/\/$/, '');
const found = (args.url && list.find(a => a.url && norm(a.url) === norm(args.url)))
  ?? list.find(a => a.company.trim().toLowerCase() === args.company.trim().toLowerCase());

let app = found;
if (!app) {
  app = await call('PUT', `/api/applications/${uuidv7()}`, {
    company: args.company, appliedOn: today, url: args.url ?? null, note: args.note ?? '',
  });
  console.log(`Создан отклик «${app.company}» (${app.id}), этап ${app.stage}, дата ${app.appliedOn}.`);
} else {
  console.log(`Отклик уже есть: «${app.company}» (${app.id}), этап ${app.stage}, с ${app.appliedOn}. Новый не создаю.`);
}

if (args.event) {
  if (app.events?.some(e => e.text === args.event)) console.log(`Событие «${args.event}» уже записано.`);
  else {
    await call('PUT', `/api/applications/${app.id}/events/${uuidv7()}`, { date: today, text: args.event });
    console.log(`Добавлено событие: ${today} — ${args.event}`);
  }
}
