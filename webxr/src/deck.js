export const sampleDeck = {
  title: "Contoh presentasi NARA",
  pages: [
    { src: "/media/slides/slide_1.jpg", text: "NARA, ruang simulasi presentasi dengan audiens virtual." },
    { src: "/media/slides/slide_2.jpg", text: "Ruang latihan interaktif dengan materi presentasi, audiens, dan sesi tanya jawab." },
    { src: "/media/slides/slide_3.jpg", text: "Pengguna berlatih menyampaikan materi, menanggapi pertanyaan, dan mengevaluasi latihan." }
  ]
};

const maxPages = 60;
const maxFile = 35 * 1024 * 1024;
const localName = (tag) => tag.split(":").at(-1);
const all = (node, name) => node ? Array.from(node.getElementsByTagName("*")).filter(el => el.localName === name) : [];
const first = (node, name) => all(node, name)[0];
const child = (node, name) => Array.from(node?.children || []).find(el => el.localName === name);
const attr = (node, name, defaultValue = "") => node?.getAttribute(name) ?? defaultValue;
const relationshipId = (node, name) => node?.getAttributeNS("http://schemas.openxmlformats.org/officeDocument/2006/relationships", name) || attr(node, "r:" + name);
const n = (node, name, fallback = 0) => Number(attr(node, name, fallback)) || fallback;

function xml(raw) {
  const doc = new DOMParser().parseFromString(raw, "application/xml");
  if (doc.querySelector("parsererror")) throw new Error("Isi XML presentasi rusak.");
  return doc;
}

function imageSource(blob) {
  const url = URL.createObjectURL(blob);
  const img = new Image();
  img.src = url;
  return img.decode().then(() => img).finally(() => URL.revokeObjectURL(url));
}

function artwork(width = 1280, height = 720) {
  const canvas = document.createElement("canvas");
  canvas.width = width;
  canvas.height = height;
  return canvas;
}

function imageBlob(canvas) {
  return new Promise((resolve, reject) => canvas.toBlob(
    blob => blob ? resolve(blob) : reject(new Error("Pratinjau gambar gagal dibuat.")), "image/jpeg", .85
  ));
}

function summarize(text) { return text.replace(/\s+/g, " ").trim().slice(0, 7000); }

async function pdfPages(file, progress) {
  const [pdf, worker] = await Promise.all([import("pdfjs-dist/legacy/build/pdf.mjs"), import("pdfjs-dist/legacy/build/pdf.worker.min.mjs?url")]);
  pdf.GlobalWorkerOptions.workerSrc = worker.default;
  const doc = await pdf.getDocument({ data: new Uint8Array(await file.arrayBuffer()), isEvalSupported: false }).promise;
  const pages = [];
  try {
    for (let i = 1; i <= Math.min(maxPages, doc.numPages); i++) {
      progress(i, Math.min(maxPages, doc.numPages));
      const page = await doc.getPage(i);
      const bounds = page.getViewport({ scale: 1 });
      const viewport = page.getViewport({ scale: Math.min(2.4, 1440 / Math.max(bounds.width, bounds.height)) });
      const canvas = artwork(Math.ceil(viewport.width), Math.ceil(viewport.height));
      const [content] = await Promise.all([
        page.getTextContent(),
        page.render({ canvas, viewport, background: "#ffffff" }).promise
      ]);
      pages.push({ blob: await imageBlob(canvas), text: summarize(content.items.map(item => item.str || "").join(" ")) });
      page.cleanup();
    }
    return { pages, actual: doc.numPages };
  } finally {
    await doc.destroy();
  }
}

function wrapText(ctx, text, width) {
  const result = [];
  for (const paragraph of text.split("\n")) {
    let line = "";
    for (const word of paragraph.split(/\s+/)) {
      const joined = line ? line + " " + word : word;
      if (line && ctx.measureText(joined).width > width) {
        result.push(line);
        line = word;
      } else line = joined;
    }
    result.push(line);
  }
  return result;
}

function hexColor(node, palette, fallback) {
  const rgb = first(node, "srgbClr");
  const scheme = first(node, "schemeClr");
  const system = first(node, "sysClr");
  if (rgb && /^[0-9a-f]{6}$/i.test(attr(rgb, "val"))) return "#" + attr(rgb, "val");
  if (scheme) return palette[attr(scheme, "val")] || fallback;
  if (system && /^[0-9a-f]{6}$/i.test(attr(system, "lastClr"))) return "#" + attr(system, "lastClr");
  return fallback;
}

function absolutePart(context, target) {
  if (!target || /^[a-z]+:/i.test(target)) return "";
  const segments = target.startsWith("/") ? [] : context.split("/").slice(0, -1);
  for (const segment of target.split("/")) {
    if (segment === "..") segments.pop();
    else if (segment && segment !== ".") segments.push(segment);
  }
  return segments.join("/");
}

class OfficeArchive {
  constructor(zip) { this.zip = zip; }
  async read(path, type = "string", limit = 12 * 1024 * 1024) {
    const part = this.zip.file(path);
    if (!part) return null;
    if (part._data?.uncompressedSize > limit) throw new Error("Salah satu bagian PowerPoint terlalu besar.");
    return part.async(type);
  }
  async doc(path) {
    const raw = await this.read(path);
    return raw ? xml(raw) : null;
  }
  async relationships(path) {
    const segments = path.split("/");
    const basename = segments.pop();
    const doc = await this.doc([...segments, "_rels", basename + ".rels"].join("/"));
    return new Map(all(doc, "Relationship").filter(el => attr(el, "TargetMode") !== "External").map(el =>
      [attr(el, "Id"), { path: absolutePart(path, attr(el, "Target")), type: attr(el, "Type").split("/").at(-1) }]));
  }
}

function position(shape, scaleX, scaleY) {
  const transform = child(child(shape, "spPr"), "xfrm") || child(shape, "xfrm");
  if (!transform) return null;
  const offset = child(transform, "off");
  const ext = child(transform, "ext");
  return { x: n(offset, "x") * scaleX, y: n(offset, "y") * scaleY,
    w: Math.max(1, n(ext, "cx") * scaleX), h: Math.max(1, n(ext, "cy") * scaleY) };
}

function placeholder(shape) {
  const element = first(shape, "ph");
  if (!element) return "";
  return attr(element, "idx") || attr(element, "type", "body");
}

function paragraphs(shape) { return all(child(shape, "txBody"), "p"); }

function drawText(ctx, shape, box, palette, scaleText, inherited) {
  const defaultFont = ["title", "ctrTitle"].includes(placeholder(shape)) ? 32 : 20;
  const body = child(child(shape, "txBody"), "bodyPr");
  const margin = Math.min(24, Math.max(6, n(body, "lIns", 90000) * scaleText / 12700));
  let y = box.y + Math.min(margin, box.h * .1);
  const lines = paragraphs(shape);
  ctx.save();
  ctx.beginPath();
  ctx.rect(box.x, box.y, box.w, box.h);
  ctx.clip();
  for (const line of lines) {
    const value = all(line, "t").map(t => t.textContent).join("");
    const props = child(line, "pPr");
    const style = first(line, "rPr") || child(props, "defRPr") || first(inherited, "defRPr");
    const size = Math.max(10, Math.min(116, n(style, "sz", defaultFont * 100) / 100 * scaleText));
    const font = attr(style, "b") === "1" || defaultFont === 32 ? "700 " : "400 ";
    const alignment = attr(props, "algn");
    const bullet = Boolean(first(props, "buChar"));
    ctx.font = font + size + "px Arial, sans-serif";
    ctx.textBaseline = "top";
    ctx.textAlign = alignment === "ctr" ? "center" : alignment === "r" ? "right" : "left";
    ctx.fillStyle = hexColor(style, palette, palette.tx1);
    const x = alignment === "ctr" ? box.x + box.w / 2 : alignment === "r" ? box.x + box.w - margin : box.x + margin;
    for (const visualLine of wrapText(ctx, (bullet ? "•  " : "") + value, Math.max(1, box.w - 2 * margin))) {
      ctx.fillText(visualLine, x, y, Math.max(1, box.w - margin));
      y += size * 1.16;
    }
    y += size * .10;
  }
  ctx.restore();
}

async function drawOffice(slide, path, archive, metadata) {
  const links = await archive.relationships(path);
  const layoutPath = Array.from(links.values()).find(entry => entry.type === "slideLayout")?.path;
  const layout = layoutPath ? await archive.doc(layoutPath) : null;
  const masterPath = layoutPath ? Array.from((await archive.relationships(layoutPath)).values()).find(entry => entry.type === "slideMaster")?.path : null;
  const master = masterPath ? await archive.doc(masterPath) : null;
  const palette = { ...metadata.palette };
  const canvas = artwork(metadata.width, metadata.height);
  const ctx = canvas.getContext("2d", { alpha: false });
  const frame = [slide, layout, master].map(doc => child(first(doc, "cSld"), "bg")).find(Boolean);
  ctx.fillStyle = hexColor(frame, palette, palette.bg1);
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  const shapes = first(slide, "spTree");
  const inherited = [layout, master].flatMap(doc => all(doc, "sp"));
  const scale = { x: canvas.width / metadata.cx, y: canvas.height / metadata.cy };
  let fallbackY = canvas.height * .10;
  const drawShapes = async (parent) => {
    for (const shape of Array.from(parent?.children || [])) {
      const type = localName(shape.tagName);
      if (type === "grpSp") {
        await drawShapes(shape);
        continue;
      }
      if (!["sp", "pic", "graphicFrame", "cxnSp"].includes(type)) continue;
      const key = placeholder(shape);
      const inheritedShape = key ? inherited.find(el => placeholder(el) === key && position(el, scale.x, scale.y)) : null;
      const box = position(shape, scale.x, scale.y) || position(inheritedShape, scale.x, scale.y) ||
        { x: canvas.width * .065, y: fallbackY, w: canvas.width * .87, h: canvas.height * (fallbackY < canvas.height * .18 ? .16 : .64) };
      if (!inheritedShape && !position(shape, scale.x, scale.y)) fallbackY += box.h;
      if (type === "pic") {
        const media = links.get(relationshipId(first(shape, "blip"), "embed"))?.path;
        if (media && /\.(png|jpe?g|webp|gif|bmp|avif)$/i.test(media)) {
          const blob = await archive.read(media, "blob");
          if (blob) {
            try { ctx.drawImage(await imageSource(blob), box.x, box.y, box.w, box.h); } catch {}
          }
        }
        continue;
      }
      const style = child(shape, "spPr");
      const fill = child(style, "solidFill");
      if (fill) {
        ctx.fillStyle = hexColor(fill, palette, "transparent");
        ctx.fillRect(box.x, box.y, box.w, box.h);
      }
      const rows = all(shape, "tr");
      if (rows.length) {
        const rowHeight = box.h / rows.length;
        rows.forEach((row, rowIndex) => {
          const cells = Array.from(row.children).filter(element => element.localName === "tc");
          cells.forEach((cell, cellIndex) => {
            const width = box.w / cells.length;
            const left = box.x + cellIndex * width;
            const top = box.y + rowIndex * rowHeight;
            ctx.fillStyle = rowIndex === 0 ? "#dce1fa" : rowIndex % 2 ? "#edf0f6" : "#ffffff";
            ctx.fillRect(left, top, width, rowHeight);
            ctx.strokeStyle = "#bcc2cc";
            ctx.strokeRect(left, top, width, rowHeight);
            drawText(ctx, cell, { x: left, y: top, w: width, h: rowHeight }, palette, metadata.scaleText * .8);
          });
        });
      } else drawText(ctx, shape, box, palette, metadata.scaleText, inheritedShape);
    }
  };
  await drawShapes(shapes);
  const notesPart = Array.from(links.values()).find(entry => entry.type === "notesSlide")?.path;
  const notes = notesPart ? await archive.doc(notesPart) : null;
  const text = all(shapes, "t").concat(all(notes, "t")).map(el => el.textContent).join(" ");
  return { blob: await imageBlob(canvas), text: summarize(text) };
}

async function pptxPages(file, progress) {
  const { default: JSZip } = await import("jszip");
  const zip = await JSZip.loadAsync(await file.arrayBuffer());
  const archive = new OfficeArchive(zip);
  const path = "ppt/presentation.xml";
  const manifest = await archive.doc(path);
  if (!manifest) throw new Error("Struktur presentasi PowerPoint tidak ditemukan.");
  const rels = await archive.relationships(path);
  const ordered = all(manifest, "sldId").map(el => rels.get(relationshipId(el, "id"))?.path).filter(Boolean);
  if (!ordered.length) throw new Error("PowerPoint ini tidak berisi slide.");
  const size = first(manifest, "sldSz");
  const cx = n(size, "cx", 12192000);
  const cy = n(size, "cy", 6858000);
  const ratio = Math.min(3, Math.max(1 / 3, cx / cy));
  const width = ratio >= 1 ? 1280 : Math.round(1120 * ratio);
  const height = ratio >= 1 ? Math.round(1280 / ratio) : 1120;
  const themePaths = Object.keys(zip.files).filter(part => /^ppt\/theme\/theme\d+\.xml$/.test(part));
  const theme = themePaths[0] ? await archive.doc(themePaths[0]) : null;
  const palette = { bg1: "#ffffff", tx1: "#182038", bg2: "#f2f3f6", tx2: "#263857" };
  for (const entry of Array.from(first(theme, "clrScheme")?.children || [])) {
    palette[entry.localName] = hexColor(entry, palette, palette.tx1);
  }
  if (palette.dk1) palette.tx1 = palette.dk1;
  if (palette.lt1) palette.bg1 = palette.lt1;
  if (palette.dk2) palette.tx2 = palette.dk2;
  if (palette.lt2) palette.bg2 = palette.lt2;
  const metadata = { width, height, cx, cy, palette, scaleText: width * 12700 / cx };
  const pages = [];
  for (const part of ordered.slice(0, maxPages)) {
    progress(pages.length + 1, Math.min(ordered.length, maxPages));
    const doc = await archive.doc(part);
    if (doc) pages.push(await drawOffice(doc, part, archive, metadata));
  }
  return { pages, actual: ordered.length, layout: "basic" };
}

async function binaryPages(file, progress) {
  const module = await import("cfb");
  const cfb = module.read ? module : module.default;
  const document = cfb.read(new Uint8Array(await file.arrayBuffer()), { type: "array" });
  const stream = cfb.find(document, "PowerPoint Document");
  if (!stream || stream.content.length < 8) throw new Error("Berkas PowerPoint lama ini tidak bisa dibaca.");
  const data = Uint8Array.from(stream.content);
  const view = new DataView(data.buffer);
  const texts = [];
  const utf16 = new TextDecoder("utf-16le");
  const ansi = new TextDecoder("windows-1252");
  const segments = (from, to, level, result) => {
    if (level > 16) return;
    for (let pos = from; pos + 8 <= to;) {
      const version = view.getUint16(pos, true) & 15;
      const type = view.getUint16(pos + 2, true);
      const end = pos + 8 + view.getUint32(pos + 4, true);
      if (end > to) break;
      const start = pos + 8;
      if (type === 1006 && !result) {
        const slide = [];
        segments(start, end, level + 1, slide);
        if (slide.length) texts.push(slide.join("\n"));
      } else if (type === 4000 || type === 4008) {
        const value = (type === 4000 ? utf16 : ansi).decode(data.subarray(start, Math.min(end, start + 18000))).replace(/\0/g, "").trim();
        if (value && result && !result.includes(value)) result.push(value);
      } else if (version === 15 && ![1016, 4080].includes(type)) segments(start, end, level + 1, result);
      pos = end;
    }
  };
  segments(0, data.length, 0, null);
  if (!texts.length) throw new Error("Tidak ada teks slide yang bisa diambil dari PPT. Coba pilih versi PDF atau PPTX.");
  const pages = [];
  for (const value of texts.slice(0, maxPages)) {
    progress(pages.length + 1, Math.min(texts.length, maxPages));
    const canvas = artwork();
    const g = canvas.getContext("2d");
    g.fillStyle = "#fbfaf7";
    g.fillRect(0, 0, canvas.width, canvas.height);
    g.fillStyle = "#3831b3";
    g.fillRect(65, 59, 76, 6);
    const [title, ...rest] = value.replace(/\r/g, "\n").split("\n").filter(line => line.trim());
    g.fillStyle = "#1b2130";
    g.font = "bold 42px Arial";
    let y = 133;
    for (const line of wrapText(g, title || file.name, 1120).slice(0, 3)) { g.fillText(line, 65, y); y += 51; }
    g.font = "27px Arial";
    if (rest.length) for (const line of wrapText(g, rest.join("\n"), 1120).slice(0, Math.floor((675 - y) / 37))) { g.fillText(line, 65, y + 10); y += 37; }
    pages.push({ blob: await imageBlob(canvas), text: summarize(value) });
  }
  return { pages, actual: texts.length, layout: "text" };
}

async function photoPage(file, progress, ocr) {
  progress(1, 1);
  const image = await imageSource(file);
  const ratio = Math.min(1, 1440 / Math.max(image.width, image.height));
  const canvas = artwork(Math.max(1, Math.round(image.width * ratio)), Math.max(1, Math.round(image.height * ratio)));
  const g = canvas.getContext("2d", { alpha: false });
  g.fillStyle = "#fff";
  g.fillRect(0, 0, canvas.width, canvas.height);
  g.drawImage(image, 0, 0, canvas.width, canvas.height);
  const blob = await imageBlob(canvas);
  return { pages: [{ blob, text: ocr ? await recognizeImage(blob) : "" }], actual: 1 };
}

export async function recognizeImage(blob) {
  const { createWorker } = await import("tesseract.js");
  const worker = await createWorker("ind+eng");
  try {
    const result = await worker.recognize(blob);
    return summarize(result.data.text);
  } finally {
    await worker.terminate();
  }
}

export async function importDeck(file, progress, language, ocr = false) {
  if (file.size > maxFile) throw new Error(language === "en" ? "Choose a file up to 35 MB." : "Ukuran berkas maksimal 35 MB.");
  const ext = file.name.toLowerCase().split(".").at(-1);
  const data = ext === "pdf" ? await pdfPages(file, progress) :
    ext === "pptx" ? await pptxPages(file, progress) :
    ext === "ppt" ? await binaryPages(file, progress) :
    ["jpg", "jpeg", "png", "webp", "gif", "bmp", "avif"].includes(ext) ? await photoPage(file, progress, ocr) : null;
  if (!data?.pages.length) throw new Error(language === "en" ? "Choose PPTX, PPT, PDF, or an image." : "Pilih berkas PPTX, PPT, PDF, atau gambar.");
  return { ...data, title: file.name.replace(/\.[^.]+$/, ""), format: ext };
}
