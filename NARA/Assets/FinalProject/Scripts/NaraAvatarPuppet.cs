using System;
using System.Linq;
using UnityEngine;

namespace Nara.Practice
{
    public sealed partial class NaraAvatarPuppet : MonoBehaviour
    {
        [SerializeField] Transform head, chest, rightUpperArm, rightForearm, rightHand;
        [SerializeField] Transform leftUpperArm, leftForearm, leftHand;
        [SerializeField] SkinnedMeshRenderer skin;
        [SerializeField] int blinkLeft, blinkRight, jaw, pucker, smileLeft, smileRight, brow;
        [SerializeField] int funnel = -1, mouthClose = -1;
        [SerializeField] int triangleCount;
        [SerializeField] Quaternion upperRest, forearmRest, handRest;
        [SerializeField] Quaternion upperRaised, forearmRaised, handRaised;
        Quaternion headRest, chestRest;
        // Preserve the authored standing coverage and leave room on both sides for
        // either wrist to travel beyond the outline of the seated mesh.
        const float AnimationBoundsPadding = .40f;
        Vector3 headRight, headUp, headForward, chestRight, chestForward, chestUp;
        Vector2 gaze, gazeVelocity;
        float bodyTurn, bodyTurnVelocity, focusBlend, countBlend;
        float seed = 1, nextBlink, blinkStart = -10;
        float speakingBlend, mouthBlend, roundedBlend, handLift;
        bool doubleBlink;

        public Transform Head => head;
        public Transform RightWrist => rightHand;
        internal Transform LeftWrist => leftHand;
        public int Triangles => triangleCount;

        public void FitToSeat(Transform model, int profile)
        {
            Transform[] skeleton = model.GetComponentsInChildren<Transform>(true);
            Transform Find(string name) => skeleton.FirstOrDefault(t => t.name == "Bip01 " + name) ??
                throw new InvalidOperationException($"Avatar {name} joint is missing in {gameObject.name}.");

            Transform pelvis = Find("Pelvis");
            Transform leftFoot = Find("L Foot"), rightFoot = Find("R Foot");
            Transform leftToe = Find("L Toe0"), rightToe = Find("R Toe0");
            head = Find("Head");
            chest = Find("Spine2");
            rightUpperArm = Find("R UpperArm");
            rightForearm = Find("R Forearm");
            rightHand = Find("R Hand");
            leftUpperArm = Find("L UpperArm");
            leftForearm = Find("L Forearm");
            leftHand = Find("L Hand");
            float height = head.position.y - Mathf.Min(leftToe.position.y, rightToe.position.y);
            if (height < .8f || height > 2.2f)
                throw new InvalidOperationException($"Unexpected avatar scale {height:F2} in {gameObject.name}.");
            model.localScale *= Mathf.Clamp(height, 1.43f, 1.58f) / height;
            Vector3 forward = Vector3.ProjectOnPlane(leftToe.position - leftFoot.position +
                rightToe.position - rightFoot.position, Vector3.up);
            float yaw = Vector3.SignedAngle(forward, Vector3.back, Vector3.up);
            model.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * model.rotation;
            float posture = Mathf.Repeat(profile * .618034f, 1f) - .5f;
            chest.rotation = Quaternion.AngleAxis(10f * posture, Vector3.up) *
                Quaternion.AngleAxis(5.4f * posture, Vector3.right) *
                Quaternion.AngleAxis(5.0f * posture, Vector3.back) * chest.rotation;

            foreach (string side in new[] { "L", "R" })
            {
                Transform thigh = Find(side + " Thigh"), knee = Find(side + " Calf");
                Transform foot = Find(side + " Foot"), toe = Find(side + " Toe0");
                float sideSign = Mathf.Sign(thigh.position.x - pelvis.position.x);
                float stagger = Mathf.Repeat(profile * .287f, 1f) - .5f;
                Aim(thigh, knee, new Vector3((.12f + .14f * stagger) * sideSign, -.11f + .05f * posture * sideSign, -1));
                Aim(knee, foot, new Vector3(.05f * sideSign, -1, -.10f + .25f * stagger * sideSign));
                Aim(foot, toe, new Vector3(.15f * sideSign, -.62f, -1));
                Transform arm = Find(side + " UpperArm"), elbow = Find(side + " Forearm");
                Transform hand = Find(side + " Hand");
                float handPosition = .57f + .34f * posture * sideSign + .24f * stagger;
                float handAngle = profile % 4 == 1 ? -.49f : -.13f + .26f * posture;
                Vector3 lap = Vector3.Lerp(thigh.position, knee.position, handPosition) +
                    new Vector3(handAngle * .078f * sideSign, .077f, .012f);
                ReachArm(arm, elbow, hand, lap, new Vector3(sideSign * .65f, -.86f, .20f));
                OrientPalm(hand, Find(side + " Finger1"), Find(side + " Finger2"), Find(side + " Finger4"),
                    new Vector3(handAngle * sideSign, -.09f, -1), -sideSign * Vector3.right);
            }

            model.position += new Vector3(0, .553f, .070f) - pelvis.position;
            float feet = Mathf.Min(leftToe.position.y, rightToe.position.y);
            model.position += Vector3.up * Mathf.Clamp(.049f - feet, -.028f, .046f);
            FitRaisedArm(pelvis, Find("R Finger1"), Find("R Finger2"), Find("R Finger4"));
#if UNITY_EDITOR
            FitContactSurfaces(model, Find);
#endif

            skin = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(renderer => renderer.sharedMesh && renderer.sharedMesh.blendShapeCount > 0);
            if (!skin)
                throw new InvalidOperationException("No rigged expression mesh in " + gameObject.name);
            Mesh mesh = skin.sharedMesh;
            int Shape(string name) => Enumerable.Range(0, mesh.blendShapeCount)
                .Where(i => mesh.GetBlendShapeName(i).EndsWith(name, StringComparison.OrdinalIgnoreCase))
                .DefaultIfEmpty(-1).First();
            blinkLeft = Shape("EyeBlinkLeft");
            blinkRight = Shape("EyeBlinkRight");
            jaw = Shape("JawOpen");
            pucker = Shape("MouthPucker");
            funnel = Shape("MouthFunnel");
            mouthClose = Shape("MouthClose");
            smileLeft = Shape("MouthSmileLeft");
            smileRight = Shape("MouthSmileRight");
            brow = Shape("BrowInnerUp");
            if (blinkLeft < 0 || blinkRight < 0 || jaw < 0)
                throw new InvalidOperationException("Missing blink or mouth controls in " + gameObject.name);
            triangleCount = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(renderer =>
                Enumerable.Range(0, renderer.sharedMesh.subMeshCount)
                    .Sum(i => (int)renderer.sharedMesh.GetIndexCount(i) / 3));
        }

        void FitRaisedArm(Transform pelvis, Transform index, Transform middle, Transform little)
        {
            upperRest = rightUpperArm.localRotation;
            forearmRest = rightForearm.localRotation;
            handRest = rightHand.localRotation;
            float side = Mathf.Sign(rightUpperArm.position.x - pelvis.position.x);
            Aim(rightUpperArm, rightForearm, new Vector3(.84f * side, .13f, -.38f));
            Aim(rightForearm, rightHand, new Vector3(.10f * side, .98f, -.13f));

            OrientPalm(rightHand, index, middle, little, new Vector3(.045f * side, 1, -.10f), -side * Vector3.right);

            upperRaised = rightUpperArm.localRotation;
            forearmRaised = rightForearm.localRotation;
            handRaised = rightHand.localRotation;
            PoseRightArm(0);
        }

        static void Aim(Transform link, Transform child, Vector3 targetDirection)
        {
            Vector3 direction = child.position - link.position;
            link.rotation = Quaternion.FromToRotation(direction, targetDirection) * link.rotation;
        }

        static void ReachArm(Transform shoulder, Transform elbow, Transform wrist, Vector3 target, Vector3 pole)
        {
            float upper = Vector3.Distance(shoulder.position, elbow.position);
            float lower = Vector3.Distance(elbow.position, wrist.position);
            Vector3 offset = target - shoulder.position;
            Vector3 line = offset.normalized;
            float reach = Mathf.Clamp(offset.magnitude, Mathf.Abs(upper - lower) + .001f, upper + lower - .001f);
            float along = (upper * upper - lower * lower + reach * reach) / (2 * reach);
            float bend = Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
            Vector3 bendDirection = Vector3.ProjectOnPlane(pole, line).normalized;
            Aim(shoulder, elbow, line * along + bendDirection * bend);
            Aim(elbow, wrist, shoulder.position + line * reach - elbow.position);
        }

        static void OrientPalm(Transform hand, Transform index, Transform middle, Transform little,
            Vector3 fingersDirection, Vector3 handWidth)
        {
            Vector3 fingers = middle.position - hand.position;
            Vector3 across = index.position - little.position;
            Quaternion current = Quaternion.LookRotation(fingers, Vector3.Cross(fingers, across));
            Quaternion desired = Quaternion.LookRotation(fingersDirection, Vector3.Cross(fingersDirection, handWidth));
            hand.rotation = desired * Quaternion.Inverse(current) * hand.rotation;
        }

        void Awake() => InitialiseRig();

        void InitialiseRig()
        {
            if (rightArm != null || !head) return;
            headRest = head.localRotation;
            chestRest = chest.localRotation;
            headRight = head.parent.InverseTransformDirection(transform.right);
            headUp = head.parent.InverseTransformDirection(transform.up);
            headForward = head.parent.InverseTransformDirection(-transform.forward);
            chestRight = chest.parent.InverseTransformDirection(transform.right);
            chestForward = chest.parent.InverseTransformDirection(-transform.forward);
            chestUp = chest.parent.InverseTransformDirection(transform.up);
            BindHands();
            BindArmContacts();
            foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Bounds bounds = renderer.localBounds;
                Vector3 localMargin = new(
                    AnimationBoundsPadding / Mathf.Max(.0001f, renderer.transform.TransformVector(Vector3.right).magnitude),
                    AnimationBoundsPadding / Mathf.Max(.0001f, renderer.transform.TransformVector(Vector3.up).magnitude),
                    AnimationBoundsPadding / Mathf.Max(.0001f, renderer.transform.TransformVector(Vector3.forward).magnitude));
                bounds.Expand(localMargin * 2);
                renderer.localBounds = bounds;
            }
        }

        public void SetMotionProfile(int profile, float time, int seat = 0, int round = 0)
        {
            InitialiseRig();
            seed = 2.3f + profile * 3.89f;
            nextBlink = time + .8f + 4f * Mathf.Repeat(seed * .31f, 1);
            blinkStart = time - 2;
            doubleBlink = false;
            speakingBlend = mouthBlend = 0;
            bodyTurn = bodyTurnVelocity = focusBlend = countBlend = 0;
            gaze = gazeVelocity = Vector2.zero;
            StartMovement(profile, seat, round, time);
            SelectFloorMotion(profile, seat, round);
        }

        float Motion(float time, float channel) => Mathf.Clamp01(Mathf.PerlinNoise(seed + channel * 5.37f, time));

        public void Animate(float time, float dt, bool listening, float volume, bool talking, Vector3 attentionTarget,
            bool raisesHand = false, float nod = 0, float articulation = .5f, float attention = 0,
            AudienceLine line = AudienceLine.Notice, float lineProgress = 0)
        {
            float breath = Mathf.Sin(time * (1.32f + .58f * Mathf.Repeat(seed, 1)) + seed);
            float shift = 2 * Motion(.27f * time, 1) - 1;
            float lean = 2 * Motion(.19f * time, 2) - 1;
            speakingBlend = Mathf.MoveTowards(speakingBlend, talking ? 1 : 0, dt * 3.4f);
            mouthBlend = Mathf.Lerp(mouthBlend, talking ? volume : 0, 1 - Mathf.Exp(-(volume > mouthBlend ? 27 : 17) * dt));
            roundedBlend = Mathf.Lerp(roundedBlend, articulation, 1 - Mathf.Exp(-13 * dt));
            AdvanceFloor(raisesHand, dt);
            focusBlend = Mathf.Lerp(focusBlend, attention, 1 - Mathf.Exp(-6 * dt));
            SeatedPose pose = SampleMovement(time, talking, focusBlend, line, lineProgress);
            Vector3 eyeLine = attentionTarget - head.position;
            Vector3 horizontal = Vector3.ProjectOnPlane(eyeLine, Vector3.up);
            float toward = Vector3.SignedAngle(-transform.forward, horizontal, Vector3.up);
            float turnTarget = Mathf.Clamp(toward * (.11f + .16f * focusBlend), -18, 18) + pose.body.y;
            bodyTurn = Mathf.SmoothDampAngle(bodyTurn, turnTarget, ref bodyTurnVelocity, .28f, 82f, dt);
            chest.localRotation = Quaternion.AngleAxis(bodyTurn, chestUp) *
                Quaternion.AngleAxis(2.4f * shift + pose.body.z, chestForward) *
                Quaternion.AngleAxis(1.6f * lean + .95f * breath + pose.body.x, chestRight) * chestRest;
            float listeningNod = listening ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.69f, .85f, Motion(time * .88f, 7))) : 0;
            var target = new Vector2(
                Mathf.Clamp(Mathf.Atan2(eyeLine.y, horizontal.magnitude) * Mathf.Rad2Deg + pose.look.x - pose.body.x * .55f, -21, 21),
                Mathf.Clamp(Mathf.DeltaAngle(bodyTurn, toward) + pose.look.y, -52, 52));
            gaze = Vector2.SmoothDamp(gaze, target, ref gazeVelocity, talking ? .14f : .22f, 135, dt);
            head.localRotation = Quaternion.AngleAxis(gaze.y, headUp) *
                Quaternion.AngleAxis(gaze.x + .64f * breath - 4.5f * listeningNod - 8f * nod, headRight) *
                Quaternion.AngleAxis(2.8f * shift + pose.look.z, headForward) * headRest;
            AnimateHands(pose, time, dt);

            if (time >= nextBlink)
            {
                blinkStart = time;
                doubleBlink = !doubleBlink && Motion(time * 1.11f, 10) > .69f;
                nextBlink = time + (doubleBlink ? .28f : 2.1f + 4.6f * Motion(time, 11));
            }
            float blinkPhase = Mathf.Clamp01((time - blinkStart) / .163f);
            float blink = 100 * Mathf.Sin(Mathf.PI * blinkPhase);
            Face(blinkLeft, blink);
            Face(blinkRight, blink);
            Face(jaw, 62f * mouthBlend * (1 - .23f * roundedBlend));
            Face(pucker, 26f * roundedBlend * mouthBlend);
            Face(funnel, 16f * roundedBlend * mouthBlend);
            Face(mouthClose, 22f * speakingBlend * Mathf.InverseLerp(.25f, .03f, mouthBlend));
            Face(smileLeft, 10f * listeningNod + pose.smile);
            Face(smileRight, 8f * listeningNod + pose.smile * .82f);
            Face(brow, pose.brow + 9f * nod + 8f * handLift);
        }

        public void PoseRightArm(float lift)
        {
            rightUpperArm.localRotation = Quaternion.Slerp(upperRest, upperRaised, lift);
            rightForearm.localRotation = Quaternion.Slerp(forearmRest, forearmRaised, lift);
            rightHand.localRotation = Quaternion.Slerp(handRest, handRaised, lift);
        }

        void Face(int index, float value)
        {
            if (index >= 0)
                skin.SetBlendShapeWeight(index, Mathf.Clamp(value, 0, 100));
        }
    }
}
