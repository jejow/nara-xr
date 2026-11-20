using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nara.Practice
{
    public sealed partial class NaraAvatarPuppet
    {
        [Serializable]
        struct BodySlice { public Vector2 center, half; }
        [Serializable]
        struct HandPoint { public Transform joint; public Vector3 offset; }
        [SerializeField] HandPoint[] rightPalmSurface, leftPalmSurface;
        [SerializeField] Transform hips, rightThigh, rightKnee, leftThigh, leftKnee;
        [SerializeField] BodySlice[] torsoSlices;
        [SerializeField] Vector3 headCenter, headRadii;
        [SerializeField] float sleeveRadius, palmRadius, legRadius, thighTop;

        // Y is height from the seat marker, Z is forward toward the presenter.
        Vector3 SeatPoint(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            return new Vector3(p.x, p.y, -p.z);
        }

        Vector3 WorldPoint(Vector3 p) => transform.TransformPoint(new Vector3(p.x, p.y, -p.z));
        Vector3 WorldDirection(Vector3 p) => transform.TransformDirection(new Vector3(p.x, p.y, -p.z));

#if UNITY_EDITOR
        // Six extremal skin vertices per finger and palm capture long nails and
        // finger thickness that a line through the middle finger cannot measure.
        sealed class HandSurfaceFit
        {
            readonly HandPoint[,] points = new HandPoint[6, 6];
            readonly float[,] depths = new float[6, 6];
            static readonly Vector3[] directions = { Vector3.up, Vector3.down, Vector3.left,
                Vector3.right, Vector3.forward, Vector3.back };

            public void Add(Transform bone, Vector3 world, int part)
            {
                for (int axis = 0; axis < directions.Length; axis++)
                {
                    float depth = Vector3.Dot(world, directions[axis]);
                    if (points[part, axis].joint && depths[part, axis] >= depth) continue;
                    depths[part, axis] = depth;
                    points[part, axis] = new HandPoint { joint = bone, offset = bone.InverseTransformPoint(world) };
                }
            }

            public HandPoint[] Finish()
            {
                var result = new List<HandPoint>();
                foreach (HandPoint point in points)
                    if (point.joint && !result.Contains(point)) result.Add(point);
                return result.ToArray();
            }
        }

        void FitContactSurfaces(Transform model, Func<string, Transform> joint)
        {
            hips = joint("Pelvis");
            rightThigh = joint("R Thigh"); rightKnee = joint("R Calf");
            leftThigh = joint("L Thigh"); leftKnee = joint("L Calf");
            Transform rightMiddle = joint("R Finger2"), leftMiddle = joint("L Finger2");
            Vector3 pelvis = SeatPoint(hips.position);
            Vector3 shoulders = SeatPoint((rightUpperArm.position + leftUpperArm.position) * .5f);
            var trunk = new List<Vector3>();
            var face = new List<Vector3>();
            var sleeve = new List<float>();
            var thigh = new List<float>();
            var upperThigh = new List<float>();
            var palm = new List<float>();
            var rightSurface = new HandSurfaceFit(); var leftSurface = new HandSurfaceFit();
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var geometry = new NaraSkinGeometry(renderer);
                geometry.Sample();
                for (int i = 0; i < geometry.World.Length; i++)
                {
                    Transform bone = geometry.Bones[geometry.Weights[i].boneIndex0];
                    Vector3 p = SeatPoint(geometry.World[i]);
                    string n = bone.name;
                    bool right = n.StartsWith("Bip01 R ", StringComparison.Ordinal);
                    int finger = n.IndexOf(" Finger", StringComparison.Ordinal);
                    bool isPalm = n.EndsWith(" Hand", StringComparison.Ordinal);
                    if (finger >= 0 || isPalm)
                    {
                        int part = isPalm ? 0 : n[finger + 7] - '0' + 1;
                        if ((uint)part < 6) (right ? rightSurface : leftSurface).Add(bone, geometry.World[i], part);
                    }
                    if (n == "Bip01 Pelvis" || n.StartsWith("Bip01 Spine", StringComparison.Ordinal) ||
                        n.EndsWith("Clavicle", StringComparison.Ordinal))
                        trunk.Add(p);
                    else if (bone == head || bone.IsChildOf(head))
                        face.Add(p);
                    else if (n.EndsWith("Forearm", StringComparison.Ordinal))
                        CollectRadius(sleeve, p, right ? rightForearm : leftForearm, right ? rightHand : leftHand);
                    else if (n.EndsWith("Thigh", StringComparison.Ordinal))
                        CollectRadius(thigh, p, right ? rightThigh : leftThigh, right ? rightKnee : leftKnee, upperThigh);
                    else if (n.EndsWith(" Hand", StringComparison.Ordinal))
                        CollectRadius(palm, p, bone, right ? rightMiddle : leftMiddle);
                }
            }

            if (trunk.Count < 20 || face.Count < 20)
                throw new InvalidOperationException("Cannot fit contact surfaces to " + name);
            torsoSlices = new BodySlice[5];
            for (int i = 0; i < torsoSlices.Length; i++)
            {
                float height = Mathf.Lerp(pelvis.y - .055f, shoulders.y + .015f, i / 4f);
                Vector3 axis = Vector3.Lerp(pelvis, shoulders, Mathf.InverseLerp(pelvis.y, shoulders.y, height));
                var xs = new List<float>(); var zs = new List<float>();
                float closest = float.MaxValue;
                foreach (Vector3 point in trunk) closest = Mathf.Min(closest, Mathf.Abs(point.y - height));
                if (closest > .23f)
                    throw new InvalidOperationException($"Body mesh disagrees with rig on {name}: target {height:F3}, " +
                        $"closest {closest:F3}, first vertex {trunk[0]}, pelvis {pelvis}, shoulders {shoulders}.");
                float band = Mathf.Max(.064f, closest + .035f);
                foreach (Vector3 p in trunk)
                    if (Mathf.Abs(p.y - height) <= band)
                    {
                        xs.Add(p.x - axis.x); zs.Add(p.z - axis.z);
                    }
                if (xs.Count == 0)
                    throw new InvalidOperationException($"Missing torso section {i} on {name}");
                Vector2 lo = new(Quantile(xs, .014f), Quantile(zs, .008f));
                Vector2 hi = new(Quantile(xs, .986f), Quantile(zs, .994f));
                torsoSlices[i] = new BodySlice { center = (hi + lo) * .5f,
                    half = Vector2.Max((hi - lo) * .5f, new Vector2(.090f, .069f)) };
            }
            Vector3 RangeOfHead(float q) => new(Quantile(face.ConvertAll(p => p.x), q),
                Quantile(face.ConvertAll(p => p.y), q), Quantile(face.ConvertAll(p => p.z), q));
            Vector3 low = RangeOfHead(.012f), high = RangeOfHead(.99f);
            headCenter = head.InverseTransformPoint(WorldPoint((low + high) * .5f));
            headRadii = Vector3.Max((high - low) * .5f, new Vector3(.076f, .104f, .08f));
            sleeveRadius = Mathf.Clamp(Quantile(sleeve, .89f), .034f, .073f);
            palmRadius = Mathf.Clamp(Quantile(palm, .83f), .029f, .046f);
            legRadius = Mathf.Clamp(Quantile(thigh, .85f), .067f, .119f);
            thighTop = Mathf.Clamp(Quantile(upperThigh, .985f), .061f, .205f);
            rightPalmSurface = rightSurface.Finish();
            leftPalmSurface = leftSurface.Finish();
        }

        void CollectRadius(List<float> samples, Vector3 point, Transform start, Transform end, List<float> top = null)
        {
            Vector3 a = SeatPoint(start.position), span = SeatPoint(end.position) - a;
            float t = Vector3.Dot(point - a, span) / span.sqrMagnitude;
            if (t > .10f && t < .88f)
            {
                Vector3 offset = point - a - span * t;
                samples.Add(offset.magnitude);
                top?.Add(offset.y);
            }
        }

        static float Quantile(List<float> values, float q)
        {
            if (values.Count == 0) return 0;
            values.Sort();
            return values[Mathf.RoundToInt((values.Count - 1) * q)];
        }
#endif
    }
}
