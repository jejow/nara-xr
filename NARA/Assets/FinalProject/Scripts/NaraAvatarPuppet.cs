using System;
using System.Linq;
using UnityEngine;

namespace Nara.Practice
{
    public sealed class NaraAvatarPuppet : MonoBehaviour
    {
        [SerializeField] Transform head, chest, rightUpperArm, rightForearm, rightHand;
        [SerializeField] SkinnedMeshRenderer skin;
        [SerializeField] int blinkLeft, blinkRight, jaw, pucker, smileLeft, smileRight, brow;
        [SerializeField] int triangleCount;
        [SerializeField] Quaternion upperRest, forearmRest, handRest;
        [SerializeField] Quaternion upperRaised, forearmRaised, handRaised;
        Quaternion headRest, chestRest;
        Vector3 headRight, headUp, chestRight, shoulderUp, elbowAxis;
        Vector2 gaze;
        float speakingBlend, mouthBlend, handLift;

        public Transform Head => head;
        public Transform RightWrist => rightHand;
        public int Triangles => triangleCount;

        public void FitToSeat(Transform model)
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
            float height = head.position.y - Mathf.Min(leftToe.position.y, rightToe.position.y);
            if (height < .8f || height > 2.2f)
                throw new InvalidOperationException($"Unexpected avatar scale {height:F2} in {gameObject.name}.");
            model.localScale *= Mathf.Clamp(height, 1.43f, 1.58f) / height;
            Vector3 forward = Vector3.ProjectOnPlane(leftToe.position - leftFoot.position +
                rightToe.position - rightFoot.position, Vector3.up);
            float yaw = Vector3.SignedAngle(forward, Vector3.back, Vector3.up);
            model.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * model.rotation;

            foreach (string side in new[] { "L", "R" })
            {
                Transform thigh = Find(side + " Thigh"), knee = Find(side + " Calf");
                Transform foot = Find(side + " Foot"), toe = Find(side + " Toe0");
                float sideSign = Mathf.Sign(thigh.position.x - pelvis.position.x);
                Aim(thigh, knee, new Vector3(.10f * sideSign, -.11f, -1));
                Aim(knee, foot, new Vector3(.04f * sideSign, -1, -.10f));
                Aim(foot, toe, new Vector3(.15f * sideSign, -.62f, -1));
                Transform arm = Find(side + " UpperArm"), elbow = Find(side + " Forearm");
                Transform hand = Find(side + " Hand");
                Aim(arm, elbow, new Vector3(.25f * sideSign, -1, -.24f));
                Aim(elbow, hand, new Vector3(-.30f * sideSign, -.12f, -1));
            }

            model.position += new Vector3(0, .553f, .070f) - pelvis.position;
            float feet = Mathf.Min(leftToe.position.y, rightToe.position.y);
            model.position += Vector3.up * Mathf.Clamp(.049f - feet, -.028f, .046f);
            FitRaisedArm(pelvis, Find("R Finger1"), Find("R Finger2"), Find("R Finger4"));

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

            Vector3 fingers = middle.position - rightHand.position;
            Vector3 across = index.position - little.position;
            Quaternion currentPalm = Quaternion.LookRotation(fingers, Vector3.Cross(fingers, across));
            Vector3 raisedFingers = new(.045f * side, 1, -.10f);
            Vector3 raisedAcross = -side * Vector3.right;
            Quaternion openPalm = Quaternion.LookRotation(raisedFingers, Vector3.Cross(raisedFingers, raisedAcross));
            rightHand.rotation = openPalm * Quaternion.Inverse(currentPalm) * rightHand.rotation;

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

        void Awake()
        {
            headRest = head.localRotation;
            chestRest = chest.localRotation;
            headRight = head.parent.InverseTransformDirection(transform.right);
            headUp = head.parent.InverseTransformDirection(transform.up);
            chestRight = chest.parent.InverseTransformDirection(transform.right);
            shoulderUp = rightUpperArm.parent.InverseTransformDirection(transform.up);
            elbowAxis = rightForearm.parent.InverseTransformDirection(transform.right);
        }

        public void Animate(float time, float elapsed, bool listening, float volume, bool talking,
            Vector3 presenterEyes, bool raisesHand = false, float nod = 0)
        {
            float breath = Mathf.Sin(time * 1.68f);
            float attention = listening ? Mathf.Pow(Mathf.Max(0, Mathf.Sin(.48f * time + .025f * elapsed)), 13) : 0;
            speakingBlend = Mathf.MoveTowards(speakingBlend, talking ? 1 : 0, Time.unscaledDeltaTime * 2.9f);
            mouthBlend = Mathf.MoveTowards(mouthBlend, talking ? volume : 0, Time.unscaledDeltaTime * 12);
            float gesture = speakingBlend * (.25f + .75f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(time * 2.02f)), 3));
            chest.localRotation = Quaternion.AngleAxis(.75f * breath + 1.1f * gesture, chestRight) * chestRest;
            Vector3 eyeLine = presenterEyes - head.position;
            Vector3 horizontal = Vector3.ProjectOnPlane(eyeLine, Vector3.up);
            var target = new Vector2(
                Mathf.Clamp(Mathf.Atan2(eyeLine.y, horizontal.magnitude) * Mathf.Rad2Deg, -12, 16),
                Mathf.Clamp(Vector3.SignedAngle(-transform.forward, horizontal, Vector3.up), -23, 23));
            gaze = Vector2.Lerp(gaze, target, 1 - Mathf.Exp(-5 * Time.unscaledDeltaTime));
            head.localRotation = Quaternion.AngleAxis(gaze.y + 2.3f * Mathf.Sin(time * .53f), headUp) *
                Quaternion.AngleAxis(gaze.x + .76f * breath - 5.1f * attention - 7.5f * nod + gesture * 1.5f, headRight) * headRest;
            handLift = Mathf.MoveTowards(handLift, raisesHand ? 1 : 0, Time.unscaledDeltaTime * (raisesHand ? 1.65f : 2.55f));
            PoseRightArm(Mathf.SmoothStep(0, 1, handLift), gesture);

            float blinkTime = Mathf.Repeat(time, 5.6f);
            float blink = blinkTime < .187f ? 100 * Mathf.Sin(blinkTime * Mathf.PI / .187f) : 0;
            Face(blinkLeft, blink);
            Face(blinkRight, blink);
            Face(jaw, 57 * mouthBlend);
            Face(pucker, 9 * mouthBlend * (1 + Mathf.Sin(time * 12)));
            Face(smileLeft, 6 * attention);
            Face(smileRight, 6 * attention);
            Face(brow, 13 * gesture);
        }

        public void PoseRightArm(float lift, float gesture = 0)
        {
            Quaternion upper = upperRest, forearm = forearmRest;
            if (gesture > 0)
            {
                upper = Quaternion.AngleAxis(gesture * 6.5f, shoulderUp) * upper;
                forearm = Quaternion.AngleAxis(-gesture * 12f, elbowAxis) * forearm;
            }
            rightUpperArm.localRotation = Quaternion.Slerp(upper, upperRaised, lift);
            rightForearm.localRotation = Quaternion.Slerp(forearm, forearmRaised, lift);
            rightHand.localRotation = Quaternion.Slerp(handRest, handRaised, lift);
        }

        void Face(int index, float value)
        {
            if (index >= 0)
                skin.SetBlendShapeWeight(index, value);
        }
    }
}
