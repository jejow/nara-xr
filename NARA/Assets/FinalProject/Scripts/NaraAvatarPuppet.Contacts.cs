using UnityEngine;

namespace Nara.Practice
{
    public sealed partial class NaraAvatarPuppet
    {
        sealed class ArmRig
        {
            public Transform upper, lower, wrist, finger;
            public Transform thumbTip, littleTip;
            public Quaternion upperRest, lowerRest, palmBasis;
            public float side, upperLength, lowerLength, handLength;
            public HandPoint[] handSurface;
            public float deskWristHeight;
            public Vector3 requested, previousRequest, pole, fingerDir, across;
            public Vector3 shoulderPoint, elbowPoint, wristPoint, tipPoint, bend, appliedWidth;
            public Vector3 thumbOffset, littleOffset;
            public ContactSegment upperSegment, forearmSegment, handSegment;
            public bool ready, solved;
            public float Length => upperLength + lowerLength;
        }

        ArmRig rightArm, leftArm;
        Vector3 pelvisPoint, shouldersPoint, facePoint, rightLeg, rightCalf, leftLeg, leftCalf, chestX, chestZ;
        Vector3 trunkMin, trunkMax, chestSpan, faceMin, faceMax, inverseHead;
        Vector2 trunkExtent;
        float bottom, top, inverseBandHeight, inverseSpineHeight, headAspect;
        ContactSegment rightLegSegment, leftLegSegment;
        Bounds deskSurface;
        Vector3 deskMin, deskMax;
        bool hasDesk;
        internal bool UsesDeskMotion => hasDesk;

#if UNITY_EDITOR
        public string AuditPose => ListeningAction.ToString();
        public Vector3 AuditWristTarget(bool right) => (right ? rightArm : leftArm).requested;
        public string AuditWorst()
        {
            string Detail(ArmRig arm, ArmRig other)
            {
                float d = ArmGap(arm, arm.shoulderPoint, arm.elbowPoint, arm.wristPoint, arm.tipPoint - arm.wristPoint,
                    other, out Vector3 p);
                return $"{arm.side:F0}:{p:F2}/{d:F3} body={BodyDistance(p):F3} upper={arm.shoulderPoint:F2} elbow={arm.elbowPoint:F2} wrist={arm.wristPoint:F2}";
            }
            return Detail(rightArm, leftArm) + " | " + Detail(leftArm, rightArm);
        }
        public float AuditPhase(float now) => (now - idleStart) / idleDuration;
        public float AuditClearance()
        {
            float Measured(ArmRig arm, ArmRig other)
            {
                Vector3 w = SeatPoint(arm.wrist.position);
                Vector3 tip = (SeatPoint(arm.finger.position) - w).normalized * arm.handLength;
                return ArmGap(arm, SeatPoint(arm.upper.position), SeatPoint(arm.lower.position), w, tip, other, out _);
            }
            return primaryRight ? Mathf.Min(Measured(rightArm, null), Measured(leftArm, rightArm)) :
                Mathf.Min(Measured(leftArm, null), Measured(rightArm, leftArm));
        }
#endif

        void BindArmContacts()
        {
            // Scenes saved before the fitted profiles were introduced can still play.
            if (!hips || torsoSlices == null || torsoSlices.Length < 2)
            {
                var joints = GetComponentsInChildren<Transform>(true);
                Transform Bone(string n) => System.Array.Find(joints, t => t.name == "Bip01 " + n);
                hips = Bone("Pelvis"); rightThigh = Bone("R Thigh"); rightKnee = Bone("R Calf");
                leftThigh = Bone("L Thigh"); leftKnee = Bone("L Calf");
                torsoSlices = new[] { new BodySlice { half = new Vector2(.16f, .115f) },
                    new BodySlice { half = new Vector2(.17f, .115f) } };
                headCenter = head.InverseTransformPoint(head.position + .082f * transform.up);
                headRadii = new Vector3(.09f, .13f, .10f);
                sleeveRadius = .051f; palmRadius = .037f; legRadius = .09f;
            }
            ArmRig Bind(Transform upper, Transform lower, Transform wrist, string prefix)
            {
                Transform digit(int n) => wrist.Find("Bip01 " + prefix + " Finger" + n);
                var middle = digit(2);
                Transform End(Transform bone)
                {
                    while (bone.childCount > 0) bone = bone.GetChild(0);
                    return bone;
                }
                Transform fingertip = End(middle);
                Vector3 toward = wrist.InverseTransformDirection(middle.position - wrist.position);
                Vector3 width = wrist.InverseTransformDirection(digit(1).position - digit(4).position);
                return new ArmRig
                {
                    upper = upper, lower = lower, wrist = wrist, finger = middle,
                    thumbTip = End(digit(0)), littleTip = End(digit(4)),
                    upperRest = upper.localRotation, lowerRest = lower.localRotation,
                    upperLength = Vector3.Distance(upper.position, lower.position),
                    lowerLength = Vector3.Distance(lower.position, wrist.position),
                    handLength = Mathf.Clamp(Mathf.Max(Vector3.Distance(wrist.position, middle.position) * 1.75f,
                        Vector3.Distance(wrist.position, fingertip.position)), .115f, .195f),
                    side = Mathf.Sign(SeatPoint(upper.position).x - SeatPoint(hips.position).x),
                    palmBasis = Quaternion.Inverse(Quaternion.LookRotation(toward, Vector3.Cross(toward, width)))
                };
            }
            rightArm = Bind(rightUpperArm, rightForearm, rightHand, "R");
            leftArm = Bind(leftUpperArm, leftForearm, leftHand, "L");
            rightArm.handSurface = rightPalmSurface;
            leftArm.handSurface = leftPalmSurface;
            foreach (BodySlice slice in torsoSlices)
                trunkExtent = Vector2.Max(trunkExtent, new Vector2(Mathf.Abs(slice.center.x) + slice.half.x,
                    Mathf.Abs(slice.center.y) + slice.half.y));
            inverseHead = new Vector3(1 / headRadii.x, 1 / headRadii.y, 1 / headRadii.z);
            headAspect = Mathf.Max(headRadii.x, Mathf.Max(headRadii.y, headRadii.z)) *
                Mathf.Max(inverseHead.x, Mathf.Max(inverseHead.y, inverseHead.z));
        }

        public void SetDesk(Bounds? worldTable)
        {
            hasDesk = worldTable.HasValue;
            if (!hasDesk) return;
            Bounds world = worldTable.Value;
            deskSurface = new Bounds(SeatPoint(world.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                deskSurface.Encapsulate(SeatPoint(world.center + Vector3.Scale(world.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            deskMin = deskSurface.min; deskMax = deskSurface.max;
        }

        void UpdateContactFrame()
        {
            pelvisPoint = SeatPoint(hips.position);
            Vector3 r = SeatPoint(rightUpperArm.position), l = SeatPoint(leftUpperArm.position);
            shouldersPoint = (r + l) * .5f;
            chestX = Vector3.ProjectOnPlane((r - l) * rightArm.side, Vector3.up).normalized;
            chestZ = new Vector3(-chestX.z, 0, chestX.x);
            facePoint = SeatPoint(head.TransformPoint(headCenter));
            rightLeg = SeatPoint(rightThigh.position); rightCalf = SeatPoint(rightKnee.position);
            leftLeg = SeatPoint(leftThigh.position); leftCalf = SeatPoint(leftKnee.position);
            rightLegSegment.Set(rightLeg, rightCalf); leftLegSegment.Set(leftLeg, leftCalf);
            faceMin = facePoint - headRadii; faceMax = facePoint + headRadii;
            bottom = pelvisPoint.y - .055f; top = shouldersPoint.y + .015f;
            chestSpan = shouldersPoint - pelvisPoint;
            inverseBandHeight = 1 / (top - bottom);
            inverseSpineHeight = 1 / chestSpan.y;
            Vector3 extent = new(Mathf.Abs(chestX.x) * trunkExtent.x + Mathf.Abs(chestZ.x) * trunkExtent.y,
                0, Mathf.Abs(chestX.z) * trunkExtent.x + Mathf.Abs(chestZ.z) * trunkExtent.y);
            trunkMin = Vector3.Min(pelvisPoint, shouldersPoint) - extent;
            trunkMax = Vector3.Max(pelvisPoint, shouldersPoint) + extent;
            trunkMin.y = bottom; trunkMax.y = top;
        }

        static bool Outside(Vector3 p, Vector3 lo, Vector3 hi, float pad) =>
            p.x < lo.x - pad || p.x > hi.x + pad || p.z < lo.z - pad || p.z > hi.z + pad ||
            p.y < lo.y - pad || p.y > hi.y + pad;

        struct ContactSegment
        {
            Vector3 start, span, lo, hi;
            float inverseLengthSquared;

            public void Set(Vector3 a, Vector3 b)
            {
                start = a; span = b - a; lo = Vector3.Min(a, b); hi = Vector3.Max(a, b);
                inverseLengthSquared = 1 / Mathf.Max(.000001f, span.sqrMagnitude);
            }

            public float Reduce(Vector3 p, float radius, float nearest)
            {
                float reach = radius + nearest;
                if (reach <= 0 || Outside(p, lo, hi, reach)) return nearest;
                float x = p.x - start.x, y = p.y - start.y, z = p.z - start.z;
                float t = Mathf.Clamp01((x * span.x + y * span.y + z * span.z) * inverseLengthSquared);
                x -= t * span.x; y -= t * span.y; z -= t * span.z;
                float squared = x*x + y*y + z*z;
                return squared >= reach*reach ? nearest : Mathf.Sqrt(squared) - radius;
            }
        }

        float TorsoDistance(Vector3 p)
        {
            float band = Mathf.Clamp01((p.y - bottom) * inverseBandHeight) * (torsoSlices.Length - 1);
            int i = Mathf.Min((int)band, torsoSlices.Length - 2);
            BodySlice a = torsoSlices[i], b = torsoSlices[i + 1];
            float t = band - i;
            float w = Mathf.LerpUnclamped(a.half.x, b.half.x, t), depth = Mathf.LerpUnclamped(a.half.y, b.half.y, t);
            float spine = Mathf.Clamp01((p.y - pelvisPoint.y) * inverseSpineHeight);
            float dx = p.x - (pelvisPoint.x + chestSpan.x * spine), dz = p.z - (pelvisPoint.z + chestSpan.z * spine);
            float corner = Mathf.Min(w, depth) * .57f;
            float x = Mathf.Abs(dx * chestX.x + dz * chestX.z - Mathf.LerpUnclamped(a.center.x, b.center.x, t)) - w + corner;
            float z = Mathf.Abs(dx * chestZ.x + dz * chestZ.z - Mathf.LerpUnclamped(a.center.y, b.center.y, t)) - depth + corner;
            float horizontal = (x > 0 && z > 0 ? Mathf.Sqrt(x*x + z*z) : Mathf.Max(x, z)) - corner;
            return ExtrudedDistance(horizontal, Mathf.Max(bottom - p.y, p.y - top));
        }

        static float ExtrudedDistance(float horizontal, float vertical) =>
            horizontal > 0 && vertical > 0 ? Mathf.Sqrt(horizontal * horizontal + vertical * vertical) :
            Mathf.Max(horizontal, vertical);

        float BodyDistance(Vector3 p, float limit = 4)
        {
            float distance = Outside(p, trunkMin, trunkMax, limit) ? limit : Mathf.Min(limit, TorsoDistance(p));
            if (p.y > facePoint.y - headRadii.y - .11f && !Outside(p, faceMin, faceMax, Mathf.Max(0, distance) * headAspect))
            {
                Vector3 d = p - facePoint;
                Vector3 n = Vector3.Scale(d, inverseHead);
                float k = n.magnitude;
                float slope = Vector3.Scale(n, inverseHead).magnitude;
                distance = Mathf.Min(distance, k < .0001f ? -headRadii.x : k * (k - 1) / slope);
            }
            if (p.y < Mathf.Max(rightLeg.y, leftLeg.y) + .24f)
            {
                distance = rightLegSegment.Reduce(p, legRadius, distance);
                distance = leftLegSegment.Reduce(p, legRadius, distance);
            }
            return distance;
        }

        float DeskDistance(Vector3 p)
        {
            Vector3 edge = p - deskSurface.center;
            float x = Mathf.Abs(edge.x) - deskSurface.extents.x;
            float z = Mathf.Abs(edge.z) - deskSurface.extents.z;
            return ExtrudedDistance(ExtrudedDistance(x, z), p.y - deskSurface.max.y);
        }

        float Clearance(Vector3 p, float radius, ArmRig other)
        {
            // Only nearby surfaces can intersect this sample. Normals need the
            // distance past the capsule radius on both sides of the sample.
            float gap = BodyDistance(p, radius > 0 ? radius + .018f : .22f);
            if (hasDesk && p.y < deskMax.y + gap && p.x > deskMin.x - gap && p.x < deskMax.x + gap &&
                p.z > deskMin.z - gap && p.z < deskMax.z + gap) gap = Mathf.Min(gap, DeskDistance(p));
            if (other != null)
            {
                gap = other.upperSegment.Reduce(p, sleeveRadius, gap);
                gap = other.forearmSegment.Reduce(p, sleeveRadius, gap);
                gap = other.handSegment.Reduce(p, palmRadius, gap);
            }
            return gap - radius;
        }

        Vector3 ContactNormal(Vector3 p, ArmRig other)
        {
            const float h = .003f;
            float d = Clearance(p, 0, other);
            Vector3 gradient = new(Clearance(p + Vector3.right * h, 0, other) - d,
                Clearance(p + Vector3.up * h, 0, other) - d, Clearance(p + Vector3.forward * h, 0, other) - d);
            return gradient.sqrMagnitude > .00000001f ? gradient.normalized : Vector3.forward;
        }

        static Vector3 ArmAxes(Vector3 v, float side) => new(side * v.x, v.y, v.z);

        void SetArmPose(ArmRig arm, RecordedArm track, Vector3 mark, float weight, Vector3 fingerMark,
            Vector3 palmAcross, Vector3 poleMark, float lift, ArmRig other, float dt)
        {
            Vector3 shoulder = SeatPoint(arm.upper.position);
            Vector3 target = shoulder + ArmAxes(track.wrist, arm.side) * arm.Length;
            Vector3 pole = ArmAxes(track.elbow, arm.side);
            Vector3 fingers = ArmAxes(track.fingers, arm.side).normalized;
            Vector3 across = ArmAxes(track.across, arm.side).normalized;
            BodySlice middle = torsoSlices[torsoSlices.Length / 2];
            float trunkFront = shouldersPoint.z + middle.center.y + middle.half.y;
            float separate = .080f + .70f * arm.handLength * Mathf.Max(0, -arm.side * fingers.x);
            target.x = shouldersPoint.x + arm.side * Mathf.Max(separate, arm.side * (target.x - shouldersPoint.x));
            target.z = Mathf.Max(target.z, trunkFront + .074f + arm.handLength * Mathf.Max(0, -fingers.z));
            if (hasDesk)
            {
                // Keep hands on the upper side of the desk while moving across its near edge.
                target.z = Mathf.Max(target.z, deskSurface.min.z + .025f);
                target.y = Mathf.Max(target.y, deskSurface.max.y + palmRadius + .024f);
                if (fingers.y < -.22f) fingers = new Vector3(fingers.x, -.22f, fingers.z).normalized;
            }
            target = Vector3.Lerp(target, mark, weight);
            fingers = Vector3.Slerp(fingers, fingerMark, weight).normalized;
            if (weight > 0)
                across = Vector3.Slerp(across, palmAcross, weight * .80f).normalized;
            pole = Vector3.Lerp(new Vector3(arm.side * .65f, -.75f, -.08f), pole.normalized, .23f);
            pole = Vector3.Lerp(pole, poleMark, weight);
            if (lift > .0001f)
                ApplyFloorPose(arm, shoulder, ref target, ref pole, ref fingers, ref across, lift);
            float lap = Mathf.Max(Mathf.Max(rightLeg.y, rightCalf.y), Mathf.Max(leftLeg.y, leftCalf.y)) +
                Mathf.Max(legRadius, thighTop);
            float floor = hasDesk && target.z + arm.handLength > deskSurface.min.z ? deskSurface.max.y : lap;
            float handDepth = Mathf.Max(.044f, palmRadius) + .023f + arm.handLength * Mathf.Max(0, -fingers.y);
            target.y = Mathf.Max(target.y, floor + handDepth);
            bool firstFrame = !arm.ready;
            if (firstFrame)
            {
                arm.requested = target; arm.pole = pole; arm.fingerDir = fingers; arm.across = across;
                arm.ready = true;
            }
            float blend = 1 - Mathf.Exp(-dt * 13f);
            arm.requested = Vector3.MoveTowards(arm.requested, Vector3.Lerp(arm.requested, target, blend), 1.65f * dt);
            arm.pole = Vector3.Lerp(arm.pole, pole, blend);
            arm.fingerDir = Vector3.RotateTowards(arm.fingerDir, fingers, 6f * dt, .2f).normalized;
            arm.across = Vector3.RotateTowards(arm.across, across, 6f * dt, .2f).normalized;
            Quaternion wristRotation = OrientWrist(arm, palmAcross, firstFrame, dt);
            Quaternion turn = wristRotation * Quaternion.Inverse(arm.wrist.rotation);
            Vector3 origin = arm.wrist.position;
            Vector3 HandOffset(Transform end)
            {
                Vector3 v = transform.InverseTransformVector(turn * (end.position - origin));
                return new Vector3(v.x, v.y, -v.z);
            }
            arm.thumbOffset = HandOffset(arm.thumbTip);
            arm.littleOffset = HandOffset(arm.littleTip);
            if (hasDesk)
            {
                arm.deskWristHeight = deskSurface.max.y + palmRadius + .006f;
                if (arm.requested.y < deskSurface.max.y + arm.handLength + .095f &&
                    arm.requested.z + arm.handLength > deskSurface.min.z - .035f && arm.handSurface != null)
                {
                    // Project the posed fingers onto the desk normal in the new wrist orientation.
                    Vector3 up = Quaternion.Inverse(turn) * transform.up;
                    float drop = 0;
                    foreach (HandPoint sample in arm.handSurface)
                        drop = Mathf.Max(drop, Vector3.Dot(origin - sample.joint.TransformPoint(sample.offset), up));
                    arm.deskWristHeight = Mathf.Max(arm.deskWristHeight, deskSurface.max.y + drop + .007f);
                }
            }
            arm.shoulderPoint = shoulder;
            FitArmPath(arm, shoulder, other, Mathf.Max(lift, speakingBlend), dt);
            arm.upperSegment.Set(Vector3.Lerp(shoulder, arm.elbowPoint, .5f), arm.elbowPoint);
            arm.forearmSegment.Set(arm.elbowPoint, arm.wristPoint);
            arm.handSegment.Set(arm.wristPoint, arm.tipPoint);
            Vector3 wristLine = (arm.wristPoint - shoulder).normalized;
            arm.bend = Vector3.ProjectOnPlane(arm.elbowPoint - shoulder, wristLine).normalized;
            arm.solved = true;
            arm.upper.localRotation = arm.upperRest;
            arm.lower.localRotation = arm.lowerRest;
            Aim(arm.upper, arm.lower, WorldDirection(arm.elbowPoint - shoulder));
            Aim(arm.lower, arm.wrist, WorldDirection(arm.wristPoint - arm.elbowPoint));
            arm.wrist.rotation = wristRotation;
        }

        Quaternion OrientWrist(ArmRig arm, Vector3 palmAcross, bool firstFrame, float dt)
        {
            Vector3 direction = WorldDirection(arm.fingerDir);
            Vector3 width = Vector3.ProjectOnPlane(arm.across, arm.fingerDir);
            Vector3 transported = Vector3.ProjectOnPlane(arm.appliedWidth, arm.fingerDir);
            if (width.sqrMagnitude < .035f) width = transported;
            if (width.sqrMagnitude < .001f) width = Vector3.ProjectOnPlane(palmAcross, arm.fingerDir);
            if (width.sqrMagnitude < .001f) width = Vector3.ProjectOnPlane(Vector3.up, arm.fingerDir);
            width.Normalize();
            if (!firstFrame && transported.sqrMagnitude > .01f)
            {
                float twist = Vector3.SignedAngle(transported, width, arm.fingerDir);
                width = Quaternion.AngleAxis(Mathf.Clamp(twist, -180f * dt, 180f * dt), arm.fingerDir) * transported.normalized;
            }
            arm.appliedWidth = width;
            return Quaternion.LookRotation(direction, Vector3.Cross(direction, WorldDirection(width))) * arm.palmBasis;
        }

        void FitArmPath(ArmRig arm, Vector3 shoulder, ArmRig other, float activity, float dt)
        {
            float travel = Mathf.Min(.090f, Mathf.Lerp(.48f, 1.60f, activity) * dt);
            float preferredTurn = Mathf.Lerp(.95f, 4.5f, activity) * dt;
            float clearanceTurn = Mathf.Lerp(1.40f, 8.5f, activity) * dt;
            Vector3 lastWrist = arm.wristPoint;
            Vector3 wrist = arm.requested;
            if (arm.solved)
            {
                // Carry the last contact correction forward so a resting hand does
                // not try to reach the obstructed target again in every frame.
                Vector3 correction = lastWrist - arm.previousRequest;
                float speed = Vector3.Distance(arm.requested, arm.previousRequest) / Mathf.Max(dt, .0001f);
                float release = Mathf.Lerp(2.5f, 11f, Mathf.InverseLerp(.045f, .50f, speed));
                wrist += correction * Mathf.Exp(-release * dt);
                wrist = Vector3.MoveTowards(lastWrist, wrist, travel);
            }
            arm.previousRequest = arm.requested;
            Vector3 tipOffset = arm.fingerDir * arm.handLength;
            float minReach = .32f * arm.Length, maxReach = .985f * arm.Length;
            float best = -100f;
            const int fitPasses = 6;
            int passes = fitPasses;
            for (int pass = 0; pass < passes; pass++)
            {
                for (int i = 0; i <= 3; i++)
                {
                    Vector3 p = wrist + tipOffset * (i / 3f);
                    float radius = Mathf.Lerp(palmRadius, .019f, i / 3f);
                    float gap = Clearance(p, radius, other);
                    if (gap < .008f) wrist += ContactNormal(p, other) * (.010f - gap);
                }
                if (hasDesk && wrist.z + arm.handLength > deskSurface.min.z - .012f)
                    wrist.y = Mathf.Max(wrist.y, arm.deskWristHeight);
                if (arm.solved) wrist = Vector3.MoveTowards(lastWrist, wrist, travel);
                Vector3 delta = wrist - shoulder;
                Vector3 line = delta.normalized;
                float reach = Mathf.Clamp(delta.magnitude, minReach, maxReach);
                wrist = shoulder + line * reach;
                float handGap = HandGap(arm, wrist, tipOffset, other, out Vector3 handPoint);
                float along = (arm.upperLength * arm.upperLength - arm.lowerLength * arm.lowerLength + reach * reach) / (2 * reach);
                Vector3 pivot = shoulder + line * along;
                float height = Mathf.Sqrt(Mathf.Max(0, arm.upperLength * arm.upperLength - along * along));
                Vector3 guide = ListeningAction == ListeningMotion.AdjustCuff && speakingBlend < .5f
                    ? new Vector3(arm.side * .69f, -1, -.23f) : new Vector3(arm.side, -.65f, -.05f);
                Vector3 outward = Vector3.ProjectOnPlane(guide, line);
                Vector3 hint = Vector3.ProjectOnPlane(arm.pole, line);
                if (hint.sqrMagnitude < .002f) hint = outward;
                hint = Vector3.Slerp(outward.normalized, hint.normalized, .60f).normalized;
                Vector3 previous = Vector3.ProjectOnPlane(arm.bend, line);
                bool keepBend = arm.solved && previous.sqrMagnitude > .10f;
                if (keepBend)
                {
                    previous.Normalize();
                    hint = Vector3.RotateTowards(previous, hint, preferredTurn, 0);
                }
                Vector3 worstPoint = wrist;
                Vector3 bestBend = hint;
                float roundBest = -100f;
                int candidates = keepBend ? 6 : arm.solved ? 5 : 11;
                for (int i = 0; i < candidates; i++)
                {
                    int candidate = keepBend && i > 0 ? i - 1 : i;
                    int step = (candidate + 1) / 2;
                    float angle = (candidate % 2 == 1 ? 1 : -1) * step * (3 + 2.6f * step);
                    // Keep the transported elbow when the new preferred bend needs clearance.
                    Vector3 bend = keepBend && i == 1 ? previous : Quaternion.AngleAxis(angle, line) * hint;
                    if (keepBend)
                        bend = Vector3.RotateTowards(previous, bend, clearanceTurn, 0);
                    Vector3 elbow = pivot + bend * height;
                    float gap = ElbowGap(shoulder, elbow, wrist, other, handGap, handPoint, roundBest, out Vector3 point);
                    if (gap > roundBest) { roundBest = gap; worstPoint = point; bestBend = bend; }
                    if (gap > best)
                    {
                        best = gap; arm.elbowPoint = elbow; arm.wristPoint = wrist; arm.tipPoint = wrist + tipOffset;
                    }
                    if (gap >= .002f) return;
                }
                Vector3 normal = ContactNormal(worstPoint, other);
                // Near the shoulder a small bend around the reach axis clears a broad
                // sleeve more effectively than pulling the wrist away from its mark.
                if ((worstPoint - wrist).sqrMagnitude > arm.lowerLength * arm.lowerLength * .31f)
                {
                    Vector3 bend = Vector3.ProjectOnPlane(bestBend * height + normal * .14f, line).normalized;
                    if (keepBend)
                        bend = Vector3.RotateTowards(previous, bend, clearanceTurn * 1.18f, 0);
                    Vector3 elbow = pivot + height * bend;
                    float gap = ElbowGap(shoulder, elbow, wrist, other, handGap, handPoint, best, out _);
                    if (gap > best)
                    {
                        best = gap; arm.elbowPoint = elbow; arm.wristPoint = wrist; arm.tipPoint = wrist + tipOffset;
                    }
                    if (gap >= .002f) return;
                }
                Vector3 outFront = new(arm.side * .64f, .17f, .72f);
                if (hasDesk && worstPoint.y < deskSurface.max.y + .06f && DeskDistance(worstPoint) < .07f)
                    outFront = new Vector3(arm.side * .15f, .90f, -.30f);
                wrist += (normal + outFront).normalized * Mathf.Clamp(-roundBest * 1.6f + .012f, .012f, .085f);
                if (pass % fitPasses == 3 && arm.solved) wrist = lastWrist;
                if (pass == fitPasses - 1 && arm.solved && best < -.0055f)
                {
                    // If a sleeve or hand is already too deep, let it clear the contact
                    // at gesture speed. Keep the best pose from either search.
                    passes += fitPasses;
                    travel = Mathf.Min(.090f, 1.60f * dt);
                    preferredTurn = 4.5f * dt;
                    clearanceTurn = 8.5f * dt;
                    Vector3 away = (normal + new Vector3(arm.side * .70f, .18f, .35f)).normalized;
                    wrist = Vector3.MoveTowards(lastWrist, arm.requested + away * .062f, travel);
                }
            }
        }

        void MeasurePoint(Vector3 point, float radius, ArmRig other, ref float minimum, ref Vector3 worst)
        {
            float gap = Clearance(point, radius, other);
            if (gap < minimum) { minimum = gap; worst = point; }
        }

        float HandGap(ArmRig arm, Vector3 wrist, Vector3 tip, ArmRig other, out Vector3 worst)
        {
            worst = wrist;
            float min = Clearance(wrist, palmRadius, other);
            MeasurePoint(wrist + tip * .4f, palmRadius, other, ref min, ref worst);
            MeasurePoint(wrist + tip * .75f, palmRadius * .78f, other, ref min, ref worst);
            MeasurePoint(wrist + tip, .019f, other, ref min, ref worst);
            MeasurePoint(wrist + arm.thumbOffset * .65f, .023f, other, ref min, ref worst);
            MeasurePoint(wrist + arm.thumbOffset, .018f, other, ref min, ref worst);
            MeasurePoint(wrist + arm.littleOffset, .016f, other, ref min, ref worst);
            return min;
        }

        float ElbowGap(Vector3 shoulder, Vector3 elbow, Vector3 wrist, ArmRig other,
            float min, Vector3 handPoint, float cutoff, out Vector3 worst)
        {
            worst = handPoint;
            if (min <= cutoff) return min;
            MeasurePoint(Vector3.LerpUnclamped(shoulder, elbow, .72f), sleeveRadius * .88f, other, ref min, ref worst);
            for (int i = 0; i < 5 && min > cutoff; i++)
            {
                float t = i * .2f;
                MeasurePoint(Vector3.LerpUnclamped(elbow, wrist, t), Mathf.LerpUnclamped(sleeveRadius, palmRadius, t),
                    other, ref min, ref worst);
            }
            return min;
        }

#if UNITY_EDITOR
        float ArmGap(ArmRig arm, Vector3 shoulder, Vector3 elbow, Vector3 wrist, Vector3 tip, ArmRig other, out Vector3 worst)
        {
            float handGap = HandGap(arm, wrist, tip, other, out Vector3 point);
            return ElbowGap(shoulder, elbow, wrist, other, handGap, point, float.NegativeInfinity, out worst);
        }
#endif
    }
}
