import { getStore } from "@netlify/blobs";

let keyRead;
let expiresAt = 0;

export function readManagedKey() {
  const configured = process.env.NARA_AISUBS_API_KEY?.trim();
  if (configured) return Promise.resolve(configured);
  if (!keyRead || Date.now() >= expiresAt) {
    expiresAt = Date.now() + 60000;
    keyRead = Promise.resolve().then(() => getStore({ name: "nara-runtime", consistency: "strong" })
      .get("aisubs-key", { type: "text" })).then(value => value?.trim() || "")
      .catch(() => { expiresAt = 0; return ""; });
  }
  return keyRead;
}
