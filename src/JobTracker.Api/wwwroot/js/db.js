// Локальное хранилище страницы в IndexedDB: снимок данных, тексты ответов и очередь изменений.
// Тонкая обёртка без библиотек. Интерфейс тот же, что у memoryStorage, — на нём работают тесты.

const DB_NAME = 'kb-339';
const VERSION = 1;

const request = r => new Promise((resolve, reject) => { r.onsuccess = () => resolve(r.result); r.onerror = () => reject(r.error); });

export async function openStorage() {
  const open = indexedDB.open(DB_NAME, VERSION);
  open.onupgradeneeded = () => {
    const db = open.result;
    if (!db.objectStoreNames.contains('kv')) db.createObjectStore('kv');
    // очередь: ключ растёт с каждым изменением — порядок отправки = порядок правок
    if (!db.objectStoreNames.contains('outbox')) db.createObjectStore('outbox', { keyPath: 'seq', autoIncrement: true });
  };
  const db = await request(open);
  const tx = (store, mode = 'readonly') => db.transaction(store, mode).objectStore(store);

  // просим браузер не удалять данные при нехватке места: здесь могут быть ещё не отправленные правки
  navigator.storage?.persist?.().catch(() => { });

  return {
    get: key => request(tx('kv').get(key)),
    set: (key, value) => request(tx('kv', 'readwrite').put(value, key)),
    outboxAll: () => request(tx('outbox').getAll()),
    outboxAdd: op => request(tx('outbox', 'readwrite').add(op)),
    outboxDelete: seq => request(tx('outbox', 'readwrite').delete(seq)),
  };
}

/** То же в памяти — для тестов (node --test) и как запасной вариант, если IndexedDB недоступен. */
export function memoryStorage() {
  const kv = new Map(), outbox = new Map();
  let seq = 0;
  const copy = v => v === undefined ? undefined : structuredClone(v);
  return {
    get: async key => copy(kv.get(key)),
    set: async (key, value) => { kv.set(key, copy(value)); },
    outboxAll: async () => [...outbox.values()].map(copy),
    outboxAdd: async op => { const s = ++seq; outbox.set(s, { ...copy(op), seq: s }); return s; },
    outboxDelete: async s => { outbox.delete(s); },
  };
}
