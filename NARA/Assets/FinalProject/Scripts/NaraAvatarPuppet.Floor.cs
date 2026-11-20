using UnityEngine;

namespace Nara.Practice
{
    public sealed partial class NaraAvatarPuppet
    {
        enum FloorStage { Rest, Raising, Holding, Lowering }

        UpperBodyMotion floorMotion;
        FloorStage floorStage;
        UpperBodyPose floorPose, floorTransition;
        float floorElapsed, floorInitialWeight, lowerDuration, reverseFrom;
        bool raisedRight, reverseIntro, generatedRaise;

        public Transform FloorWrist => raisedRight ? rightHand : leftHand;
        internal bool RequestHandHeld => floorStage == FloorStage.Holding;
        internal bool RequestHandAtRest => floorStage == FloorStage.Rest;
#if UNITY_EDITOR
        public static int AuditLocalRaiseCount => UpperBodyMotion.LocalRaises.Length;
        public string AuditFloorTake { get; private set; }
        public bool AuditFloorHeld => RequestHandHeld;
        public bool AuditFloorAtRest => RequestHandAtRest;
#endif

        void SelectFloorMotion(int profile, int seat, int round)
        {
            UpperBodyMotion[] local = UpperBodyMotion.LocalRaises;
            int choice = (profile + 11 * seat + 7 * round) % (10 + local.Length * 2);
            generatedRaise = choice >= 10;
            if (generatedRaise)
            {
                int index = (choice - 10) / 2;
                raisedRight = choice % 2 == 0;
                floorMotion = local[index];
#if UNITY_EDITOR
                AuditFloorTake = $"HY{index + 1}{(raisedRight ? "Right" : "Left")}";
#endif
            }
            else
            {
                var take = choice switch
                {
                    0 or 1 => OverteTake.RaiseRight01,
                    2 or 3 => OverteTake.RaiseLeft03,
                    _ => OverteTake.RaiseRight02
                };
                raisedRight = take != OverteTake.RaiseLeft03;
                floorMotion = UpperBodyMotion.Get(take);
#if UNITY_EDITOR
                AuditFloorTake = take.ToString();
#endif
            }
            floorStage = FloorStage.Rest;
            floorElapsed = handLift = 0;
            floorPose = floorMotion.Sample(0);
        }

        void AdvanceFloor(bool requested, float dt)
        {
            float raiseDuration = Mathf.Clamp(floorMotion.HoldStart, .46f, generatedRaise ? .78f : .62f);
            if (requested)
            {
                if (floorStage is FloorStage.Rest or FloorStage.Lowering)
                {
                    floorStage = FloorStage.Raising;
                    floorTransition = floorPose;
                    floorInitialWeight = handLift;
                    floorElapsed = 0;
                }
                floorElapsed += dt;
                if (floorElapsed < raiseDuration)
                {
                    float progress = floorElapsed / raiseDuration;
                    floorPose = UpperBodyPose.Blend(floorTransition, floorMotion.Sample(progress * floorMotion.HoldStart),
                        Ease(0, .33f, progress));
                    handLift = Mathf.Lerp(floorInitialWeight, 1, Ease(0, .76f, progress));
                }
                else
                {
                    floorStage = FloorStage.Holding;
                    floorPose = floorMotion.Loop(floorElapsed - raiseDuration, floorMotion.HoldStart, floorMotion.LowerStart);
                    handLift = 1;
                }
                return;
            }

            if (floorStage == FloorStage.Rest) return;
            if (floorStage != FloorStage.Lowering)
            {
                // A quick invitation can arrive before the wrist reaches the hold.
                // Retrace the part of the introduction already played in that case.
                reverseIntro = floorStage == FloorStage.Raising;
                reverseFrom = floorMotion.HoldStart * Mathf.Clamp01(floorElapsed / raiseDuration);
                lowerDuration = reverseIntro ? Mathf.Lerp(.14f, .34f, floorElapsed / raiseDuration) : .43f;
                floorTransition = floorPose;
                floorInitialWeight = handLift;
                floorStage = FloorStage.Lowering;
                floorElapsed = 0;
            }
            floorElapsed += dt;
            float release = Mathf.Clamp01(floorElapsed / lowerDuration);
            if (release >= 1)
            {
                floorStage = FloorStage.Rest;
                floorPose = floorMotion.Sample(0);
                handLift = 0;
                return;
            }
            float clipTime = reverseIntro ? reverseFrom * (1 - release) :
                Mathf.Lerp(floorMotion.LowerStart, floorMotion.Duration, release);
            floorPose = UpperBodyPose.Blend(floorTransition, floorMotion.Sample(clipTime), Ease(0, .42f, release));
            handLift = floorInitialWeight * (1 - Ease(reverseIntro ? 0 : .61f, 1, release));
        }

        float FloorLift(ArmRig arm) => (arm == rightArm) == raisedRight ? handLift : 0;

        Vector3 FloorTorso()
        {
            Vector3 torso = floorPose.body.torso;
            return generatedRaise && !raisedRight ? new Vector3(torso.x, -torso.y, -torso.z) : torso;
        }

        void ApplyFloorPose(ArmRig arm, Vector3 shoulder, ref Vector3 target, ref Vector3 pole,
            ref Vector3 fingers, ref Vector3 across, float weight)
        {
            RecordedArm source = raisedRight || generatedRaise ? floorPose.body.right : floorPose.body.left;
            // Keep the centre of the raised hand outside the shoulder line.
            // Wrist travel and the bend still follow the recorded performance.
            float raised = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.32f, .48f, source.wrist.y));
            float side = Mathf.Lerp(source.wrist.x, Mathf.Clamp(source.wrist.x, .32f, .63f), raised);
            float height = Mathf.Lerp(source.wrist.y, Mathf.Clamp(source.wrist.y, .49f, .72f), raised);
            float depth = Mathf.Lerp(source.wrist.z, Mathf.Clamp(source.wrist.z, .10f, .36f), raised);
            Vector3 lifted = shoulder + ArmAxes(new Vector3(side, height, depth), arm.side) * arm.Length;
            lifted.z += .087f * Mathf.Sin(raised * Mathf.PI);
            Vector3 elbowDirection = ArmAxes(source.elbow, arm.side).normalized;
            Vector3 outward = new(arm.side, -.22f, .09f);
            Vector3 floorPole = Vector3.Slerp(outward.normalized, elbowDirection, .45f);
            Vector3 fingerDirection = ArmAxes(source.fingers, arm.side);
            Vector3 handWidth = ArmAxes(source.across, arm.side);
            target = Vector3.Lerp(target, lifted, weight);
            pole = Vector3.Lerp(pole, floorPole, raised * weight);
            fingers = Vector3.Slerp(fingers, fingerDirection, weight).normalized;
            across = Vector3.Slerp(across, handWidth, weight).normalized;
        }
    }
}
