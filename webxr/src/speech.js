let context;
let current;
let generation = 0;
let ttsWorker;
let ttsBoot;
let modelWarm;
let webVoiceList = [];
const voiceRequests = new Map();
const recordedAudio = new Map();
const synthesizedAudio = new Map();
const audioCache = new Map();

function refreshVoices() {
  webVoiceList = globalThis.speechSynthesis?.getVoices() || [];
}
refreshVoices();
globalThis.speechSynthesis?.addEventListener("voiceschanged", refreshVoices);

export function audioUnlock() {
  const WebAudio = globalThis.AudioContext || globalThis.webkitAudioContext;
  if (!WebAudio) return null;
  context ??= new WebAudio();
  if (context.state === "suspended") context.resume().catch(() => {});
  return context;
}

export function stopVoice() {
  generation++;
  if (current) {
    try { current.stop(); } catch {}
  }
  current = null;
  globalThis.speechSynthesis?.cancel();
}

async function sound(key, bufferProvider, pan, turn, onStart) {
  const audio = audioUnlock();
  if (!audio) return false;
  let decoded = key && audioCache.get(key);
  if (!decoded) {
    decoded = await audio.decodeAudioData(await bufferProvider());
    if (key) {
      audioCache.set(key, decoded);
      if (audioCache.size > 14) audioCache.delete(audioCache.keys().next().value);
    }
  }
  if (turn !== generation || audio.state === "closed") return false;
  const source = audio.createBufferSource();
  source.buffer = decoded;
  const gain = audio.createGain();
  gain.gain.value = .85;
  source.connect(gain);
  const stereo = audio.createStereoPanner?.();
  if (stereo) {
    stereo.pan.value = Math.max(-.85, Math.min(.85, pan || 0));
    gain.connect(stereo).connect(audio.destination);
  } else gain.connect(audio.destination);
  current = source;
  const play = new Promise(resolve => {
    let timer;
    const end = (value) => { clearTimeout(timer); resolve(value); };
    source.onended = () => end(true);
    timer = setTimeout(() => end(false), (decoded.duration + 2) * 1000);
  });
  source.start();
  onStart?.();
  return play;
}

function browserVoice(text, lang, profile, turn, onStart) {
  const synthesis = globalThis.speechSynthesis;
  const local = webVoiceList.filter(voice => voice.localService && voice.lang.toLowerCase().startsWith(lang));
  if (!synthesis || !local.length || turn !== generation) return Promise.resolve(false);
  return new Promise(resolve => {
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.voice = local[(profile?.portrait || 0) % local.length];
    utterance.lang = lang === "id" ? "id-ID" : "en-US";
    utterance.rate = Math.max(.83, Math.min(1.13, profile?.voiceRate || .98));
    const max = setTimeout(() => { synthesis.cancel(); resolve(false); }, 32000);
    utterance.onstart = () => { if (turn === generation) onStart?.(); };
    utterance.onend = () => { clearTimeout(max); resolve(true); };
    utterance.onerror = () => { clearTimeout(max); resolve(false); };
    synthesis.speak(utterance);
  });
}

function piperVoiceId(lang, profile) {
  if (lang === "id") return "id_ID-news_tts-medium";
  return profile?.gender === "male" ? "en_US-ryan-low" : "en_US-amy-low";
}

function resetTts(reason) {
  ttsWorker?.terminate();
  ttsWorker = ttsBoot = modelWarm = undefined;
  for (const request of voiceRequests.values()) {
    clearTimeout(request.timer);
    request.reject(new Error(reason));
  }
  voiceRequests.clear();
}

function prepareTts() {
  return ttsBoot ??= import("./speech-voice.js?worker").then(({ default: WorkerModule }) => {
    const worker = ttsWorker = new WorkerModule({ name: "nara-local-voice" });
    worker.addEventListener("message", ({ data }) => {
      const request = voiceRequests.get(data.id);
      if (!request) return;
      if (data.status === "progress") { request.progress?.(data); return; }
      voiceRequests.delete(data.id);
      clearTimeout(request.timer);
      if (data.status === "done") request.resolve(data.audio);
      else request.reject(new Error("voice"));
    });
    worker.addEventListener("error", () => {
      if (ttsWorker === worker) resetTts("voice-worker");
    });
    return worker;
  }).catch(error => { ttsBoot = undefined; throw error; });
}

function synthesize(text, voice, progress, warm = false) {
  return prepareTts().then(worker => new Promise((resolve, reject) => {
    const id = crypto.randomUUID();
    const timer = setTimeout(() => resetTts("voice-timeout"), 90000);
    voiceRequests.set(id, { progress, resolve, reject, timer });
    worker.postMessage({ id, text, voice, warm });
  }));
}

function hasBrowserVoice(language) {
  return webVoiceList.some(voice => voice.localService && voice.lang.toLowerCase().startsWith(language));
}

export function prepareVoice(language, profile, progress) {
  if (hasBrowserVoice(language)) return Promise.resolve();
  const voice = piperVoiceId(language, profile);
  if (modelWarm?.voice === voice) return modelWarm.promise;
  const promise = synthesize("", voice, progress, true).catch(error => {
    if (modelWarm?.promise === promise) modelWarm = undefined;
    throw error;
  });
  modelWarm = { voice, promise };
  return promise;
}

function clipPath(profile, soundName, language) {
  return "/media/voices/" + profile.id + "/" + soundName + "_" + language + ".ogg";
}

function recording(path) {
  if (!recordedAudio.has(path)) {
    const promise = fetch(path, { priority: "low" }).then(response => {
      if (!response.ok) throw new Error("recording");
      return response.arrayBuffer();
    }).catch(error => { recordedAudio.delete(path); throw error; });
    recordedAudio.set(path, promise);
    if (recordedAudio.size > 9) recordedAudio.delete(recordedAudio.keys().next().value);
  }
  return recordedAudio.get(path);
}

export function prepareRecordings(picks, language) {
  const [first, second] = picks;
  if (first) recording(clipPath(first.profile, "question_1", language)).catch(() => {});
  if (second) for (const key of ["question_2", "feedback"]) {
    recording(clipPath(second.profile, key, language)).catch(() => {});
  }
}

function voiceAudio(line, language, progress) {
  const voice = piperVoiceId(language, line.profile);
  const key = voice + "\n" + line.text;
  if (!synthesizedAudio.has(key)) {
    const promise = synthesize(line.text, voice, progress).catch(error => {
      synthesizedAudio.delete(key);
      throw error;
    });
    synthesizedAudio.set(key, promise);
    if (synthesizedAudio.size > 5) synthesizedAudio.delete(synthesizedAudio.keys().next().value);
  }
  return synthesizedAudio.get(key);
}

export function prepareLine(line, language) {
  if (line.text && !hasBrowserVoice(language)) voiceAudio(line, language).catch(() => {});
}

export async function speak(line, language, pan, { progress, onStart } = {}) {
  stopVoice();
  const turn = generation;
  if (line.voice && line.profile) {
    const path = clipPath(line.profile, line.voice, language);
    try {
      if (await sound(path, async () => (await recording(path)).slice(0), pan, turn, onStart)) return true;
    } catch {}
  }
  if (turn === generation && await browserVoice(line.text, language, line.profile, turn, onStart)) return true;
  if (turn !== generation) return false;
  try {
    const voice = await voiceAudio(line, language, progress);
    return await sound(null, () => voice.arrayBuffer(), pan, turn, onStart);
  } catch {
    return false;
  }
}

export class Microphone {
  constructor(report) {
    this.report = report;
    this.requests = new Map();
    this.serial = 0;
  }

  workerInstance() {
    if (!this.worker) {
      this.worker = new Worker("/speech-recognition.js", { type: "module", name: "nara-whisper" });
      this.worker.addEventListener("message", ({ data }) => {
        if (data.type === "loading") { this.report(data); return; }
        const request = this.requests.get(data.id);
        if (!request) return;
        this.requests.delete(data.id);
        clearTimeout(request.timer);
        if (data.type === "error") request.reject(new Error("transcription"));
        else request.resolve(data.text || "");
      });
      this.worker.addEventListener("error", () => this.resetWorker("speech-worker"));
    }
    return this.worker;
  }

  resetWorker(reason) {
    this.worker?.terminate();
    this.worker = null;
    this.loading = null;
    for (const request of this.requests.values()) {
      clearTimeout(request.timer);
      request.reject(new Error(reason));
    }
    this.requests.clear();
  }

  request(samples, language) {
    return new Promise((resolve, reject) => {
      const worker = this.workerInstance();
      const id = ++this.serial;
      const timer = setTimeout(() => this.resetWorker("speech-timeout"), 140000);
      this.requests.set(id, { resolve, reject, timer });
      worker.postMessage({ id, samples, language }, samples ? [samples.buffer] : []);
    });
  }

  warm() {
    if (this.loading) return this.loading;
    const ready = this.request().catch(error => {
      if (this.loading === ready) this.loading = null;
      throw error;
    });
    return this.loading = ready;
  }

  async start(onLimit) {
    if (!navigator.mediaDevices?.getUserMedia || !globalThis.MediaRecorder) throw new Error("unsupported");
    await this.release();
    this.stream = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true, channelCount: 1 } });
    try {
      const audio = audioUnlock();
      if (!audio) throw new Error("unsupported");
      const recorder = this.recorder = new MediaRecorder(this.stream);
      const chunks = [];
      recorder.addEventListener("dataavailable", ({ data }) => { if (data.size) chunks.push(data); });
      this.captured = new Promise((resolve, reject) => {
        recorder.addEventListener("stop", () => resolve(new Blob(chunks, { type: recorder.mimeType })), { once: true });
        recorder.addEventListener("error", () => reject(new Error("recording")), { once: true });
      });
      this.captured.catch(() => {});
      recorder.start();
      this.cutoff = setTimeout(onLimit, 60000);
      this.warm().catch(() => {});
    } catch (error) {
      await this.release();
      throw error;
    }
  }

  async release() {
    clearTimeout(this.cutoff);
    if (this.recorder?.state === "recording") this.recorder.stop();
    this.stream?.getTracks().forEach(track => track.stop());
    this.stream = null;
    this.recorder = null;
  }

  async finish(language) {
    const recording = this.captured;
    await this.release();
    const blob = await recording;
    if (!blob?.size) return "";
    const decoded = await context.decodeAudioData(await blob.arrayBuffer());
    if (decoded.duration < .2) return "";
    const offline = new OfflineAudioContext(1, Math.ceil(decoded.duration * 16000), 16000);
    const source = offline.createBufferSource();
    source.buffer = decoded;
    source.connect(offline.destination);
    source.start();
    const raw = (await offline.startRendering()).getChannelData(0);
    let rms = 0;
    for (let i = 0; i < raw.length; i += 4) rms += raw[i] * raw[i];
    if (Math.sqrt(4 * rms / raw.length) < .0022) return "";
    await this.warm();
    return this.request(raw, language);
  }
}
