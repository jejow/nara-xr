import * as T from "three";
import { clone } from "three/addons/utils/SkeletonUtils.js";

const vector = () => new T.Vector3();
const up = new T.Vector3(0, 1, 0);
const front = new T.Vector3(0, 0, -1);
const radians = Math.PI / 180;
const finite = (value, low, high) => T.MathUtils.clamp(Number.isFinite(value) ? value : 0, low, high);
const normalized = name => name.toLowerCase().replace(/[^a-z0-9]/g, "");
const position = (object) => object.getWorldPosition(vector());
const orientation = (object) => object.getWorldQuaternion(new T.Quaternion());
const difference = (a, b) => position(a).sub(position(b));

function applyWorldRotation(joint, rotation) {
  const inverse = orientation(joint.parent).invert();
  joint.quaternion.copy(inverse.multiply(rotation));
  joint.updateWorldMatrix(false, true);
}

function aim(joint, end, direction) {
  const current = difference(end, joint).normalize();
  if (current.lengthSq() < .01 || direction.lengthSq() < .00001) return;
  applyWorldRotation(joint, new T.Quaternion().setFromUnitVectors(current, direction.clone().normalize()).multiply(orientation(joint)));
}

function reach(arm, target, pole) {
  const shoulder = position(arm.upper);
  const line = target.clone().sub(shoulder);
  const reach = finite(line.length(), Math.abs(arm.length1 - arm.length2) + .003, arm.length1 + arm.length2 - .006);
  if (line.lengthSq() < .00001) return;
  line.normalize();
  const along = (arm.length1 ** 2 - arm.length2 ** 2 + reach ** 2) / (2 * reach);
  const height = Math.sqrt(Math.max(0, arm.length1 ** 2 - along ** 2));
  const bend = pole.clone().addScaledVector(line, -pole.dot(line)).normalize();
  if (bend.lengthSq() < .00001) bend.set(arm.sign, -.15, .07).normalize();
  aim(arm.upper, arm.elbow, line.clone().multiplyScalar(along).addScaledVector(bend, height));
  aim(arm.elbow, arm.hand, shoulder.addScaledVector(line, reach).sub(position(arm.elbow)));
}

function palm(arm, finger, across) {
  if (!arm.digits.every(Boolean)) return;
  const [index, middle, little] = arm.digits;
  const direction = difference(middle, arm.hand);
  const normal = direction.clone().cross(difference(index, little));
  const wantedNormal = finger.clone().cross(across);
  if (normal.lengthSq() < .000001 || wantedNormal.lengthSq() < .000001) return;
  const from = new T.Quaternion().setFromRotationMatrix(new T.Matrix4().lookAt(direction.normalize(), vector(), normal.normalize()));
  const to = new T.Quaternion().setFromRotationMatrix(new T.Matrix4().lookAt(finger.clone().normalize(), vector(), wantedNormal.normalize()));
  applyWorldRotation(arm.hand, to.multiply(from.invert()).multiply(orientation(arm.hand)));
}

function armRig(bone, prefix, pelvis) {
  const upper = bone(prefix + " UpperArm"), elbow = bone(prefix + " Forearm"), hand = bone(prefix + " Hand");
  return {
    upper, elbow, hand, length1: difference(elbow, upper).length(), length2: difference(hand, elbow).length(),
    sign: Math.sign(difference(upper, pelvis).x),
    digits: [1, 2, 4].map(index => bone(prefix + " Finger" + index, false))
  };
}

function fitPerson(original, seat, profile, index) {
  const model = clone(original);
  const bones = new Map();
  const shapes = [];
  model.traverse(node => {
    if (node.isBone) bones.set(normalized(node.name), node);
    if (node.isSkinnedMesh) {
      node.frustumCulled = false;
      const morphs = new Map(Object.entries(node.morphTargetDictionary || {}).map(([key, value]) => [normalized(key), value]));
      if (morphs.size) shapes.push({ weights: node.morphTargetInfluences, morphs });
    }
  });
  const bone = (name, required = true) => {
    const value = bones.get(normalized("Bip01 " + name));
    if (required && !value) throw new Error("Rig " + profile.id + " missing " + name);
    return value;
  };
  model.updateWorldMatrix(true, true);
  const pelvis = bone("Pelvis"), head = bone("Head"), chest = bone("Spine2");
  const toes = [bone("L Toe0"), bone("R Toe0")];
  const feet = [bone("L Foot"), bone("R Foot")];
  const feetHeight = () => Math.min(...toes.map(toe => position(toe).y));
  const height = position(head).y - feetHeight();
  if (height >= .8 && height <= 2.3) model.scale.multiplyScalar(finite(height, 1.43, 1.58) / height);
  const facing = difference(toes[0], feet[0]).add(difference(toes[1], feet[1]));
  model.rotation.y += Math.PI - Math.atan2(facing.x, facing.z);
  model.updateWorldMatrix(true, true);
  const posture = profile.portrait * .61803399 % 1 - .5;
  const stagger = profile.portrait * .287 % 1 - .5;
  const arms = ["R", "L"].map(prefix => {
    const leg = bone(prefix + " Thigh"), knee = bone(prefix + " Calf"), foot = bone(prefix + " Foot"), toe = bone(prefix + " Toe0");
    const side = Math.sign(difference(leg, pelvis).x);
    aim(leg, knee, new T.Vector3((.12 + .11 * stagger) * side, -.105, -1));
    aim(knee, foot, new T.Vector3(.04 * side, -1, -.15 + .2 * stagger * side));
    aim(foot, toe, new T.Vector3(.12 * side, -.48, -1));
    const arm = armRig(bone, prefix, pelvis);
    const target = position(leg).lerp(position(knee), .55 + .24 * posture * side + .13 * stagger);
    target.add(new T.Vector3(side * .014, .077, .01));
    reach(arm, target, new T.Vector3(side * .65, -.86, .20));
    palm(arm, new T.Vector3(-side * .12, -.09, -1), new T.Vector3(-side, 0, 0));
    arm.base = [arm.upper, arm.elbow, arm.hand].map(joint => joint.quaternion.clone());
    arm.offset = difference(arm.hand, arm.upper);
    arm.elbowOffset = difference(arm.elbow, arm.upper);
    arm.last = null;
    return arm;
  });
  const seatAnchor = new T.Vector3(.0, .551, .071).sub(position(pelvis));
  model.position.add(seatAnchor);
  model.updateWorldMatrix(true, true);
  model.position.y += finite(.050 - feetHeight(), -.027, .041);
  model.position.add(new T.Vector3(...seat));
  model.updateWorldMatrix(true, true);
  return { model, head, chest, shapes, arms, headRest: head.quaternion.clone(), chestRest: chest.quaternion.clone(),
    seat, profile, index, seed: 1.3 + index * 2.439 + profile.portrait * .57, nextBlink: 1 + profile.portrait * .23, blinkAt: -2,
    raiseBlend: 0, raised: false, changed: 0, speakBlend: 0, posture };
}

function unpack(buffer, expected, hasHead) {
  const data = new DataView(buffer);
  const count = data.getUint32(4, true);
  if (data.getUint32(0, true) !== expected || count > 24) throw new Error("Invalid NARA motion archive");
  let byte = 8;
  const width = hasHead ? 30 : 27;
  return Array.from({ length: count }, () => {
    const size = data.getInt32(byte, true);
    const seconds = data.getFloat32(byte + 4, true);
    const hold = hasHead ? data.getFloat32(byte + 8, true) : 0;
    const lower = hasHead ? data.getFloat32(byte + 12, true) : 0;
    byte += hasHead ? 16 : 8;
    if (size < 2 || size > 16000 || !Number.isFinite(seconds) || seconds <= 0 || byte + size * width * 4 > buffer.byteLength) {
      throw new Error("Invalid NARA motion track");
    }
    const values = new Float32Array(buffer, byte, size * width);
    byte += size * width * 4;
    return { values, width, size, seconds, hold, lower };
  });
}

const motionSource = async (name, type, head) => {
  const result = await fetch("/media/" + name + ".bytes");
  if (!result.ok) throw new Error("Motion file unavailable");
  return unpack(await result.arrayBuffer(), type, head);
};

export async function loadMotions() {
  const [seated, gestures, generated] = await Promise.all([
    motionSource("SeatedMotion", 0x314d534e, false), motionSource("OverteMotion", 0x31564f4e, true),
    motionSource("LocalAudienceRaises", 0x3159484e, true).catch(() => [])
  ]);
  return { seated, gestures, generated };
}

function sample(track, time) {
  const frame = finite(time / track.seconds, 0, 1) * (track.size - 1);
  const floor = Math.min(track.size - 2, Math.floor(frame));
  const a = floor * track.width;
  return Float32Array.from({ length: track.width }, (_, i) =>
    T.MathUtils.lerp(track.values[a + i], track.values[a + track.width + i], frame - floor));
}

function expressiveRotation(joint, base, pitch, yaw, roll) {
  joint.quaternion.copy(base);
  const world = orientation(joint);
  const rotation = new T.Quaternion().setFromEuler(new T.Euler(pitch, yaw, roll, "YXZ"));
  applyWorldRotation(joint, rotation.multiply(world));
}

function poseArm(person, arm, pose, index, strength, delta, desk, lifted) {
  for (let i = 0; i < 3; i++) [arm.upper, arm.elbow, arm.hand][i].quaternion.copy(arm.base[i]);
  const origin = position(arm.upper);
  const length = arm.length1 + arm.length2;
  const next = arm.offset.clone();
  const elbow = arm.elbowOffset.clone();
  const dir = new T.Vector3(-arm.sign * .12, -.08, -1);
  const across = new T.Vector3(-arm.sign, 0, 0);
  if (pose) {
    const slot = 3 + index * 12;
    const v = (start) => new T.Vector3(pose[start] * arm.sign, pose[start + 1], -pose[start + 2]);
    const positionGoal = v(slot).multiplyScalar(length);
    const elbowGoal = v(slot + 3).multiplyScalar(length);
    if (desk && !lifted) {
      positionGoal.z = Math.max(positionGoal.z, -.30);
      positionGoal.y = Math.max(positionGoal.y, .800 - origin.y);
    }
    const weight = strength * (lifted ? 1 : .8);
    next.lerp(positionGoal, weight);
    elbow.lerp(elbowGoal, weight);
    if (v(slot + 6).lengthSq() > .02) dir.lerp(v(slot + 6).normalize(), weight);
    if (v(slot + 9).lengthSq() > .02) across.lerp(v(slot + 9).normalize(), .55 * weight);
  }
  // Keep the wrist outside the thorax when close to the torso, clear the tabletop, and damp frame-to-frame travel.
  const targetX = origin.x + next.x;
  if (next.z > -.17) {
    const boundary = person.seat[0] + .146 * arm.sign;
    next.x += arm.sign * Math.max(0, (boundary - targetX) * arm.sign);
  }
  if (desk && next.z < -.15) next.y = Math.max(next.y, .801 - origin.y);
  const alpha = 1 - Math.exp(-(lifted ? 11 : 7) * delta);
  if (arm.last) arm.last.lerp(next, alpha);
  else arm.last = next.clone();
  reach(arm, origin.add(arm.last), elbow);
  palm(arm, dir, across);
}

export function shuffle(list) {
  const values = list.slice();
  for (let i = values.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [values[i], values[j]] = [values[j], values[i]];
  }
  return values;
}

export class Audience {
  constructor(group, motions, isDesk) {
    this.group = group;
    this.motions = motions;
    this.desk = isDesk;
    this.people = [];
    this.last = 0;
  }

  add(model, seat, profile, index) {
    const person = fitPerson(model, seat, profile, index);
    this.group.add(person.model);
    this.people[index] = person;
    return person;
  }

  update(time, state, interval) {
    const delta = Math.min(.12, time - this.last);
    if (delta < interval || !this.motions) return;
    this.last = time;
    for (const person of this.people) {
      if (!person) continue;
      const { index, seed } = person;
      const talking = state.speaking === index;
      const raised = state.raised === index;
      if (person.raised !== raised) {
        person.raised = raised;
        person.changed = time;
        if (raised) person.raiseTrack = (seed % 3 < .9 && this.motions.generated.length) ?
          this.motions.generated[index % this.motions.generated.length] : this.motions.gestures[index % 3];
      }
      const elapsed = time - person.changed;
      let track, localTime, intensity;
      if (person.raiseTrack && (raised || elapsed < .8)) {
        track = person.raiseTrack;
        localTime = raised ? Math.min(elapsed, track.hold + Math.min(1.2, .44 * (track.lower - track.hold))) :
          T.MathUtils.lerp(track.lower, track.seconds, finite(elapsed / .8, 0, 1));
        intensity = raised ? T.MathUtils.smoothstep(elapsed, 0, .30) : 1 - T.MathUtils.smoothstep(elapsed, .05, .8);
      } else if (talking) {
        track = this.motions.gestures[3 + index % 3];
        localTime = (time - state.speakingSince) % track.seconds;
        intensity = .66;
      } else {
        track = this.motions.seated[(this.desk ? 10 : 0) + (index * 3 + person.profile.portrait) % 10];
        const phase = (time + seed) % (track.seconds + 3.3 + seed % 2);
        localTime = Math.min(phase, track.seconds);
        intensity = T.MathUtils.smoothstep(localTime, 0, .7) * (1 - T.MathUtils.smoothstep(localTime, track.seconds - .8, track.seconds));
      }
      const pose = sample(track, localTime);
      const tilt = Math.sin(time * .91 + seed) * .016 + person.posture * .105;
      expressiveRotation(person.chest, person.chestRest, tilt + finite(pose[0] * radians, -.09, .09) * intensity,
        finite(pose[1] * radians, -.12, .12) * intensity, person.posture * .075);
      person.arms.forEach((arm, i) => poseArm(person, arm, pose, i, intensity, delta, this.desk, raised || talking || (person.raiseTrack && elapsed < .8)));
      let glance = Math.sin(time * .51 + seed) * .11;
      if (state.speaking !== null && !talking) {
        const speaker = this.people[state.speaking];
        if (speaker) glance = finite((speaker.seat[0] - person.seat[0]) * -.085, -.31, .31);
      }
      const nod = Math.sin(time * 1.26 + seed) ** 11 * .10 * (talking ? .25 : 1);
      expressiveRotation(person.head, person.headRest, -.045 + nod, glance, Math.sin(time * .6 + seed) * .024);
      if (time >= person.nextBlink) {
        person.blinkAt = time;
        person.nextBlink = time + 2.6 + Math.abs(Math.sin(time + seed)) * 4.1;
      }
      const blink = Math.max(0, 1 - Math.abs((time - person.blinkAt) / .081 - 1));
      person.speakBlend += ((talking ? 1 : 0) - person.speakBlend) * (1 - Math.exp(-12 * delta));
      const jaw = person.speakBlend * Math.max(.02, .19 + .20 * Math.sin(time * 23 + seed) + .13 * Math.sin(time * 34));
      for (const { weights, morphs } of person.shapes) {
        for (const [name, i] of morphs) {
          if (name.endsWith("eyeblinkleft") || name.endsWith("eyeblinkright")) weights[i] = blink;
          else if (name.endsWith("jawopen")) weights[i] = jaw;
          else if (name.endsWith("mouthpucker")) weights[i] = jaw * .20 * (1 + Math.sin(time * 12));
          else if (name.endsWith("browinnerup")) weights[i] = (raised ? .12 : .02) + jaw * .19;
        }
      }
    }
  }
}
