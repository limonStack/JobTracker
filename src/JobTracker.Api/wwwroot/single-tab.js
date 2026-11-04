'use strict';
// Одна вкладка JobTracker: каждый запуск из IDE (launchBrowser) открывает новую, и старые копятся.
// Новая вкладка сообщает о себе, остальные закрываются. Закрыть себя скрипт может, только если во вкладке
// не было переходов; иначе вкладка гаснет с предложением работать в ней — это перезагрузка, и уже она закроет остальные.
(() => {
  if (!('BroadcastChannel' in window)) return;
  const channel = new BroadcastChannel('jobtracker-tab');

  channel.onmessage = e => {
    if (e.data !== 'opened') return;
    window.close();
    // если браузер не дал закрыть, страница ещё жива — показываем заглушку
    setTimeout(showOverlay, 100);
  };
  channel.postMessage('opened');

  function showOverlay() {
    if (document.querySelector('.tab-overlay')) return;
    const overlay = document.createElement('div');
    overlay.className = 'tab-overlay';
    overlay.innerHTML = '<div><p>JobTracker открыт в другой вкладке — эту можно закрыть.</p>' +
      '<button type="button" class="jobs-btn">Работать здесь</button></div>';
    overlay.querySelector('button').onclick = () => location.reload();
    document.body.append(overlay);
  }
})();
