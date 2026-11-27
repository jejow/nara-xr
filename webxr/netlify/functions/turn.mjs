import { getStore } from "@netlify/blobs";
import { handleTurn } from "../../src/api.js";
import { reserveDailyTurn } from "../quota.mjs";
import { readManagedKey } from "../managed.mjs";

export const config = {
  path: "/api/turn",
  rateLimit: { action: "rate_limit", aggregateBy: ["ip", "domain"], windowSize: 60, windowLimit: 30 }
};

export default async function turn(request) {
  const managedKey = request.method === "POST" && !request.headers.get("X-Nara-Key") ? await readManagedKey() : "";
  return handleTurn(request, fetch, {
    managedKey,
    reserveManaged: () => reserveDailyTurn(getStore({ name: "nara-shared-ai", consistency: "strong" }))
  });
}
