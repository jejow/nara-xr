import { Readable } from "node:stream";
import { defineConfig } from "vite";
import { handleTurn, turnAvailability } from "./src/api.js";

async function api(req, res, next) {
  const path = req.url?.split("?")[0];
  if (path === "/api/availability" && req.method === "GET") {
    const output = turnAvailability();
    res.writeHead(output.status, Object.fromEntries(output.headers));
    res.end(await output.text());
    return;
  }
  if (path !== "/api/turn") return next();
  const location = (req.socket.encrypted ? "https://" : "http://") + req.headers.host + req.url;
  const options = { method: req.method, headers: req.headers, signal: AbortSignal.timeout(22000) };
  if (req.method !== "GET" && req.method !== "HEAD") {
    options.body = Readable.toWeb(req);
    options.duplex = "half";
  }
  try {
    const output = await handleTurn(new Request(location, options));
    res.writeHead(output.status, Object.fromEntries(output.headers));
    res.end(Buffer.from(await output.arrayBuffer()));
  } catch {
    res.writeHead(502, { "Content-Type": "application/json" });
    res.end('{"error":"provider"}');
  }
}

export default defineConfig({
  worker: { format: "es" },
  build: { outDir: "dist/client", chunkSizeWarningLimit: 620 },
  plugins: [{
    name: "nara-turn",
    configureServer(server) { server.middlewares.use(api); },
    configurePreviewServer(server) { server.middlewares.use(api); }
  }]
});
