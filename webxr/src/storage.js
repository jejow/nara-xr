const storageKey = "nara.webxr.settings.v1";
const defaults = { room: "university", language: "id", minutes: 3, mode: "auto", audio: true, preloadVoice: true, view: "presenter", topic: "", summary: "", quality: "balanced" };
let opened;

export function loadSettings() {
  try {
    const parsed = JSON.parse(localStorage.getItem(storageKey)) || {};
    const settings = { ...defaults, ...parsed };
    // The earlier Netlify build saved its offline default with any settings change.
    if (settings.mode === "offline" && !Object.hasOwn(parsed, "preloadVoice")) settings.mode = defaults.mode;
    if (!["presenter", "screen", "overview"].includes(settings.view)) settings.view = defaults.view;
    if (!["auto", "offline", "live"].includes(settings.mode)) settings.mode = defaults.mode;
    return settings;
  } catch {
    return { ...defaults };
  }
}

export function saveSettings(value) {
  try { localStorage.setItem(storageKey, JSON.stringify(value)); } catch {}
}

function openDatabase() {
  return opened ??= new Promise((resolve, reject) => {
    const request = indexedDB.open("nara-webxr", 1);
    request.onupgradeneeded = () => request.result.createObjectStore("local");
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

async function transaction(mode, run) {
  const db = await openDatabase();
  return new Promise((resolve, reject) => {
    const tx = db.transaction("local", mode);
    const operation = run(tx.objectStore("local"));
    tx.oncomplete = () => resolve(operation.result);
    tx.onerror = () => reject(tx.error);
    tx.onabort = () => reject(tx.error);
  });
}

export const readLocal = (id) => transaction("readonly", (store) => store.get(id));
export const writeLocal = (id, value) => transaction("readwrite", (store) => store.put(value, id));
export const removeLocal = (id) => transaction("readwrite", (store) => store.delete(id));

export async function storeCredential(secret) {
  if (!/^sk-[a-zA-Z0-9_-]{12,200}$/.test(secret)) throw new Error("key");
  const aes = await crypto.subtle.generateKey({ name: "AES-GCM", length: 256 }, false, ["encrypt", "decrypt"]);
  const iv = crypto.getRandomValues(new Uint8Array(12));
  const sealed = await crypto.subtle.encrypt({ name: "AES-GCM", iv }, aes, new TextEncoder().encode(secret));
  await writeLocal("credential", { aes, sealed, iv });
}

export async function readCredential() {
  const record = await readLocal("credential");
  if (!record) return "";
  const plain = await crypto.subtle.decrypt({ name: "AES-GCM", iv: record.iv }, record.aes, record.sealed);
  return new TextDecoder().decode(plain);
}
