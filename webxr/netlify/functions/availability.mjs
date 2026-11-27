import { turnAvailability } from "../../src/api.js";
import { readManagedKey } from "../managed.mjs";

export const config = { path: "/api/availability", method: "GET" };

export default async function availability() {
  return turnAvailability(await readManagedKey());
}
