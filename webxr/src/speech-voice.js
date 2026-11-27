import * as piper from "@mintplex-labs/piper-tts-web";
import runtimeModule from "../node_modules/onnxruntime-web/dist/ort-wasm-simd-threaded.mjs?url";
import runtimeBinary from "../node_modules/onnxruntime-web/dist/ort-wasm-simd-threaded.wasm?url";

const indonesianVoice = "id_ID-news_tts-medium";
const indonesianModel = "id/id_ID/news_tts/medium/" + indonesianVoice + ".onnx";
piper.PATH_MAP[indonesianVoice] = indonesianModel;
const mirroredModel = piper.HF_BASE + "/" + indonesianModel;
const officialModel = "https://huggingface.co/rhasspy/piper-voices/resolve/main/" + indonesianModel;

// Extend the package's voice catalog with the model hosted by Piper.
const fetchModel = self.fetch.bind(self);
self.fetch = (input, options) => {
  const url = typeof input === "string" ? input : input instanceof URL ? input.href : input.url;
  if (url === mirroredModel) return fetchModel(officialModel, options);
  if (url === mirroredModel + ".json") {
    return fetchModel(officialModel + ".json", options).then(async response => {
      if (!response.ok) return response;
      const config = await response.json();
      return Response.json({ ...config, speaker_id_map: config.speaker_id_map || {} });
    });
  }
  return fetchModel(input, options);
};

const wasmPaths = {
  ...piper.TtsSession.WASM_LOCATIONS,
  onnxWasm: { mjs: new URL(runtimeModule, self.location.origin).href,
    wasm: new URL(runtimeBinary, self.location.origin).href }
};

let queue = Promise.resolve();
let session;
let currentVoice;
let primed = false;
self.onmessage = ({ data: { id, text, voice, warm } }) => {
  queue = queue.then(async () => {
    try {
      const report = (progress) => {
        if (progress.total) self.postMessage({ id, status: "progress", percent: Math.round(100 * progress.loaded / progress.total) });
      };
      if (!session || currentVoice !== voice) {
        session = undefined;
        session = await piper.TtsSession.create({ voiceId: voice, progress: report, wasmPaths });
        currentVoice = voice;
        primed = false;
      }
      if (warm && !primed) await session.predict(voice === indonesianVoice ? "Halo." : "Ready.");
      const audio = warm ? undefined : await session.predict(text);
      primed = true;
      self.postMessage({ id, status: "done", audio });
    } catch {
      self.postMessage({ id, status: "error" });
    }
  });
};
