const library = "https://cdn.jsdelivr.net/npm/@huggingface/transformers@3.8.1/dist/transformers.min.js";
let hearing;

async function transcriber() {
  return hearing ??= (async () => {
    const { pipeline, env } = await import(library);
    env.allowLocalModels = false;
    env.useBrowserCache = true;
    env.backends.onnx.wasm.numThreads = 1;
    return pipeline("automatic-speech-recognition", "onnx-community/whisper-tiny", {
      device: "wasm",
      dtype: "q8",
      progress_callback: item => {
        if (["initiate", "progress", "done"].includes(item.status)) {
          self.postMessage({ type: "loading", file: item.file, percent: Math.round(item.progress || 0) });
        }
      }
    });
  })().catch(error => { hearing = undefined; throw error; });
}

self.onmessage = async ({ data }) => {
  try {
    const run = await transcriber();
    if (!data.samples) return self.postMessage({ type: "ready", id: data.id });
    const result = await run(data.samples, { language: data.language === "id" ? "indonesian" : "english",
      task: "transcribe", return_timestamps: false, chunk_length_s: 27, stride_length_s: 4, max_new_tokens: 180 });
    const text = (result.text || "").replace(/\[[^\]]*\]/g, "").trim();
    self.postMessage({ type: "result", id: data.id, text });
  } catch {
    self.postMessage({ type: "error", id: data.id });
  }
};
