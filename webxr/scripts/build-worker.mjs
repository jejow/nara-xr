import { mkdir, readFile, writeFile } from "node:fs/promises";

const out = new URL("../dist/server/", import.meta.url);
await mkdir(out, { recursive: true });
const handler = await readFile(new URL("../src/api.js", import.meta.url), "utf8");
await writeFile(new URL("index.js", out), handler + `
export default {
  async fetch(request, env) {
    const path = new URL(request.url).pathname;
    if (path === "/api/turn") return handleTurn(request);
    if (path === "/api/availability" && request.method === "GET") return turnAvailability();
    return env.ASSETS.fetch(request);
  }
};
`);
