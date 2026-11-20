using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nara.Practice.Editor
{
    public static class NaraSeatedMotionAudit
    {
        const int Rate = 30;
        const float Step = 1f / Rate;
        static int tested, poses, frames, meshFrames, crossings, fixedBaseErrors, raisedErrors;
        static float deepestProxy, deepestDesk, maxStep, maxElbow, maxPalm;
        static int idleWristFlips, idleElbowFlips;
        static float maxIdleWristFlip, maxIdleElbowFlip;
        static string images;
        static readonly HashSet<string> floorTakes = new(), reactionTakes = new(), talkingTakes = new();

        [MenuItem("NARA/Final Project/Validate seated motion")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var restore = EditorSceneManager.GetSceneManagerSetup();
            tested = poses = frames = meshFrames = crossings = fixedBaseErrors = raisedErrors = 0;
            deepestProxy = deepestDesk = maxStep = maxElbow = maxPalm = 0;
            idleWristFlips = idleElbowFlips = 0;
            maxIdleWristFlip = maxIdleElbowFlip = 0;
            floorTakes.Clear(); reactionTakes.Clear(); talkingTakes.Clear();
            images = Environment.GetEnvironmentVariable("NARA_CONTACT_IMAGES");
            if (!string.IsNullOrEmpty(images)) Directory.CreateDirectory(images);
            try
            {
                CheckRoom(NaraPracticeBuild.If101ScenePath, false);
                CheckRoom(NaraPracticeBuild.ScenePath, true);
            }
            finally
            {
                if (restore.Any(s => s.isLoaded && !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(restore);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            }
            Debug.Log($"NARA_CONTACT_RESULT profilesRooms={tested} actions={poses} frames={frames} meshFrames={meshFrames} " +
                $"meshCrossings={crossings} deskIntrusionMM={1000*deepestDesk:F1} proxyIntrusionMM={-1000*deepestProxy:F1} " +
                $"maxWristStepMM={1000*maxStep:F1} maxElbowStepMM={1000*maxElbow:F1} maxPalmDegrees={maxPalm:F1} " +
                $"idleWristFlips={idleWristFlips} idleElbowFlips={idleElbowFlips} " +
                $"idleWristFlipMM={1000*maxIdleWristFlip:F1} idleElbowFlipMM={1000*maxIdleElbowFlip:F1} " +
                $"seatedBaseErrors={fixedBaseErrors} handRaiseErrors={raisedErrors} " +
                $"raiseClips={floorTakes.Count} localRaiseClips={floorTakes.Count(take => take.StartsWith("HY", StringComparison.Ordinal))} " +
                $"reactionClips={reactionTakes.Count} speechClips={talkingTakes.Count}");
            bool entireCatalog = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NARA_CONTACT_PROFILES"));
            if (tested == 0 || entireCatalog && tested != AvatarCatalog.ProfileCount * 2 ||
                entireCatalog && (floorTakes.Count != 3 + 2 * NaraAvatarPuppet.AuditLocalRaiseCount ||
                                  reactionTakes.Count != 11 || talkingTakes.Count != 4) ||
                poses != tested * 10 || fixedBaseErrors != 0 || raisedErrors != 0 ||
                crossings != 0 || deepestDesk > .003f || deepestProxy < -.008f || maxStep > .072f ||
                maxElbow > .095f || maxPalm > 24 || maxIdleWristFlip > .018f || maxIdleElbowFlip > .018f)
                throw new BuildFailedException("Seated motion audit found a contact, continuity or anchoring regression.");
        }

        static float Backtrack(Vector3 before, Vector3 step)
        {
            float distance = before.magnitude;
            return distance > .00001f ? Mathf.Clamp(-Vector3.Dot(before, step) / distance, 0, distance) : 0;
        }

        static void CheckRoom(string path, bool tables)
        {
            EditorSceneManager.OpenScene(path);
            var audience = Object.FindFirstObjectByType<NaraAudience>();
            Transform seat = audience.seats[2];
            var tops = audience.transform.parent.GetComponentsInChildren<BoxCollider>(true)
                .Where(c => c.name.StartsWith("Table white trapezoidal top", StringComparison.Ordinal)).ToArray();
            if (tops.Length != (tables ? 24 : 0)) throw new BuildFailedException("Unexpected classroom furniture.");
            Bounds? desk = tables ? tops.OrderBy(c => (c.bounds.center - seat.position).sqrMagnitude).First().bounds : null;
            var catalog = AvatarCatalog.Load().entries;
            for (int i = 0; i < catalog.Length; i++)
            {
                string selected = Environment.GetEnvironmentVariable("NARA_CONTACT_PROFILES");
                if (!string.IsNullOrEmpty(selected) && Array.IndexOf(selected.Split(','), i.ToString()) < 0) continue;
                var prefab = Resources.Load<GameObject>(AvatarCatalog.Models + catalog[i].id);
                var instance = Object.Instantiate(prefab, seat, false);
                try { CheckActor(instance.GetComponent<NaraAvatarPuppet>(), i, desk, tables ? "university" : "if110"); }
                finally { Object.DestroyImmediate(instance); }
            }
        }

        static void CheckActor(NaraAvatarPuppet actor, int profile, Bounds? desk, string room)
        {
            var bones = actor.GetComponentsInChildren<Transform>(true);
            Transform Joint(string n) => Array.Find(bones, t => t.name == "Bip01 " + n);
            var limbs = new[] { new Limb(Joint, "R"), new Limb(Joint, "L") };
            var anchors = new[] { Joint("Pelvis"), Joint("R Toe0"), Joint("L Toe0") };
            var original = anchors.Select(j => j.position).ToArray();
            var skin = actor.GetComponentsInChildren<SkinnedMeshRenderer>().Select(s => new Surface(s)).ToArray();
            Vector3 viewer = actor.transform.position - actor.transform.forward * 4 + Vector3.up * 1.65f;
            actor.SetDesk(desk);
            actor.SetMotionProfile(profile, 0, 2, 7);
            Vector3 lastRight = default, lastLeft = default;
            var elbows = new Vector3[2]; var wrists = new Quaternion[2];
            var wristSteps = new Vector3[2]; var elbowSteps = new Vector3[2]; var targets = new Vector3[2];
            var observed = new HashSet<string>();
            var holds = new HashSet<string>();
            int hits = 0;
            float gap = 0, table = 0, jump = 0, wristFlip = 0, elbowFlip = 0;
            bool longHoldRegression = false;
            float heightDuringHold = 0, heightBeforeCancel = 0;
            string hitLabel = "", gapLabel = "", jumpLabel = "", deskLabel = "", floorFailure = "", flipLabel = "";
            floorTakes.Add(actor.AuditFloorTake);
            for (int frame = 0; frame <= 107 * Rate; frame++)
            {
                float now = frame * Step;
                bool raise = now >= 72 && now < 74.25f || now >= 89 && now < 89.16f || now >= 90 && now < 105;
                bool talking = now >= 74.55f && now < 88;
                float turn = Mathf.Max(0, now - 74.55f) / 2.7f;
                var line = (AudienceLine)Mathf.Clamp(1 + (int)turn, 1, 5);
                actor.Animate(now, Step, true, talking ? .65f : 0, talking, viewer, raise,
                    line: line, lineProgress: Mathf.Repeat(turn, 1));
                string action = actor.AuditPose;
                if (now < 72) observed.Add(action);
                if (!string.IsNullOrEmpty(actor.AuditOverteReaction)) reactionTakes.Add(actor.AuditOverteReaction);
                if (!string.IsNullOrEmpty(actor.AuditOverteTalk)) talkingTakes.Add(actor.AuditOverteTalk);
                float clearance = actor.AuditClearance();
                if (clearance < gap) { gap = clearance; gapLabel = action + "@" + now.ToString("F2") + " " + actor.AuditWorst(); }
                Vector3 rightStep = limbs[0].wrist.position - lastRight;
                Vector3 leftStep = limbs[1].wrist.position - lastLeft;
                if (frame > 0)
                {
                    float distance = Mathf.Max(rightStep.magnitude, leftStep.magnitude);
                    if (distance > jump) { jump = distance; jumpLabel = action + "@" + now.ToString("F2"); }
                }
                lastRight = limbs[0].wrist.position; lastLeft = limbs[1].wrist.position;
                for (int a = 0; a < limbs.Length; a++)
                {
                    Vector3 wristStep = a == 0 ? rightStep : leftStep;
                    Vector3 elbowStep = limbs[a].elbow.position - elbows[a];
                    Vector3 target = actor.AuditWristTarget(a == 0);
                    if (frame > 0)
                    {
                        maxElbow = Mathf.Max(maxElbow, elbowStep.magnitude);
                        maxPalm = Mathf.Max(maxPalm, Quaternion.Angle(wrists[a], limbs[a].wrist.rotation));
                    }
                    if (now >= .5f && now < 72)
                    {
                        float w = Backtrack(wristSteps[a], wristStep);
                        float e = Backtrack(elbowSteps[a], elbowStep);
                        if (w >= .004f) idleWristFlips++;
                        if (e >= .004f) idleElbowFlips++;
                        if (w > wristFlip)
                        {
                            wristFlip = w;
                            flipLabel = $"{action}@{now:F2}/{(a == 0 ? "R" : "L")} targetMM={1000*Vector3.Distance(targets[a], target):F1}";
                        }
                        elbowFlip = Mathf.Max(elbowFlip, e);
                    }
                    wristSteps[a] = wristStep;
                    elbowSteps[a] = elbowStep;
                    targets[a] = target;
                    elbows[a] = limbs[a].elbow.position; wrists[a] = limbs[a].wrist.rotation;
                }
                float hand = actor.FloorWrist.position.y;
                if (frame == 73 * Rate)
                {
                    heightDuringHold = hand;
                    float otherHand = actor.FloorWrist == actor.RightWrist ? lastLeft.y : lastRight.y;
                    float face = actor.Head.position.y;
                    if (!actor.AuditFloorHeld || hand < face - .12f || otherHand > face - .16f)
                        floorFailure += $" hold={hand:F2} support={otherHand:F2} face={face:F2}";
                }
                if (frame == 76 * Rate && (!actor.AuditFloorAtRest || hand > heightDuringHold - .14f))
                    floorFailure += $" permission={hand:F2}/raised={heightDuringHold:F2}";
                if (frame == 89 * Rate - 1) heightBeforeCancel = hand;
                if (frame == 89 * Rate + 20 && (!actor.AuditFloorAtRest ||
                    hand > Mathf.Max(actor.Head.position.y - .12f, heightBeforeCancel + .12f)))
                    floorFailure += $" earlyRelease={hand:F2}/rest={heightBeforeCancel:F2}";
                if (frame == 105 * Rate - 1) heightDuringHold = hand;
                if (frame == 106 * Rate && (!actor.AuditFloorAtRest || hand > heightDuringHold - .14f))
                    floorFailure += $" longRelease={hand:F2}/raised={heightDuringHold:F2}";
                if (frame >= 91 * Rate && frame < 105 * Rate)
                    longHoldRegression |= !actor.AuditFloorHeld || hand < actor.Head.position.y - .16f;
                float phase = actor.AuditPhase(now);
                string label = null;
                if (now < 72 && phase is > .25f and < .80f)
                    label = action + (phase < .41f ? "-enter" : phase < .62f ? "-hold" : "-exit");
                if (frame == 72 * Rate + 8 || frame == 73 * Rate || frame == 74 * Rate + 12 ||
                    frame == 89 * Rate + 7 || frame == 90 * Rate + 23 || frame == 104 * Rate || frame == 105 * Rate + 9)
                    label = "raise-" + frame;
                if (talking && Mathf.Repeat(turn, 1) is > .40f and < .47f) label = line.ToString();
                if (clearance < -.006f) label = "tight-contact-" + frame;
                if (label != null && holds.Add(label))
                {
                    meshFrames++;
                    foreach (Surface surface in skin)
                    {
                        float lastDepth = table;
                        int count = surface.Check(limbs, desk, ref table);
                        if (table > lastDepth) deskLabel = label + "/" + surface.DeskHit;
                        if (count > 0 && hits == 0) hitLabel = label + "/" + surface.Hit;
                        hits += count;
                    }
                    if (!string.IsNullOrEmpty(images) && (profile == 2 || profile == 3 || profile == 10) && (label == "FoldArms-hold" ||
                        label == "Think-hold" || label == "AdjustCuff-hold" || frame == 73 * Rate || label == "FirstQuestion"))
                        Capture(actor, room + "-" + profile + "-" + label, frame == 73 * Rate);
                }
                frames++;
            }
            if (anchors.Where((j, i) => (j.position - original[i]).sqrMagnitude > .000001f).Any()) fixedBaseErrors++;
            if (floorFailure.Length > 0 || longHoldRegression) raisedErrors++;
            poses += observed.Count; crossings += hits; deepestProxy = Mathf.Min(deepestProxy, gap);
            deepestDesk = Mathf.Max(deepestDesk, table); maxStep = Mathf.Max(maxStep, jump); tested++;
            maxIdleWristFlip = Mathf.Max(maxIdleWristFlip, wristFlip);
            maxIdleElbowFlip = Mathf.Max(maxIdleElbowFlip, elbowFlip);
            Debug.Log($"NARA_CONTACT {room} {actor.name} actions={observed.Count} mesh={holds.Count} crossings={hits} {hitLabel} " +
                $"proxyMM={gap*1000:F1} {gapLabel} deskMM={table*1000:F1} {deskLabel} jumpMM={jump*1000:F1} {jumpLabel} " +
                $"idleWristFlipMM={1000*wristFlip:F1} idleElbowFlipMM={1000*elbowFlip:F1} {flipLabel} " +
                $"floor={actor.AuditFloorTake} releaseOK={floorFailure.Length == 0} longHoldOK={!longHoldRegression}{floorFailure}");
        }

        sealed class Limb
        {
            public readonly Transform shoulder, elbow, wrist;
            public readonly Transform[] tips;
            public Limb(Func<string, Transform> joint, string side)
            {
                shoulder = joint(side + " UpperArm"); elbow = joint(side + " Forearm"); wrist = joint(side + " Hand");
                tips = new Transform[5];
                for (int i = 0; i < tips.Length; i++)
                {
                    var t = joint(side + " Finger" + i);
                    while (t.childCount > 0) t = t.GetChild(0);
                    tips[i] = t;
                }
            }
        }

        sealed class Surface
        {
            readonly NaraSkinGeometry skin;
            readonly int[] triangles, handVertices;
            readonly int[] groups;
            public string Hit { get; private set; }
            public string DeskHit { get; private set; }
            public Surface(SkinnedMeshRenderer renderer)
            {
                skin = new NaraSkinGeometry(renderer);
                int BoneGroup(string n)
                {
                    if (n == "Bip01 Pelvis" || n.StartsWith("Bip01 Spine") || n == "Bip01 Head" ||
                        n == "Bip01 Neck" || n.EndsWith("Thigh") || n.EndsWith("Clavicle")) return 1;
                    if (n.EndsWith("UpperArm") || n.Contains("Forearm") || n.EndsWith(" Hand") || n.Contains("Finger"))
                        return n.StartsWith("Bip01 R ") ? 2 : 3;
                    return 0;
                }
                var vertexGroups = skin.Weights.Select(w => BoneGroup(skin.Bones[w.boneIndex0].name)).ToArray();
                handVertices = Enumerable.Range(0, vertexGroups.Length).Where(i => vertexGroups[i] >= 2).ToArray();
                var tris = renderer.sharedMesh.triangles;
                var chosen = new List<int>(); var parts = new List<int>();
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int group = vertexGroups[tris[i]];
                    if (group == 0 || vertexGroups[tris[i+1]] != group || vertexGroups[tris[i+2]] != group) continue;
                    chosen.Add(tris[i]); chosen.Add(tris[i+1]); chosen.Add(tris[i+2]); parts.Add(group);
                }
                triangles = chosen.ToArray(); groups = parts.ToArray();
            }

            public int Check(Limb[] arms, Bounds? desk, ref float deepest)
            {
                skin.Sample();
                if (desk.HasValue)
                {
                    Bounds top = desk.Value;
                    foreach (int i in handVertices)
                    {
                        Vector3 v = skin.World[i];
                        if (v.x > top.min.x + .002f && v.x < top.max.x - .002f &&
                            v.z > top.min.z + .002f && v.z < top.max.z - .002f)
                        {
                            float depth = top.max.y - v.y;
                            if (depth > deepest) { deepest = depth; DeskHit = skin.Bones[skin.Weights[i].boneIndex0].name; }
                        }
                    }
                }
                int hits = 0;
                for (int side = 0; side < arms.Length; side++)
                {
                    Limb arm = arms[side];
                    for (int j = -2; j < arm.tips.Length; j++)
                    {
                        Vector3 a = j == -2 ? Vector3.Lerp(arm.shoulder.position, arm.elbow.position, .68f) :
                            j == -1 ? arm.elbow.position : arm.wrist.position;
                        Vector3 b = j == -2 ? arm.elbow.position : j == -1 ? arm.wrist.position : arm.tips[j].position;
                        Vector3 lo = Vector3.Min(a, b), hi = Vector3.Max(a, b), dir = b - a;
                        for (int i = 0; i < groups.Length; i++)
                        {
                            if (groups[i] == side + 2) continue;
                            Vector3 v = skin.World[triangles[i*3]], w = skin.World[triangles[i*3+1]], x = skin.World[triangles[i*3+2]];
                            if (Outside(v.x,w.x,x.x,lo.x,hi.x) || Outside(v.y,w.y,x.y,lo.y,hi.y) ||
                                Outside(v.z,w.z,x.z,lo.z,hi.z)) continue;
                            if (Pierces(a, dir, v, w, x))
                            {
                                if (hits == 0) Hit = $"arm{side} digit{j} to {skin.Bones[skin.Weights[triangles[i*3]].boneIndex0].name}";
                                hits++;
                            }
                        }
                    }
                }
                return hits;
            }

            static bool Outside(float a, float b, float c, float lo, float hi) =>
                a < lo && b < lo && c < lo || a > hi && b > hi && c > hi;

            static bool Pierces(Vector3 a, Vector3 dir, Vector3 v, Vector3 w, Vector3 x)
            {
                Vector3 e = w - v, other = x - v;
                Vector3 p = Vector3.Cross(dir, other);
                float det = Vector3.Dot(e, p);
                if (Mathf.Abs(det) < .00000001f) return false;
                Vector3 d = a - v;
                float u = Vector3.Dot(d, p) / det;
                if (u < 0 || u > 1) return false;
                Vector3 q = Vector3.Cross(d, e);
                float k = Vector3.Dot(dir, q) / det;
                if (k < 0 || u + k > 1) return false;
                float fraction = Vector3.Dot(other, q) / det;
                return fraction is > .004f and < .996f;
            }
        }

        static void Capture(NaraAvatarPuppet actor, string label, bool raised)
        {
            var camera = new GameObject("Contact audit camera").AddComponent<Camera>();
            var frame = RenderTexture.GetTemporary(720, 720, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(720, 720, TextureFormat.RGB24, false);
            var old = RenderTexture.active;
            var models = new List<(SkinnedMeshRenderer source, GameObject snapshot, Mesh mesh)>();
            try
            {
                // Batch editor cameras may reuse a GPU skin from the previous render.
                // Snapshot current bone matrices so each diagnostic image shows this pose.
                foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var surface = new NaraSkinGeometry(renderer);
                    surface.Sample();
                    Mesh mesh = Object.Instantiate(renderer.sharedMesh);
                    mesh.vertices = surface.World.Select(renderer.transform.InverseTransformPoint).ToArray();
                    mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
                    var snapshot = new GameObject("Pose snapshot");
                    snapshot.transform.SetParent(renderer.transform, false);
                    snapshot.AddComponent<MeshFilter>().sharedMesh = mesh;
                    snapshot.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                    models.Add((renderer, snapshot, mesh));
                    renderer.enabled = false;
                }
                Vector3 face = actor.Head.position, look = face - Vector3.up * (raised ? .135f : .25f);
                camera.targetTexture = frame; camera.nearClipPlane = .035f; camera.fieldOfView = 45;
                for (int i = 0; i < 2; i++)
                {
                    camera.transform.position = look - actor.transform.forward * (i == 0 ? 1.48f : .76f)
                        + actor.transform.right * (i == 0 ? .20f : 1.32f) + Vector3.up * .20f;
                    camera.transform.LookAt(look);
                    camera.Render(); RenderTexture.active = frame;
                    image.ReadPixels(new Rect(0, 0, 720, 720), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(images, label + (i == 0 ? "-front.jpg" : "-side.jpg")), image.EncodeToJPG(92));
                }
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = old; RenderTexture.ReleaseTemporary(frame);
                foreach (var model in models)
                {
                    model.source.enabled = true; Object.DestroyImmediate(model.snapshot); Object.DestroyImmediate(model.mesh);
                }
                Object.DestroyImmediate(image); Object.DestroyImmediate(camera.gameObject);
            }
        }
    }
}
