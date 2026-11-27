import * as T from "three";

const ink = "#f5f3ee", muted = "#aeb0c1", soft = "#b6abff";
const escapeCut = (text, size = 360) => String(text || "").trim().slice(0, size);

function panel(g, x, y, w, h, radius, fill, stroke) {
  g.beginPath();
  g.roundRect(x, y, w, h, radius);
  g.fillStyle = fill;
  g.fill();
  if (stroke) { g.strokeStyle = stroke; g.lineWidth = 2; g.stroke(); }
}

function lines(g, value, width, max) {
  const result = [];
  let line = "";
  for (const word of value.split(/\s+/)) {
    const joined = line ? line + " " + word : word;
    if (line && g.measureText(joined).width > width) { result.push(line); line = word; }
    else line = joined;
  }
  if (line) result.push(line);
  return result.length > max ? [...result.slice(0, max - 1), result[max - 1].slice(0, -1) + "…"] : result;
}

function canvas(w, h) {
  const node = document.createElement("canvas");
  node.width = w; node.height = h;
  const texture = new T.CanvasTexture(node);
  texture.colorSpace = T.SRGBColorSpace;
  const g = node.getContext("2d");
  return { node, texture, g };
}

export class HeadsetPanel {
  constructor(scene, input) {
    const { node, texture, g } = canvas(1024, 608);
    const material = new T.MeshBasicMaterial({ map: texture, transparent: true, depthTest: false, depthWrite: false, toneMapped: false, side: T.DoubleSide });
    this.mesh = new T.Mesh(new T.PlaneGeometry(1.26, .748), material);
    this.mesh.renderOrder = 9;
    this.mesh.visible = false;
    scene.add(this.mesh);
    this.width = node.width; this.height = node.height;
    this.g = g; this.texture = texture; this.input = input;
    this.keys = []; this.keyboard = false; this.upper = false; this.alternate = false; this.hover = null;
  }

  place(camera) {
    const forward = camera.getWorldDirection(new T.Vector3());
    forward.y = 0;
    if (forward.lengthSq() < .05) forward.set(0, 0, 1);
    forward.normalize();
    const origin = camera.getWorldPosition(new T.Vector3());
    this.mesh.position.copy(origin).addScaledVector(forward, 1.65);
    this.mesh.position.y -= .46;
    this.mesh.lookAt(origin);
    this.mesh.updateMatrixWorld(true);
  }

  setView(view) {
    if (!view.canType) this.keyboard = false;
    this.view = view;
    if (this.mesh.visible) this.draw();
  }

  redraw() { if (this.mesh.visible && this.view) this.draw(); }

  button(x, y, w, h, label, action, accent = false) {
    const hover = action === this.hover;
    const fill = accent ? (hover ? "#d1c7ff" : "#b6abff") : hover ? "#3b3853" : "#292a36";
    panel(this.g, x, y, w, h, 14, fill, accent ? null : "#45465a");
    this.g.fillStyle = accent ? "#16132a" : ink;
    this.g.font = "700 " + (h <= 60 ? 24 : 26) + "px Inter, Arial";
    this.g.textAlign = "center";
    this.g.textBaseline = "middle";
    this.g.fillText(label, x + w / 2, y + h / 2 + 1, w - 14);
    this.keys.push({ x, y, w, h, action });
  }

  draw() {
    const v = this.view, g = this.g;
    if (!v) return;
    const t = v.labels;
    this.keys = [];
    g.clearRect(0, 0, this.width, this.height);
    panel(g, 5, 5, 1014, 598, 33, "#14151e", "#57516e");
    g.textAlign = "left";
    g.textBaseline = "alphabetic";
    g.fillStyle = soft;
    g.font = "bold 27px Inter, Arial";
    g.fillText("NARA  /  " + v.room.toUpperCase(), 38, 52, 602);
    g.fillStyle = ink;
    g.textAlign = "right";
    g.fillText(v.clock + "   ·   " + v.slide, 986, 52);
    g.textAlign = "left";
    g.font = "bold 41px Inter, Arial";
    g.fillText(escapeCut(v.title, 65), 38, 118, 946);
    g.font = "30px Inter, Arial";
    g.fillStyle = muted;
    if (this.keyboard) {
      g.fillText(t.type + "  " + escapeCut(v.draft, 110).slice(-49), 38, 176, 946);
      const rows = this.alternate ? ["1234567890", "!@#$%&*()-", ".,?/:+='\u0022"] : ["qwertyuiop", "asdfghjkl", "zxcvbnm,.?"];
      rows.forEach((row, y) => row.split("").forEach((value, x) => {
        const key = this.upper ? value.toUpperCase() : value;
        this.button(38 + x * 95, 211 + y * 70, 86, 60, key, "character:" + key);
      }));
      const buttons = [[t.caps, "caps", 100], [this.alternate ? "ABC" : "123", "symbols", 100], [t.space, "character: ", 280],
        ["⌫", "delete", 100], [t.close, "keyboard", 141], [t.send, "send", 150]];
      let x = 38;
      buttons.forEach(([label, action, width]) => { this.button(x, 424, width, 62, label, action, action === "send"); x += width + 13; });
      this.button(38, 507, 216, 67, t.exit, "exit");
      this.texture.needsUpdate = true;
      return;
    }
    const detail = escapeCut(v.caption || v.detail, 420);
    lines(g, detail, 944, 5).forEach((line, i) => g.fillText(line, 38, 172 + i * 38));
    if (v.help) {
      g.fillStyle = soft;
      g.font = "23px Inter, Arial";
      lines(g, escapeCut(v.help, 130), 535, 2).forEach((line, i) => g.fillText(line, 38, 366 + i * 25));
    }
    this.button(599, 348, 180, 48, v.audio ? t.mute : t.unmute, "audio-toggle");
    this.button(796, 348, 190, 48, t.recenter, "recenter");
    this.button(38, 408, 210, 73, t.back, "prev");
    this.button(264, 408, 210, 73, t.next, "next");
    if (v.canType || v.micActive) this.button(490, 408, 238, 73, v.micLabel, "mic");
    else if (v.canReplay) this.button(490, 408, 238, 73, t.replay, "replay");
    if (v.canType) this.button(744, 408, 242, 73, t.type, "keyboard");
    else if (v.canSkip) this.button(744, 408, 242, 73, t.skip, "skip");
    this.button(38, 500, 210, 72, t.exit, "exit");
    if (v.canPause) this.button(264, 500, 210, 72, v.pauseLabel, "pause");
    else if (v.canType && v.canReplay) this.button(264, 500, 210, 72, t.replay, "replay");
    if (v.canSkip && v.canType) this.button(490, 500, 175, 72, t.skip, "skip");
    if (v.primary && !v.busy) this.button(682, 500, 304, 72, v.primary, "primary", true);
    this.texture.needsUpdate = true;
  }

  hit(uv) {
    if (!uv) return null;
    const x = uv.x * this.width, y = (1 - uv.y) * this.height;
    return this.keys.find(box => x >= box.x && x <= box.x + box.w && y >= box.y && y <= box.y + box.h)?.action || null;
  }

  highlight(action) {
    if (this.hover !== action) { this.hover = action; this.redraw(); }
  }

  press(action) {
    if (!action) return;
    if (action === "keyboard") this.keyboard = !this.keyboard;
    else if (action === "caps") this.upper = !this.upper;
    else if (action === "symbols") this.alternate = !this.alternate;
    else {
      if (action === "send") this.keyboard = false;
      this.input(action);
    }
    this.redraw();
    this.texture.needsUpdate = true;
  }
}

export function dialogueTexture(scene) {
  const { g, node, texture } = canvas(768, 256);
  const material = new T.MeshBasicMaterial({ map: texture, transparent: true, depthWrite: false, depthTest: false,
    side: T.DoubleSide, toneMapped: false });
  const mesh = new T.Mesh(new T.PlaneGeometry(1.33, .443), material);
  mesh.renderOrder = 8;
  mesh.visible = false;
  scene.add(mesh);
  let key;
  return {
    mesh,
    set(name, text) {
      const value = name + "\n" + text;
      if (value === key) return;
      key = value;
      g.clearRect(0, 0, node.width, node.height);
      panel(g, 5, 5, 758, 227, 28, "#1b1d2dee", "#aca0fb");
      g.fillStyle = soft;
      g.font = "bold 34px Inter, Arial";
      g.fillText(name, 28, 58, 705);
      g.fillStyle = ink;
      g.font = "29px Inter, Arial";
      lines(g, escapeCut(text, 290), 710, 4).forEach((line, i) => g.fillText(line, 28, 102 + i * 33));
      texture.needsUpdate = true;
    }
  };
}
