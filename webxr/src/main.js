import "./style.css";
import { layout } from "./layout.js";
import { copy } from "./copy.js";
import { loadSettings, saveSettings, readLocal, writeLocal, removeLocal, storeCredential, readCredential } from "./storage.js";
import { sampleDeck, importDeck } from "./deck.js";
import { PracticeSession } from "./session.js";
import { audioUnlock, speak, stopVoice, prepareVoice, prepareLine, prepareRecordings, Microphone } from "./speech.js";

const app = document.querySelector("#app");
app.innerHTML = layout;
const $ = (key) => document.getElementById(key);
const settings = loadSettings();
const t = () => copy[settings.language] || copy.id;
let deck = sampleDeck, pageIndex = 0, viewer, roster = [], profiles = [], importSerial = 0, roomSerial = 0;
let ready = false, xrAvailable = false, recording = false, micPending = false;
let localKey = "", keyResult = "", sessionMessage = "", roomMessage = "";
let sharedAI = false, checkingAI = true;
let microphoneState = "micIdle", microphonePercent = 0;
let voiceState = "voiceStandby", audioSignature = "", audioRevision = 0;
let speaking = null, spokenLine = null, lastLine = null, voiceSerial = 0;
let deckProgress = "", deckError = "", imageUrls = [];

const microphone = new Microphone(event => {
  microphonePercent = event.percent || 0;
  if (micPending) sessionMessage = t().loadingMic + (microphonePercent ? " · " + microphonePercent + "%" : "");
  paintMessages();
});

const session = new PracticeSession({
  script: [], report: refresh, play: playLine, stop: interruptVoice,
  prepare: (line, language) => {
    if (settings.audio && settings.preloadVoice) prepareLine(line, language);
  },
  finish: () => writeLocal("lastPractice", { date: Date.now(), deck: deck.title, room: settings.room,
    time: settings.minutes * 60 - session.remaining, turns: session.turns, feedback: session.feedback }).catch(() => {})
});

function applyCopy() {
  document.documentElement.lang = settings.language;
  for (const node of app.querySelectorAll("[data-copy]")) node.textContent = t()[node.dataset.copy];
  $("topic").placeholder = t().topicHint;
  $("summary").placeholder = t().summaryHint;
  $("prev").setAttribute("aria-label", t().previous);
  $("next").setAttribute("aria-label", t().following);
  $("xr-note").textContent = xrAvailable ? "" : t().xrUnavailable;
  $("enter-vr").title = xrAvailable ? "" : t().xrUnavailable;
  $("key-state").textContent = keyResult || (localKey ? t().keySaved : t().noKey);
  $("mode").setAttribute("aria-label", t().mode);
  $("help-button").setAttribute("aria-label", t().helpButton);
  $("camera-controls").setAttribute("aria-label", t().cameraView);
  paintViewControls();
  for (const button of app.querySelectorAll("[data-room]")) button.setAttribute("aria-pressed", String(settings.room === button.dataset.room));
  refresh();
}

const clock = seconds => Math.floor(seconds / 60) + ":" + String(seconds % 60).padStart(2, "0");
const hasAI = () => Boolean(sharedAI || localKey);
const wantsAI = () => settings.mode !== "offline" && hasAI();

function paintViewControls() {
  for (const button of app.querySelectorAll("[data-view]")) {
    button.setAttribute("aria-pressed", String(button.dataset.view === settings.view));
  }
  const expanded = document.fullscreenElement === $("stage");
  $("fullscreen").hidden = !document.fullscreenEnabled;
  $("fullscreen").setAttribute("aria-pressed", String(expanded));
  $("fullscreen-text").textContent = expanded ? t().exitFullscreen : t().fullscreen;
}

function paintReadiness() {
  const labels = t();
  const online = wantsAI();
  const awaiting = settings.mode !== "offline" && !online && checkingAI;
  const mode = labels[online ? "aiReady" : awaiting ? "availableCheck" : "offline"];
  const voice = labels[settings.audio ? voiceState : "voiceOff"];
  if ($("mode-text").textContent !== mode) $("mode-text").textContent = mode;
  if ($("voice-state").textContent !== voice) $("voice-state").textContent = voice;
  $("ai-badge").dataset.state = online ? "ready" : awaiting ? "loading" : "idle";
  $("voice-badge").dataset.state = settings.audio && voiceState === "voiceReady" ? "ready" :
    settings.audio && voiceState === "voiceWarm" ? "loading" : "idle";
  $("service-state").textContent = labels[localKey ? "personalSource" : sharedAI ? "managedSource" :
    checkingAI ? "availableCheck" : "unavailableSource"];
}

async function preloadMicrophone() {
  if (microphoneState === "loadingMic" || microphoneState === "modelReady") return;
  microphonePercent = 0;
  microphoneState = "loadingMic";
  paintMessages();
  try {
    await microphone.warm();
    microphoneState = "modelReady";
  } catch {
    microphoneState = "micError";
  }
  paintMessages();
}

function preloadAudio() {
  const useAI = wantsAI();
  const signature = [settings.language, useAI, settings.audio, settings.preloadVoice].join(":");
  if (signature === audioSignature) return;
  audioSignature = signature;
  const revision = ++audioRevision;
  if (!settings.preloadVoice) {
    voiceState = "voiceStandby";
    paintReadiness();
    return;
  }
  const language = settings.language;
  voiceState = useAI ? "voiceWarm" : "voiceRecorded";
  paintReadiness();
  setTimeout(async () => {
    if (revision !== audioRevision) return;
    preloadMicrophone();
    if (!useAI || !settings.audio) return;
    try {
      await prepareVoice(language, roster[0]?.profile);
      if (revision === audioRevision) voiceState = "voiceReady";
    } catch {
      if (revision === audioRevision) voiceState = "voiceStandby";
    }
    if (revision === audioRevision) paintReadiness();
  }, 170);
}

async function checkAI() {
  try {
    const response = await fetch("/api/availability", { cache: "no-store", signal: AbortSignal.timeout(5000) });
    if (response.ok) sharedAI = (await response.json()).managed === true;
  } catch {}
  checkingAI = false;
  if (session.phase === "presenting") session.context.managed = sharedAI;
  applyCopy();
  preloadAudio();
}

function layoutInfo() {
  if (deckError || deckProgress) return deckError || deckProgress;
  if (deck === sampleDeck) return t().formats;
  const parts = [];
  if (deck.layout === "basic") parts.push(t().drawn);
  if (deck.layout === "text") parts.push(t().old);
  if (deck.actual > deck.pages.length) parts.push(t().deckTrimmed);
  if (!parts.length && deck.pages.every(page => !page.text)) parts.push(t().slideOcr);
  return parts.join(" ");
}

function paintMessages() {
  const notice = session.notice === "offline" ? "provider" : session.notice;
  const parts = [sessionMessage, t()[notice], roomMessage].filter(Boolean);
  $("session-message").hidden = !parts.length;
  $("session-message").textContent = parts.join(" ");
  const micText = t()[microphoneState] + (microphoneState === "loadingMic" && microphonePercent ? " · " + microphonePercent + "%" : "");
  if ($("mic-settings").textContent !== micText) $("mic-settings").textContent = micText;
  $("prepare-mic").disabled = microphoneState === "loadingMic" || microphoneState === "modelReady";
  $("deck-note").textContent = layoutInfo();
}

function phaseView() {
  const labels = t();
  const phase = session.phase;
  const presenting = phase === "presenting";
  const answering = phase === "answer";
  const primary = { setup: labels.start, presenting: labels.endTalk, invite: labels.grant,
    thinking: labels.quickQuestion, answer: $("draft").value.trim() ? labels.send : "", done: labels.again }[phase];
  const who = phase === "invite" && session.floor?.profile.label;
  const detail = who ? (settings.language === "id" ? who + " ingin bertanya." : who + " has a question.") : {
    setup: labels.readyDetail, presenting: labels.presentingDetail, thinking: labels.thinkingDetail, done: session.feedback?.text || ""
  }[phase] || "";
  const line = session.caption || spokenLine;
  return {
    room: labels[settings.room], title: labels[phase], detail, caption: line?.text, line,
    primary: ready ? primary : "", canType: presenting || answering, canPause: presenting, canSkip: answering,
    canReplay: !!lastLine && !presenting, micActive: recording, busy: recording || micPending,
    micLabel: micPending ? labels.waitMic : recording ? labels.stopMic : labels.mic, pauseLabel: session.paused ? labels.resume : labels.pause,
    clock: clock(phase === "setup" ? settings.minutes * 60 : session.remaining),
    slide: labels.slide + " " + (pageIndex + 1) + "/" + deck.pages.length,
    labels, draft: $("draft").value, audio: settings.audio,
    help: sessionMessage || labels[session.notice === "offline" ? "provider" : session.notice] || ""
  };
}

function refresh() {
  const phase = session.phase;
  const view = phaseView();
  $("setup").hidden = phase !== "setup";
  $("enter-vr").disabled = !xrAvailable || !ready;
  $("room-tag").textContent = view.room;
  paintReadiness();
  $("count-tag").textContent = roster.length ? roster.length + " " + t().count : "";
  $("phase-title").textContent = view.title;
  $("session").dataset.phase = phase;
  $("timer").textContent = view.clock;
  $("time-bar").hidden = phase !== "presenting";
  $("time-bar").max = settings.minutes * 60;
  $("time-bar").value = session.remaining;
  $("phase-note").hidden = !view.detail || phase === "done";
  $("phase-note").textContent = view.detail;
  const line = phase === "done" ? session.feedback : view.line;
  $("session-caption").hidden = !line;
  $("speaker-name").textContent = phase === "done" ? t().feedback : line?.profile?.label || "";
  $("speech-text").textContent = line?.text || "";
  $("primary").hidden = !view.primary || phase === "answer";
  $("primary").textContent = view.primary || "";
  $("primary").disabled = (phase === "setup" && (!ready || roster.length < 2)) || view.busy;
  $("write-form").hidden = !view.canType;
  $("draft").placeholder = phase === "answer" ? t().typeAnswer : t().typePresentation;
  $("send").textContent = phase === "answer" ? t().send : t().addNote;
  $("send").disabled = !$("draft").value.trim() || view.busy;
  $("mic").textContent = view.micLabel;
  $("mic").hidden = !view.canType;
  $("mic").disabled = micPending;
  $("mic").classList.toggle("recording", recording);
  $("pause").hidden = !view.canPause;
  $("pause").textContent = view.pauseLabel;
  $("replay").hidden = !view.canReplay;
  $("skip").hidden = !view.canSkip;
  $("skip").disabled = view.busy;
  $("pick-deck").disabled = phase !== "setup";
  $("use-sample").hidden = deck === sampleDeck || phase !== "setup";
  $("prev").disabled = pageIndex === 0;
  $("next").disabled = pageIndex === deck.pages.length - 1;
  paintMessages();
  renderHistory();
  viewer?.setDialogue({ raised: ["invite", "thinking"].includes(phase) ? session.floor?.seat : null,
    speaking, speaker: view.line?.seat ?? null, caption: view.line?.text || (phase === "invite" ? view.detail : "") }, view);
}

function renderHistory() {
  $("history").hidden = session.phase !== "done" || !session.turns.length;
  if ($("history").hidden) return;
  const rows = session.turns.map(turn => {
    const li = document.createElement("li");
    const label = document.createElement("b");
    label.textContent = turn.line.profile?.label || t().question;
    const prompt = document.createElement("p");
    prompt.textContent = turn.line.text;
    const answer = document.createElement("p");
    answer.className = "subtle";
    answer.textContent = turn.answer || t().skipped;
    li.append(label, prompt, answer);
    return li;
  });
  $("history").replaceChildren(...rows);
}

function pageTiles() {
  imageUrls.forEach(url => URL.revokeObjectURL(url));
  imageUrls = [];
  const first = Math.min(Math.max(pageIndex - 1, 0), Math.max(0, deck.pages.length - 3));
  $("thumbnails").replaceChildren(...deck.pages.slice(first, first + 3).map((page, offset) => {
    const i = offset + first;
    const tile = document.createElement("button");
    tile.type = "button";
    tile.className = "tile";
    tile.setAttribute("aria-current", i === pageIndex ? "page" : "false");
    tile.setAttribute("aria-label", t().slide + " " + (i + 1));
    const img = document.createElement("img");
    img.alt = "";
    if (page.blob) {
      img.src = URL.createObjectURL(page.blob);
      imageUrls.push(img.src);
    } else img.src = page.src;
    const label = document.createElement("span");
    label.textContent = String(i + 1).padStart(2, "0");
    tile.append(img, label);
    tile.onclick = () => moveSlide(i);
    return tile;
  }));
}

function moveSlide(index, force = false) {
  const target = Math.min(deck.pages.length - 1, Math.max(0, index));
  if (pageIndex === target && !force) return;
  pageIndex = target;
  $("page-count").textContent = (pageIndex + 1) + " / " + deck.pages.length;
  $("deck-filename").textContent = deck.title;
  viewer?.setSlide(deck.pages[pageIndex]);
  session.recordPage(pageIndex);
  pageTiles();
  refresh();
}

async function loadRoom() {
  const serial = ++roomSerial;
  ready = false;
  roomMessage = "";
  roster = [];
  $("stage-loading").hidden = false;
  $("load-progress").textContent = "";
  refresh();
  try {
    if (!viewer) await bootRenderer();
    const result = await viewer.load(settings.room, profiles, (loaded, count) => {
      if (serial === roomSerial) $("load-progress").textContent = loaded + " / " + count;
    });
    if (serial !== roomSerial || !result) return;
    roster = viewer.audience.people.flatMap((person, seat) => person ? [{ profile: person.profile, seat }] : []);
    ready = true;
    preloadAudio();
    if (result.failed.length || !viewer.audience.motions) roomMessage = t().partialRoom;
  } catch (error) {
    console.error(error);
    if (serial === roomSerial) roomMessage = t().badRoom;
  } finally {
    if (serial === roomSerial) {
      $("stage-loading").hidden = true;
      refresh();
    }
  }
}

let rendererBoot;
function bootRenderer() {
  return rendererBoot ??= import("./classroom.js").then(({ Classroom }) => {
    viewer = new Classroom($("viewport"), $("bubble"), action);
    viewer.setQuality(settings.quality);
    viewer.positionPose(settings.view);
    viewer.setSlide(deck.pages[pageIndex]);
  });
}

function interruptVoice() {
  voiceSerial++;
  stopVoice();
  speaking = null;
  spokenLine = null;
}

async function playLine(line) {
  lastLine = line;
  if (!settings.audio || !line?.text) { refresh(); return; }
  interruptVoice();
  const id = voiceSerial;
  spokenLine = line;
  refresh();
  const heard = await speak(line, settings.language, viewer?.panFor(line.seat), {
    progress: () => {
      if (id !== voiceSerial) return;
      sessionMessage = t().voiceLoading;
      paintMessages();
    },
    onStart: () => {
      if (id !== voiceSerial) return;
      speaking = line.seat ?? null;
      sessionMessage = "";
      refresh();
    }
  });
  if (id !== voiceSerial) return;
  speaking = null;
  spokenLine = null;
  sessionMessage = heard ? "" : t().voiceError;
  refresh();
}

async function micAction() {
  if (!["presenting", "answer"].includes(session.phase) || micPending) return;
  if (recording) {
    recording = false;
    micPending = true;
    sessionMessage = t().waitMic;
    refresh();
    try {
      const transcript = await microphone.finish(settings.language);
      $("draft").value = [$("draft").value, transcript].filter(Boolean).join(" ").slice(0, 1900);
      sessionMessage = transcript ? "" : t().noSpeech;
    } catch { sessionMessage = t().micError; }
    micPending = false;
    refresh();
    return;
  }
  micPending = true;
  interruptVoice();
  audioUnlock();
  refresh();
  try {
    await microphone.start(micAction);
    recording = true;
    sessionMessage = t().listening;
  } catch { sessionMessage = t().micError; }
  micPending = false;
  refresh();
}

async function submit(skip) {
  if (micPending || recording) return;
  const draft = $("draft").value.trim();
  if (session.phase === "answer" && (draft || skip)) {
    $("draft").value = "";
    sessionMessage = "";
    lastLine = null;
    session.answer(draft, skip);
  } else if (!skip && session.addNotes(draft)) {
    $("draft").value = "";
    sessionMessage = t().noteSaved;
  }
}

async function primary() {
  if (micPending || recording) return;
  audioUnlock();
  if (session.phase === "setup" && ready && roster.length >= 2) {
    const { shuffle } = await import("./audience.js");
    if (session.phase !== "setup") return;
    lastLine = null;
    sessionMessage = "";
    $("draft").value = "";
    session.start(pageIndex, shuffle(roster), { ...settings,
      mode: settings.mode === "offline" ? "offline" : "live", secret: localKey, managed: sharedAI }, deck);
    if (settings.mode === "live" && !hasAI() && !checkingAI) session.notice = "key";
    if (settings.audio && settings.preloadVoice) {
      prepareRecordings(session.picks, session.language);
      if (session.canAskAI) prepareVoice(session.language, session.picks[0]?.profile).catch(() => {});
    }
  } else if (session.phase === "presenting") {
    session.addNotes($("draft").value);
    $("draft").value = "";
    sessionMessage = "";
    session.invite();
  } else if (session.phase === "invite") return session.reveal();
  else if (session.phase === "thinking") session.useOfflineQuestion();
  else if (session.phase === "answer") return submit(false);
  else if (session.phase === "done") {
    interruptVoice();
    lastLine = null;
    sessionMessage = "";
    session.reset();
  }
}

async function action(key) {
  if (key.startsWith("character:")) $("draft").value = ($("draft").value + key.slice(10)).slice(0, 1900);
  else if (key === "delete") $("draft").value = $("draft").value.slice(0, -1);
  else if (key === "xrstart") audioUnlock();
  else if (key === "xrend" && recording) return micAction();
  else if (key === "prev" || key === "next") return moveSlide(pageIndex + (key === "next" ? 1 : -1));
  else if (key === "mic") return micAction();
  else if (key === "audio-toggle") {
    $("audio").checked = !settings.audio;
    $("audio").dispatchEvent(new Event("change"));
    if (settings.audio) audioUnlock();
  }
  else if (key === "pause") session.togglePause();
  else if (key === "replay" && lastLine && !recording && !micPending) {
    audioUnlock();
    return playLine(lastLine);
  } else if (key === "send" || key === "skip") await submit(key === "skip");
  else if (key === "primary") await primary();
  refresh();
}

function bindSettings() {
  for (const id of ["minutes", "language", "topic", "summary", "mode", "quality", "audio", "preloadVoice", "ocr"]) {
    const field = $(id);
    if (field.type === "checkbox") field.checked = Boolean(settings[id]);
    else field.value = settings[id] ?? "";
    field.addEventListener(field.tagName === "SELECT" || field.type === "checkbox" ? "change" : "input", () => {
      settings[id] = field.type === "checkbox" ? field.checked : id === "minutes" ? Number(field.value) : field.value;
      saveSettings(settings);
      if (id === "quality") viewer?.setQuality(settings.quality);
      if (id === "audio" && !settings.audio) interruptVoice();
      if (id === "language") { keyResult = ""; applyCopy(); pageTiles(); }
      else refresh();
      if (["language", "mode", "audio", "preloadVoice"].includes(id)) preloadAudio();
    });
  }
  $("save-key").onclick = async () => {
    try {
      const value = $("token").value.trim();
      await storeCredential(value);
      localKey = value;
      $("token").value = "";
      keyResult = t().keySaved;
    } catch { keyResult = t().invalidKey; }
    applyCopy();
    preloadAudio();
  };
  $("clear-key").onclick = async () => {
    try {
      await removeLocal("credential");
      localKey = "";
      if (session.context) session.context.secret = "";
      keyResult = "";
      $("token").value = "";
    } catch { keyResult = t().invalidKey; }
    applyCopy();
    preloadAudio();
  };
  $("prepare-mic").onclick = preloadMicrophone;
}

function bindDeck() {
  $("pick-deck").onclick = () => $("file").click();
  $("use-sample").onclick = () => {
    importSerial++;
    deck = sampleDeck;
    deckError = deckProgress = "";
    removeLocal("deck").catch(() => {});
    moveSlide(0, true);
  };
  $("file").onchange = async () => {
    const file = $("file").files[0];
    $("file").value = "";
    if (!file) return;
    const id = ++importSerial;
    deckError = "";
    deckProgress = t().importing + "…";
    paintMessages();
    try {
      const data = await importDeck(file, (number, limit) => {
        if (id !== importSerial) return;
        deckProgress = t().importing + " " + number + " / " + limit;
        paintMessages();
      }, settings.language, settings.ocr);
      if (id !== importSerial) return;
      deck = data;
      deckProgress = "";
      writeLocal("deck", data).catch(() => {});
      moveSlide(0, true);
    } catch (error) {
      if (id !== importSerial) return;
      deckProgress = "";
      deckError = error.message;
      paintMessages();
    }
  };
}

function bindEvents() {
  bindSettings();
  bindDeck();
  for (const button of app.querySelectorAll("[data-room]")) button.onclick = () => {
    if (settings.room === button.dataset.room || session.phase !== "setup") return;
    settings.room = button.dataset.room;
    saveSettings(settings);
    applyCopy();
    loadRoom();
  };
  for (const button of app.querySelectorAll("[data-view]")) button.onclick = () => {
    settings.view = button.dataset.view;
    saveSettings(settings);
    viewer?.positionPose(settings.view);
    paintViewControls();
  };
  $("fullscreen").onclick = async () => {
    try {
      if (document.fullscreenElement === $("stage")) await document.exitFullscreen();
      else await $("stage").requestFullscreen();
    } catch {}
  };
  document.addEventListener("fullscreenchange", paintViewControls);
  for (const button of app.querySelectorAll("[data-dismiss]")) button.onclick = () => $(button.dataset.dismiss).close();
  $("settings-button").onclick = () => $("settings").showModal();
  $("help-button").onclick = () => $("help").showModal();
  $("enter-vr").onclick = async () => {
    if (!ready || !xrAvailable) return;
    audioUnlock();
    try { await viewer.enter(); } catch { sessionMessage = t().xrUnavailable; refresh(); }
  };
  for (const key of ["primary", "mic", "pause", "skip", "replay", "prev", "next"]) $(key).onclick = () => action(key);
  $("draft").oninput = refresh;
  $("write-form").onsubmit = event => { event.preventDefault(); action("send"); };
  addEventListener("keydown", event => {
    if (event.target.matches?.("input,textarea,select,button") || app.querySelector("dialog[open]")) return;
    if (event.key === "ArrowLeft" || event.key === "ArrowRight") {
      event.preventDefault();
      action(event.key === "ArrowLeft" ? "prev" : "next");
    }
  });
}

async function initialize() {
  bindEvents();
  applyCopy();
  moveSlide(0, true);
  if (navigator.xr?.isSessionSupported) navigator.xr.isSessionSupported("immersive-vr").then(supported => {
    xrAvailable = supported;
    applyCopy();
  }).catch(() => {});
  checkAI();
  readCredential().then(value => {
    localKey = value;
    applyCopy();
    preloadAudio();
  }).catch(() => {});
  readLocal("deck").then(value => {
    if (value?.pages?.length && !importSerial) { deck = value; moveSlide(0, true); }
  }).catch(() => {});
  try {
    const [{ shuffle }, catalog, script] = await Promise.all([
      import("./audience.js"), fetch("/media/AvatarCatalog.json").then(response => response.json()),
      fetch("/media/OfflineScript.json").then(response => response.json())
    ]);
    profiles = shuffle(catalog.entries).slice(0, 24);
    session.script = script.lines;
    await loadRoom();
  } catch (error) {
    console.error(error);
    roomMessage = t().badRoom;
    $("stage-loading").hidden = true;
    refresh();
  }
  setInterval(() => {
    if (session.phase !== "presenting") return;
    const before = session.remaining;
    session.tick();
    if (before !== session.remaining) refresh();
  }, 245);
}
initialize();
