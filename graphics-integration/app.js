'use strict';

/* ============ Общие помощники ============ */
const $ = (id) => document.getElementById(id);

const fileInput = $('fileInput');
const canvas = $('canvas');
const ctx = canvas.getContext('2d', { willReadFrequently: true });
const canvasEmpty = $('canvasEmpty');
const filterButtons = [...document.querySelectorAll('button[data-filter]')];
const resetBtn = $('resetBtn');
const publishBtn = $('publishBtn');
const uploadBtn = $('uploadBtn');
const saveBtn = $('saveBtn');
const previewContainer = $('previewContainer');
const uploadResult = $('uploadResult');
const filterInfo = $('filterInfo');

const MAX_SIDE = 1600;       // большие фото уменьшаем: getImageData по 20 Мпикс тормозит и съедает память
let originalData = null;     // ImageData исходного изображения (для сброса)
let sourceName = '';
let appliedFilters = [];     // какие фильтры уже применены (для подписи и записи в БД)

let statusTimer = 0;
function showStatus(text, isError = false) {
  const el = $('status');
  el.textContent = text;
  el.classList.toggle('error', isError);
  el.classList.add('show');
  clearTimeout(statusTimer);
  statusTimer = setTimeout(() => el.classList.remove('show'), 3500);
}

function setEditorEnabled(on) {
  filterButtons.forEach((b) => (b.disabled = !on));
  [resetBtn, publishBtn, uploadBtn, saveBtn].forEach((b) => (b.disabled = !on));
}

function canvasToBlob(type = 'image/jpeg', quality = 0.9) {
  return new Promise((resolve, reject) =>
    canvas.toBlob((b) => (b ? resolve(b) : reject(new Error('Не удалось получить изображение из canvas'))), type, quality));
}

function describeFilters() {
  return appliedFilters.length ? appliedFilters.map((f) => FILTER_TITLES[f]).join(' + ') : 'без фильтров';
}

/* ============ Задание 2: загрузка и фильтры Canvas ============ */
const FILTER_TITLES = { grayscale: 'чёрно-белое', sepia: 'сепия', invert: 'негатив', contrast: 'контраст' };

const FILTERS = {
  // Формула яркости (luminance), коэффициенты ITU-R BT.601
  grayscale(d) {
    for (let i = 0; i < d.length; i += 4) {
      const g = 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2];
      d[i] = d[i + 1] = d[i + 2] = g;
    }
  },
  sepia(d) {
    for (let i = 0; i < d.length; i += 4) {
      const r = d[i], g = d[i + 1], b = d[i + 2];
      d[i] = 0.393 * r + 0.769 * g + 0.189 * b;      // Uint8ClampedArray сам ограничивает значения 0..255
      d[i + 1] = 0.349 * r + 0.686 * g + 0.168 * b;
      d[i + 2] = 0.272 * r + 0.534 * g + 0.131 * b;
    }
  },
  invert(d) {
    for (let i = 0; i < d.length; i += 4) {
      d[i] = 255 - d[i]; d[i + 1] = 255 - d[i + 1]; d[i + 2] = 255 - d[i + 2];   // альфа не трогаем
    }
  },
  contrast(d, k = 1.3) {
    for (let i = 0; i < d.length; i += 4) {
      d[i] = (d[i] - 128) * k + 128; d[i + 1] = (d[i + 1] - 128) * k + 128; d[i + 2] = (d[i + 2] - 128) * k + 128;
    }
  },
};

fileInput.addEventListener('change', (e) => {
  const file = e.target.files[0];
  if (!file) return;
  if (!file.type.startsWith('image/')) {
    showStatus('Выбранный файл не является изображением.', true);
    fileInput.value = '';
    return;
  }
  const reader = new FileReader();
  reader.onerror = () => showStatus('Не удалось прочитать файл.', true);
  reader.onload = (event) => {
    const img = new Image();
    img.onerror = () => showStatus('Файл повреждён или имеет неподдерживаемый формат.', true);
    img.onload = () => {
      const scale = Math.min(1, MAX_SIDE / Math.max(img.naturalWidth, img.naturalHeight));
      canvas.width = Math.round(img.naturalWidth * scale);
      canvas.height = Math.round(img.naturalHeight * scale);
      ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
      originalData = ctx.getImageData(0, 0, canvas.width, canvas.height);
      sourceName = file.name.replace(/\.[^.]+$/, '');
      appliedFilters = [];
      canvas.classList.add('on');
      canvasEmpty.hidden = true;
      canvas.setAttribute('aria-label', `Редактируемое изображение: ${file.name}`);
      $('nameInput').value = sourceName;
      setEditorEnabled(true);
      updateFilterInfo(scale < 1 ? ` Размер уменьшен до ${canvas.width}×${canvas.height}.` : '');
    };
    img.src = event.target.result;
  };
  reader.readAsDataURL(file);
});

function updateFilterInfo(extra = '') {
  filterInfo.textContent = `Применено: ${describeFilters()}.${extra}`;
}

filterButtons.forEach((btn) =>
  btn.addEventListener('click', () => {
    if (!originalData) return;
    const name = btn.dataset.filter;
    // Фильтры накапливаются: данные берём с canvas (а не из исходника), чтобы можно было комбинировать.
    const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
    FILTERS[name](imageData.data);
    ctx.putImageData(imageData, 0, 0);
    appliedFilters.push(name);
    updateFilterInfo();
  }));

resetBtn.addEventListener('click', () => {
  if (!originalData) return;
  ctx.putImageData(originalData, 0, 0);
  appliedFilters = [];
  updateFilterInfo();
});

/* ============ Задание 3: публикация и загрузка на сервер ============ */
publishBtn.addEventListener('click', () => {
  // Data URL: пригоден для немедленного показа в <img>
  const dataURL = canvas.toDataURL('image/jpeg', 0.9);
  const item = document.createElement('figure');
  item.className = 'gallery-item';
  const img = document.createElement('img');
  img.src = dataURL;
  img.alt = `Обработанное изображение «${sourceName}» (${describeFilters()})`;
  img.width = canvas.width; img.height = canvas.height;
  const cap = document.createElement('figcaption');
  cap.className = 'caption';
  cap.textContent = `${sourceName}: ${describeFilters()}`;
  const del = document.createElement('button');
  del.type = 'button'; del.className = 'item-delete danger';
  del.textContent = '×'; del.setAttribute('aria-label', 'Убрать из галереи');
  del.addEventListener('click', () => item.remove());
  item.append(img, cap, del);
  previewContainer.appendChild(item);
  showStatus('Изображение опубликовано в галерее.');
});

async function uploadToServer(blob, filename) {
  const formData = new FormData();
  formData.append('file', blob, filename);
  formData.append('filters', describeFilters());
  const response = await fetch('/api/upload', { method: 'POST', body: formData });
  if (!response.ok) throw new Error(`Сервер ответил ${response.status}`);
  return { formData, json: await response.json() };      // сервер должен вернуть, например, { "url": "..." }
}

uploadBtn.addEventListener('click', async () => {
  uploadBtn.disabled = true;
  uploadResult.hidden = false;
  const filename = `edited_${Date.now()}.jpg`;
  let blob;
  try {
    blob = await canvasToBlob();
    const { json } = await uploadToServer(blob, filename);
    uploadResult.textContent = `Файл загружен. URL: ${json.url ?? json.id ?? JSON.stringify(json)}`;
    showStatus('Файл загружен на сервер.');
  } catch (err) {
    // Серверной части в этой работе нет — показываем, что именно было бы отправлено (допускается заданием).
    const fd = new FormData();
    if (blob) { fd.append('file', blob, filename); fd.append('filters', describeFilters()); }
    const lines = [`Сервер недоступен (${err.message}). Сформирован FormData (multipart/form-data):`];
    for (const [key, value] of fd.entries()) {
      lines.push(value instanceof File
        ? `  ${key} = File { name: "${value.name}", type: "${value.type}", size: ${value.size} байт }`
        : `  ${key} = "${value}"`);
    }
    uploadResult.textContent = lines.join('\n');
    console.log(lines.join('\n'));
    showStatus('Сервер недоступен: показано содержимое FormData.', true);
  } finally {
    uploadBtn.disabled = !originalData;
  }
});

/* ============ Задание 4: IndexedDB ============ */
const DB_NAME = 'MediaGalleryDB';
const DB_VERSION = 1;
const STORE = 'media';
let dbPromise = null;

function openDB() {
  if (!('indexedDB' in window)) return Promise.reject(new Error('IndexedDB не поддерживается'));
  // Одно подключение на всё приложение: не открываем базу заново при каждой операции
  dbPromise ??= new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);
    request.onupgradeneeded = (e) => {
      const db = e.target.result;
      if (!db.objectStoreNames.contains(STORE)) {
        const store = db.createObjectStore(STORE, { keyPath: 'id', autoIncrement: true });
        store.createIndex('createdAt', 'createdAt');
        store.createIndex('type', 'type');
      }
    };
    request.onsuccess = (e) => resolve(e.target.result);
    request.onerror = (e) => { dbPromise = null; reject(e.target.error); };
  });
  return dbPromise;
}

/** Выполняет запрос и ждёт завершения транзакции (а не только успеха запроса). */
async function runTx(mode, work) {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, mode);
    let result;
    const req = work(tx.objectStore(STORE));
    if (req) req.onsuccess = () => { result = req.result; };
    tx.oncomplete = () => resolve(result);
    tx.onerror = () => reject(tx.error);
    tx.onabort = () => reject(tx.error || new Error('Транзакция отменена'));
  });
}

async function saveMedia(blob, metadata = {}) {
  const record = {
    blob,
    type: blob.type,
    name: metadata.name || `media_${Date.now()}`,
    description: metadata.description || '',
    filters: metadata.filters || [],
    width: metadata.width || 0,
    height: metadata.height || 0,
    createdAt: new Date().toISOString(),
    size: blob.size,
  };
  return runTx('readwrite', (store) => store.add(record));
}

const deleteMedia = (id) => runTx('readwrite', (store) => store.delete(id));
const clearMedia = () => runTx('readwrite', (store) => store.clear());
const countMedia = () => runTx('readonly', (store) => store.count());

/** Все записи от новых к старым — через индекс createdAt и курсор в обратном порядке. */
async function getAllNewestFirst() {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const records = [];
    const tx = db.transaction(STORE, 'readonly');
    const cursorReq = tx.objectStore(STORE).index('createdAt').openCursor(null, 'prev');
    cursorReq.onsuccess = () => {
      const cursor = cursorReq.result;
      if (cursor) { records.push(cursor.value); cursor.continue(); }
    };
    tx.oncomplete = () => resolve(records);
    tx.onerror = tx.onabort = () => reject(tx.error);
  });
}

const dbGallery = $('dbGallery');
const dbEmpty = $('dbEmpty');

function formatSize(bytes) {
  return bytes < 1024 ? `${bytes} Б` : bytes < 1048576 ? `${(bytes / 1024).toFixed(1)} КБ` : `${(bytes / 1048576).toFixed(2)} МБ`;
}

async function loadMediaGallery(container = dbGallery) {
  const records = await getAllNewestFirst();
  container.replaceChildren();
  dbEmpty.hidden = records.length > 0;
  $('dbStats').textContent = records.length
    ? `Записей: ${records.length}, всего ${formatSize(records.reduce((s, r) => s + r.size, 0))}`
    : '';
  for (const record of records) {
    const url = URL.createObjectURL(record.blob);
    const item = document.createElement('figure');
    item.className = 'gallery-item';
    item.dataset.id = record.id;
    const img = document.createElement('img');
    img.src = url;
    img.alt = record.description || record.name;
    img.width = record.width || 800; img.height = record.height || 600;
    // Освобождаем объектный URL после загрузки (и при ошибке, чтобы не было утечки)
    img.onload = img.onerror = () => URL.revokeObjectURL(url);
    const cap = document.createElement('figcaption');
    cap.className = 'caption';
    cap.textContent = record.name;
    const meta = document.createElement('div');
    meta.className = 'meta';
    meta.textContent = `#${record.id} · ${formatSize(record.size)} · ${new Date(record.createdAt).toLocaleString('ru-RU')}`;
    const del = document.createElement('button');
    del.type = 'button'; del.className = 'item-delete danger';
    del.textContent = '×'; del.setAttribute('aria-label', `Удалить «${record.name}» из базы`);
    del.addEventListener('click', async () => {
      try { await deleteMedia(record.id); await loadMediaGallery(); showStatus('Запись удалена.'); }
      catch (err) { showStatus(`Не удалось удалить: ${err.message}`, true); }
    });
    item.append(img, cap, meta, del);
    container.appendChild(item);
  }
}

$('saveForm').addEventListener('submit', async (e) => {
  e.preventDefault();
  if (!originalData) return;
  saveBtn.disabled = true;
  try {
    const blob = await canvasToBlob('image/jpeg', 0.9);
    const id = await saveMedia(blob, {
      name: $('nameInput').value.trim() || sourceName,
      description: $('descInput').value.trim(),
      filters: [...appliedFilters],
      width: canvas.width, height: canvas.height,
    });
    await loadMediaGallery();
    showStatus(`Сохранено в базе (id ${id}).`);
  } catch (err) {
    showStatus(`Не удалось сохранить: ${err.message}`, true);
  } finally {
    saveBtn.disabled = !originalData;
  }
});

$('clearDbBtn').addEventListener('click', async () => {
  if (!(await countMedia().catch(() => 0))) { showStatus('База уже пуста.'); return; }
  if (!confirm('Удалить все изображения из базы? Действие необратимо.')) return;
  try { await clearMedia(); await loadMediaGallery(); showStatus('База очищена.'); }
  catch (err) { showStatus(`Не удалось очистить: ${err.message}`, true); }
});

// Старт: показываем содержимое базы; если IndexedDB недоступна (например, приватный режим) — сообщаем и отключаем раздел.
loadMediaGallery().catch((err) => {
  showStatus(`База недоступна: ${err.message}`, true);
  $('saveForm').querySelectorAll('input,button').forEach((el) => (el.disabled = true));
  $('clearDbBtn').disabled = true;
});
