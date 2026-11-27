import { readFile, stat } from "node:fs/promises";

const media = new URL("../public/media/", import.meta.url);
const required = [
  "AvatarCatalog.json", "OfflineScript.json", "SeatedMotion.bytes", "OverteMotion.bytes",
  "rooms/university.glb", "rooms/university.json", "rooms/if110.glb", "rooms/if110.json",
  "fonts/Inter-Regular.ttf", "fonts/Inter-Bold.ttf",
  "slides/slide_1.jpg", "slides/slide_2.jpg", "slides/slide_3.jpg"
];

try {
  const catalog = JSON.parse((await readFile(new URL("AvatarCatalog.json", media), "utf8")).replace(/^\uFEFF/, ""));
  const avatars = catalog.entries.map(({ id }) => id);
  if (new Set(avatars).size < 24 || avatars.some(id => !/^[a-zA-Z0-9_-]+$/.test(id))) {
    throw new Error("Katalog harus menyediakan minimal 24 avatar dengan ID yang valid.");
  }
  required.push(...avatars.map(id => "avatars/" + id + ".glb"));
  const missing = await Promise.all(required.map(async name => {
    const file = await stat(new URL(name, media)).catch(() => null);
    return file?.isFile() && file.size ? null : name;
  }));
  const absent = missing.filter(Boolean);
  if (absent.length) throw new Error("Aset belum tersedia: " + absent.join(", "));
  console.log(`Aset Netlify siap untuk 2 ruang dan ${avatars.length} avatar.`);
} catch (error) {
  console.error("Build Netlify memerlukan public/media. Ekspor aset NARA sesuai README.md.");
  console.error(error.message);
  process.exitCode = 1;
}
