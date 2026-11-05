// Service worker: оболочка страницы (HTML, CSS, JS, значки) открывается и без сервера.
// Стратегия — «сначала сеть»: сервер доступен — всегда свежие файлы (без старого кэша после правок),
// недоступен — копия из кэша. Запросы /api/* не трогаем: данные и очередь изменений ведёт sync.js.
const CACHE = 'kb-338-shell-v1';
const SHELL = [
  '/', '/index.html', '/app.css', '/app.js', '/single-tab.js', '/manifest.webmanifest',
  '/css/knowledge.css',
  '/js/main.js', '/js/db.js', '/js/sync.js', '/js/render.js', '/js/store.js', '/js/knowledge.js',
  '/icons/favicon.svg', '/icons/icon-192.png', '/icons/icon-512.png',
];

self.addEventListener('install', event => {
  event.waitUntil(caches.open(CACHE).then(c => c.addAll(SHELL)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', event => {
  // кэши прежних версий оболочки больше не нужны
  event.waitUntil(caches.keys()
    .then(keys => Promise.all(keys.filter(k => /^kb-33\d-shell-/.test(k) && k !== CACHE).map(k => caches.delete(k))))
    .then(() => self.clients.claim()));
});

self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);
  if (event.request.method !== 'GET' || url.origin !== location.origin || url.pathname.startsWith('/api/')) return;

  event.respondWith((async () => {
    const cache = await caches.open(CACHE);
    try {
      const fresh = await fetch(event.request);
      if (fresh.ok) cache.put(event.request, fresh.clone());
      return fresh;
    } catch {
      // открытие с якорем или параметрами (/#q7.1, /?x) — та же страница
      return (await cache.match(event.request, { ignoreSearch: true }))
        ?? (event.request.mode === 'navigate' ? cache.match('/index.html') : Response.error());
    }
  })());
});
