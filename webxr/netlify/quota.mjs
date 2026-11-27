const defaultLimit = 120;
const jakartaOffset = 7 * 60 * 60 * 1000;

export async function reserveDailyTurn(store, { now = Date.now(), limit = defaultLimit } = {}) {
  const day = new Date(now + jakartaOffset).toISOString().slice(0, 10);
  for (let attempt = 0; attempt < 6; attempt++) {
    const entry = await store.getWithMetadata(day, { type: "json" });
    const count = entry ? entry.data.count : 0;
    if (!Number.isSafeInteger(count) || count < 0 || (entry && !entry.etag)) {
      throw new Error("Invalid shared quota data");
    }
    if (count >= limit) return false;
    const options = entry ? { onlyIfMatch: entry.etag } : { onlyIfNew: true };
    const write = await store.setJSON(day, { count: count + 1 }, options);
    if (write.modified) return true;
  }
  throw new Error("Shared quota contention");
}
