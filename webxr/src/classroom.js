import * as T from "three";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
import { Audience, loadMotions } from "./audience.js";
import { HeadsetPanel, dialogueTexture } from "./hud.js";

const v = (value) => new T.Vector3(...value);
const fetchJSON = async (path) => {
  const response = await fetch(path);
  if (!response.ok) throw new Error("Missing room file");
  return response.json();
};

function slideQuad(center, normal, width, ratio, material) {
  const screen = new T.Mesh(new T.PlaneGeometry(width, width / ratio), material);
  screen.position.copy(center).addScaledVector(normal, .007);
  screen.quaternion.setFromUnitVectors(new T.Vector3(0, 0, 1), normal);
  return screen;
}

function canvasForSlide() {
  const image = document.createElement("canvas");
  image.width = 1280; image.height = 720;
  return image;
}

function imageFrom(page) {
  const image = new Image();
  const transient = page.blob && URL.createObjectURL(page.blob);
  image.src = transient || page.src;
  return image.decode().then(() => image).finally(() => { if (transient) URL.revokeObjectURL(transient); });
}

export class Classroom {
  constructor(mount, desktopBubble, runAction) {
    this.mount = mount;
    this.desktopBubble = desktopBubble;
    this.runAction = runAction;
    this.assets = new Map();
    this.metadata = new Map();
    this.loader = new GLTFLoader();
    this.scene = new T.Scene();
    this.scene.background = new T.Color("#202331");
    this.viewer = new T.PerspectiveCamera(59, 1, .05, 31);
    this.viewer.rotation.order = "YXZ";
    this.rig = new T.Group();
    this.rig.add(this.viewer);
    this.scene.add(this.rig);
    this.renderer = new T.WebGLRenderer({ antialias: true, powerPreference: "high-performance", alpha: false });
    const gl = this.renderer.getContext();
    const debug = gl.getExtension("WEBGL_debug_renderer_info");
    const rendererName = debug ? gl.getParameter(debug.UNMASKED_RENDERER_WEBGL) : "";
    this.software = /swiftshader|llvmpipe|software rasterizer/i.test(rendererName);
    this.renderer.xr.enabled = true;
    this.renderer.xr.setReferenceSpaceType("local-floor");
    this.renderer.xr.setFramebufferScaleFactor(this.software ? .62 : .90);
    this.renderer.outputColorSpace = T.SRGBColorSpace;
    this.renderer.toneMapping = T.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.17;
    this.renderer.domElement.setAttribute("aria-label", "Pratinjau ruang presentasi 3D");
    mount.prepend(this.renderer.domElement);
    this.room = new T.Group();
    this.crowd = new T.Group();
    this.scene.add(this.room, this.crowd);
    this.scene.add(new T.HemisphereLight(0xdde8ff, 0xb39881, 1.91));
    const main = new T.DirectionalLight(0xffefd8, 2.30);
    main.position.set(-4, 7, -3);
    const fill = new T.DirectionalLight(0xc3ddff, .82);
    fill.position.set(3, 6, 3);
    this.scene.add(main, fill);
    this.slideCanvas = canvasForSlide();
    this.slideTexture = new T.CanvasTexture(this.slideCanvas);
    this.slideTexture.colorSpace = T.SRGBColorSpace;
    this.slideMaterial = new T.MeshBasicMaterial({ map: this.slideTexture, color: 0xecebeb, toneMapped: false, side: T.DoubleSide });
    this.hud = new HeadsetPanel(this.scene, action => this.perform(action, true));
    this.dialogue = dialogueTexture(this.scene);
    this.raycaster = new T.Raycaster();
    this.controllers = [this.setupController(0), this.setupController(1)];
    this.startTime = performance.now();
    this.state = { speaking: null, raised: null, speakingSince: 0 };
    this.xrButtons = new WeakMap();
    this.rendered = 0;
    this.loadId = 0;
    this.resizeObserver = new ResizeObserver(() => this.resize());
    this.resizeObserver.observe(mount);
    this.listenDesktop();
    this.renderer.xr.addEventListener("sessionstart", () => {
      this.positionPose("presenter", true);
      this.hud.mesh.visible = true;
      this.placePending = true;
      this.renderer.xr.setFoveation(.76);
      this.runAction("xrstart");
    });
    this.renderer.xr.addEventListener("sessionend", () => {
      this.hud.mesh.visible = false;
      this.placePending = false;
      this.positionPose(this.perspective || "presenter");
      this.runAction("xrend");
    });
    this.resize();
    this.renderer.setAnimationLoop((raw, frame) => this.animate(raw, frame));
  }

  model(path) {
    if (!this.assets.has(path)) this.assets.set(path, this.loader.loadAsync(path).then(file => file.scene).catch(error => {
      this.assets.delete(path); throw error;
    }));
    return this.assets.get(path);
  }

  async load(name, profiles, progress) {
    const id = ++this.loadId;
    this.loading = true;
    this.crowd.clear();
    this.audience = undefined;
    this.motions ??= loadMotions().catch(() => null);
    const roomPath = "/media/rooms/" + name;
    if (!this.metadata.has(name)) this.metadata.set(name, fetchJSON(roomPath + ".json").catch(error => { this.metadata.delete(name); throw error; }));
    const [roomModel, data, motions] = await Promise.all([this.model(roomPath + ".glb"), this.metadata.get(name), this.motions]);
    if (id !== this.loadId) return false;
    this.room.clear();
    this.room.add(roomModel);
    this.data = data;
    this.roomKey = name;
    const marks = data.markers;
    const seatNames = Object.keys(marks).filter(key => key.startsWith("NaraAudienceSeat_")).sort();
    const seats = seatNames.map(key => marks[key]);
    const screenAt = v(marks.NaraScreen);
    const screenWidth = 2 * screenAt.distanceTo(v(marks.NaraScreenEdge));
    const screenDir = new T.Vector3(0, 0, Math.sign(seats[0][2] - screenAt.z));
    this.room.add(slideQuad(screenAt, screenDir, screenWidth, 16 / 9, this.slideMaterial));
    if (marks.NaraConfidenceScreen) {
      const center = v(marks.NaraConfidenceScreen);
      this.room.add(slideQuad(center, v(marks.NaraConfidenceNormal).sub(center).normalize(), .422, 16 / 9, this.slideMaterial));
    }
    this.audience = new Audience(this.crowd, motions, name === "university");
    this.positionPose(this.perspective || "presenter", this.renderer.xr.isPresenting);
    let loaded = 0;
    let cursor = 0;
    const failed = [];
    const consume = async () => {
      while (cursor < seats.length && id === this.loadId) {
        const index = cursor++;
        const profile = profiles[index];
        if (!profile) continue;
        try {
          const avatar = await this.model("/media/avatars/" + profile.id + ".glb");
          if (id === this.loadId) this.audience.add(avatar, seats[index], profile, index);
        } catch (error) { failed.push(profile.id); console.error(error); }
        if (id === this.loadId) progress(++loaded, seats.length);
      }
    };
    await Promise.all(Array.from({ length: 5 }, consume));
    if (id !== this.loadId) return false;
    this.loading = false;
    return { failed, seatedCount: loaded - failed.length };
  }

  positionPose(view, xr = false) {
    if (!xr) this.perspective = view;
    if (!this.data) return;
    const { NaraPresenter: station, NaraSightline: focus } = this.data.markers;
    const eye = v(station);
    const look = v(focus).sub(eye);
    const yaw = Math.atan2(-look.x, -look.z);
    if (xr) {
      this.rig.position.set(eye.x, eye.y - 1.67, eye.z);
      this.rig.rotation.set(0, yaw, 0);
      this.viewer.position.set(0, 0, 0);
      this.viewer.rotation.set(0, 0, 0, "YXZ");
      this.rig.updateMatrixWorld(true);
      return;
    }
    this.rig.position.set(0, 0, 0);
    this.rig.rotation.set(0, 0, 0);
    const source = this.data.cameras["Sudut ruangan"] || this.data.cameras["IF101 dari belakang"];
    let position = eye;
    let target = v(focus);
    if (view === "overview" && source) {
      position = v(source.position);
      target = position.clone().add(v(source.direction));
    } else if (view === "screen") {
      const screen = v(this.data.markers.NaraScreen);
      const width = 2 * screen.distanceTo(v(this.data.markers.NaraScreenEdge));
      const direction = Math.sign(v(focus).z - screen.z) || 1;
      position = new T.Vector3(eye.x, 1.78, screen.z + direction * Math.max(2.55, width * 1.12));
      target = screen;
    }
    this.viewer.position.copy(position);
    this.viewer.lookAt(target);
    this.basePitch = this.viewer.rotation.x;
    this.baseYaw = this.viewer.rotation.y;
    this.panX = this.panY = 0;
    this.viewer.updateMatrixWorld(true);
  }

  listenDesktop() {
    const node = this.renderer.domElement;
    let pointer;
    node.addEventListener("pointerdown", (e) => {
      if (e.button !== 0) return;
      pointer = { id: e.pointerId, x: e.clientX, y: e.clientY };
      node.setPointerCapture(e.pointerId);
      node.classList.add("dragging");
    });
    node.addEventListener("pointermove", (e) => {
      if (pointer?.id !== e.pointerId || this.renderer.xr.isPresenting || this.baseYaw === undefined) return;
      this.panX = T.MathUtils.clamp(this.panX + (e.clientX - pointer.x) * .004, -Math.PI, Math.PI);
      this.panY = T.MathUtils.clamp(this.panY + (e.clientY - pointer.y) * .003, -.83, .69);
      pointer.x = e.clientX; pointer.y = e.clientY;
      this.viewer.rotation.set(this.basePitch + this.panY, this.baseYaw + this.panX, 0, "YXZ");
    });
    const release = () => { pointer = null; node.classList.remove("dragging"); };
    node.addEventListener("pointerup", release);
    node.addEventListener("lostpointercapture", release);
  }

  setQuality(value) {
    this.quality = value;
    this.resize();
  }

  resize() {
    const { clientWidth: width, clientHeight: height } = this.mount;
    if (!width || !height || this.renderer.xr.isPresenting) return;
    this.viewer.aspect = width / height;
    this.viewer.updateProjectionMatrix();
    const limit = this.software ? .85 : this.quality === "high" ? 1.8 : this.quality === "eco" ? 1 : 1.35;
    this.renderer.setPixelRatio(Math.min(devicePixelRatio, limit));
    this.renderer.setSize(width, height);
  }

  setSlide(page) {
    this.slide = page;
    const ctx = this.slideCanvas.getContext("2d");
    if (!page) return;
    imageFrom(page).then(image => {
      if (page !== this.slide) return;
      ctx.fillStyle = "#f7f8fc";
      ctx.fillRect(0, 0, 1280, 720);
      const scale = Math.min(1280 / image.width, 720 / image.height);
      const width = image.width * scale, height = image.height * scale;
      ctx.drawImage(image, (1280 - width) / 2, (720 - height) / 2, width, height);
      this.slideTexture.needsUpdate = true;
    }).catch(() => {});
  }

  setDialogue(state, view) {
    if (state.speaking !== this.state.speaking || this.state.caption !== state.caption) this.state.speakingSince = (performance.now() - this.startTime) / 1000;
    this.state = { ...this.state, ...state };
    this.hud.setView(view);
  }

  setupController(index) {
    const controller = this.renderer.xr.getController(index);
    this.rig.add(controller);
    const lineMaterial = new T.LineBasicMaterial({ color: 0xb7a6ff, transparent: true, opacity: .85, depthTest: false });
    const line = new T.Line(new T.BufferGeometry().setFromPoints([new T.Vector3(), new T.Vector3(0, 0, -1)]), lineMaterial);
    line.renderOrder = 11;
    line.scale.z = 2.2;
    line.visible = false;
    controller.add(line);
    controller.addEventListener("connected", () => { line.visible = true; });
    controller.addEventListener("disconnected", () => { line.visible = false; });
    controller.addEventListener("select", () => {
      const hit = this.point(controller);
      if (hit?.action) this.hud.press(hit.action);
    });
    controller.addEventListener("squeeze", () => this.perform("mic", true));
    return { controller, line };
  }

  perform(action, xr) {
    if (action === "exit" && xr) return this.renderer.xr.getSession()?.end();
    if (action === "recenter" && xr) { this.placePending = true; return; }
    this.runAction(action);
  }

  point(controller) {
    if (!this.hud.mesh.visible || !controller.visible) return null;
    const rotation = new T.Matrix4().extractRotation(controller.matrixWorld);
    this.raycaster.ray.origin.setFromMatrixPosition(controller.matrixWorld);
    this.raycaster.ray.direction.set(0, 0, -1).applyMatrix4(rotation);
    const hit = this.raycaster.intersectObject(this.hud.mesh, false)[0];
    return hit ? { action: this.hud.hit(hit.uv), distance: hit.distance } : null;
  }

  async enter() {
    if (!this.data) throw new Error("room");
    const session = await navigator.xr.requestSession("immersive-vr", { requiredFeatures: ["local-floor"] });
    try { await this.renderer.xr.setSession(session); }
    catch (error) { await session.end().catch(() => {}); throw error; }
  }

  panFor(index) {
    const seat = this.audience?.people[index]?.seat;
    return seat ? T.MathUtils.clamp((this.viewer.getWorldPosition(new T.Vector3()).x - seat[0]) / 4.9, -.79, .79) : 0;
  }

  controllerTick() {
    const s = this.renderer.xr.getSession();
    for (const source of s.inputSources) {
      const buttons = source.gamepad?.buttons;
      if (!buttons) continue;
      const was = this.xrButtons.get(source) || [false, false, false];
      const is = [buttons[4]?.pressed, buttons[5]?.pressed, buttons[3]?.pressed];
      if (is[0] && !was[0]) this.perform(source.handedness === "left" ? "prev" : "primary", true);
      if (is[1] && !was[1]) this.perform("next", true);
      if (is[2] && !was[2]) this.placePending = true;
      this.xrButtons.set(source, is);
    }
    let hover = null;
    for (const { controller, line } of this.controllers) {
      const hit = this.point(controller);
      if (hit?.action) hover = hit.action;
      line.scale.z = hit?.distance || 2.2;
      line.material.opacity = hit?.action ? 1 : .48;
    }
    this.hud.highlight(hover);
  }

  updateDialogue(xr) {
    const bubble = this.desktopBubble;
    const index = this.state.speaker ?? this.state.raised;
    const person = index === null ? null : this.audience?.people[index];
    if (!person || !this.state.caption) {
      this.dialogue.mesh.visible = false;
      bubble.hidden = true;
      return;
    }
    const target = person.head.getWorldPosition(new T.Vector3()).add(new T.Vector3(0, .30, -.14));
    if (xr) {
      const camera = this.renderer.xr.getCamera();
      this.dialogue.set(person.profile.label, this.state.caption);
      this.dialogue.mesh.visible = true;
      this.dialogue.mesh.position.copy(target);
      this.dialogue.mesh.lookAt(camera.getWorldPosition(new T.Vector3()));
      bubble.hidden = true;
      return;
    }
    this.dialogue.mesh.visible = false;
    const projected = target.project(this.viewer);
    const inside = Math.abs(projected.x) < 1.17 && Math.abs(projected.y) < 1.2 && projected.z < 1 && projected.z > -1;
    bubble.hidden = !inside;
    if (!inside) return;
    if (bubble.dataset.caption !== this.state.caption || bubble.dataset.speaker !== person.profile.label) {
      bubble.querySelector("strong").textContent = person.profile.label;
      bubble.querySelector("span").textContent = this.state.caption;
      bubble.dataset.caption = this.state.caption;
      bubble.dataset.speaker = person.profile.label;
    }
    const half = bubble.offsetWidth / 2 + 10;
    const horizontal = T.MathUtils.clamp((projected.x * .5 + .5) * this.mount.clientWidth, half, this.mount.clientWidth - half);
    const vertical = T.MathUtils.clamp((.5 - projected.y * .5) * this.mount.clientHeight, bubble.offsetHeight + 62, this.mount.clientHeight - 64);
    bubble.style.transform = "translate(" + horizontal + "px," + vertical + "px) translate(-50%,-100%)";
  }

  animate(raw, frame) {
    const xr = Boolean(frame && this.renderer.xr.isPresenting);
    const time = (raw - this.startTime) / 1000;
    const frameMs = this.loading ? 105 : this.software ? 70 : this.quality === "eco" ? 42 : 28;
    if (!xr && (raw - this.rendered < frameMs || document.hidden || !this.data)) return;
    if (xr && this.software && raw - this.rendered < 72) {
      this.controllerTick();
      return;
    }
    this.rendered = raw;
    if (!this.loading) this.audience?.update(time, this.state, xr ? .046 : .048);
    this.updateDialogue(xr);
    this.renderer.render(this.scene, this.viewer);
    if (xr) {
      if (this.placePending) {
        this.hud.place(this.renderer.xr.getCamera());
        this.placePending = false;
        this.hud.redraw();
      }
      this.controllerTick();
    }
  }
}
