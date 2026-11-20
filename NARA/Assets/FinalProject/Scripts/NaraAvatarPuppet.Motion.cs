using UnityEngine;

namespace Nara.Practice
{
    public enum AudienceLine { Notice, Greeting, FirstQuestion, SecondQuestion, FollowUp, Feedback }

    public sealed partial class NaraAvatarPuppet
    {
        internal enum ListeningMotion
        {
            LeanIn, SettleBack, FoldArms, Think, AdjustCuff, ClaspHands, GlanceAside, DoubleNod, RollShoulder, ShiftWeight
        }
        internal enum SpeakingMotion { OfferPalm, ShowWidth, CompareSides, CountPoint, HandToChest }
        internal ListeningMotion ListeningAction { get; private set; }
        internal SpeakingMotion SpeakingAction { get; private set; }

        struct FingerPose { public Transform joint; public Quaternion rest; public Vector3 axis; }
        struct SeatedPose { public Vector3 body, look; public float idle, speech, smile, brow; }

        static readonly OverteTake[] SettleTakes = { OverteTake.Fidget, OverteTake.LookAndSettle };
        static readonly OverteTake[] GlanceTakes = { OverteTake.Look, OverteTake.LookBrief };
        static readonly OverteTake[] NodTakes =
            { OverteTake.Nod, OverteTake.Acknowledge, OverteTake.NodYes, OverteTake.SlowNod };
        static readonly OverteTake[] ShiftTakes = { OverteTake.Shift, OverteTake.ShiftSeat };
        static readonly OverteTake[] SpeechTakes =
            { OverteTake.Talk02, OverteTake.Talk03, OverteTake.Talk04, OverteTake.PointBrief };

        readonly int[] listeningDeck = new int[10];
        System.Random movementRandom;
        int listenIndex;
        float idleStart, idleDuration, startOffset, playbackRate, energy;
        bool primaryRight;
        FingerPose[] rightFingers, leftFingers;
        SeatedMotionBank recording;
        RecordedSeatPose sampled, previous, quiet;
        UpperBodyMotion reactionMotion, talkMotion;
        OverteTake reactionTake, talkTake;
        UpperBodyPose reactionPose, talkPose, talkTransition;
        float reactionWeight, speechStart, previousProgress, talkOffset, talkRate;
        bool wasTalking;
        AudienceLine previousLine;
        int talkIndex;

#if UNITY_EDITOR
        public string AuditOverteReaction => reactionWeight > .10f ? reactionTake.ToString() : "";
        public string AuditOverteTalk => wasTalking && speakingBlend > .45f ? talkTake.ToString() : "";
#endif

        void BindHands()
        {
            rightFingers = BindFingers(rightHand, "R");
            leftFingers = BindFingers(leftHand, "L");
        }

        FingerPose[] BindFingers(Transform wrist, string side)
        {
            var joints = new FingerPose[8];
            for (int digit = 1; digit <= 4; digit++)
            {
                Transform joint = wrist.Find("Bip01 " + side + " Finger" + digit);
                for (int link = 0; link < 2 && joint && joint.childCount > 0; link++)
                {
                    Vector3 along = joint.GetChild(0).position - joint.position;
                    Vector3 curl = Vector3.Cross(along, -transform.up).normalized;
                    joints[(digit - 1) * 2 + link] = new FingerPose
                        { joint = joint, rest = joint.localRotation, axis = joint.InverseTransformDirection(curl) };
                    joint = joint.GetChild(0);
                }
            }
            return joints;
        }

        float Range(float lo, float hi) => Mathf.Lerp(lo, hi, (float)movementRandom.NextDouble());
        static float Ease(float lo, float hi, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(lo, hi, t));

        void StartMovement(int profile, int seat, int round, float now)
        {
            movementRandom = new System.Random(137 + profile * 6719 + seat * 179 + round * 313);
            primaryRight = movementRandom.NextDouble() > .31;
            energy = Range(.85f, 1.05f);
            listenIndex = -1;
            talkIndex = (profile + seat + round) % SpeechTakes.Length;
            wasTalking = false;
            talkPose = default;
            reactionWeight = 0;
            rightArm.ready = leftArm.ready = rightArm.solved = leftArm.solved = false;
            quiet = SeatedMotionBank.Get(hasDesk, ListeningMotion.ClaspHands).Sample(Range(1, 6));
            PickIdle(now);
            idleStart -= Range(.03f, .90f) * idleDuration;
            previous = sampled = recording.Sample(startOffset + (now - idleStart) * playbackRate);
        }

        void PickIdle(float now)
        {
            if (listenIndex < 0 || listenIndex == listeningDeck.Length)
            {
                int last = listenIndex < 0 ? -1 : listeningDeck[^1];
                for (int i = 0; i < listeningDeck.Length; i++) listeningDeck[i] = i;
                for (int i = listeningDeck.Length - 1; i > 0; i--)
                {
                    int j = movementRandom.Next(i + 1);
                    (listeningDeck[i], listeningDeck[j]) = (listeningDeck[j], listeningDeck[i]);
                }
                if (listeningDeck[0] == last)
                    (listeningDeck[0], listeningDeck[1]) = (listeningDeck[1], listeningDeck[0]);
                listenIndex = 0;
            }
            previous = sampled;
            ListeningAction = (ListeningMotion)listeningDeck[listenIndex++];
            recording = SeatedMotionBank.Get(hasDesk, ListeningAction);
            OverteTake? reaction = ListeningAction switch
            {
                ListeningMotion.LeanIn => OverteTake.Lean,
                ListeningMotion.SettleBack => SettleTakes[movementRandom.Next(SettleTakes.Length)],
                ListeningMotion.GlanceAside => GlanceTakes[movementRandom.Next(GlanceTakes.Length)],
                ListeningMotion.DoubleNod => NodTakes[movementRandom.Next(NodTakes.Length)],
                ListeningMotion.ShiftWeight => ShiftTakes[movementRandom.Next(ShiftTakes.Length)],
                _ => null
            };
            reactionMotion = null;
            if (reaction.HasValue)
            {
                reactionTake = reaction.Value;
                reactionMotion = UpperBodyMotion.Get(reactionTake);
            }
            playbackRate = Range(.91f, 1.04f);
            float segment = Mathf.Min(recording.Duration, Range(5.2f, 8.1f));
            startOffset = Range(0, Mathf.Max(0, recording.Duration - segment));
            idleDuration = segment / playbackRate;
            idleStart = now;
        }

        SeatedPose SampleMovement(float time, bool talking, float attention, AudienceLine line, float progress)
        {
            if (time >= idleStart + idleDuration) PickIdle(time);
            float t = Mathf.Max(0, time - idleStart);
            sampled = RecordedSeatPose.Blend(previous, recording.Sample(startOffset + t * playbackRate), Ease(0, .85f, t));
            float fraction = t / idleDuration;
            float idle = Ease(0, .25f, fraction) * (1 - Ease(.70f, 1, fraction));
            var pose = new SeatedPose { idle = idle * (1 - speakingBlend) * (1 - .48f * attention) * (1 - .90f * handLift) };
            // Source angles are measured in the capture's side/up/forward basis.
            pose.body = new Vector3(-Mathf.Clamp(sampled.torso.x * .48f, -11, 15),
                Mathf.Clamp(-rightArm.side * sampled.torso.y * .64f, -15, 15),
                Mathf.Clamp(rightArm.side * sampled.torso.z * .60f, -9, 9)) * (1 - .55f * speakingBlend);
            if (ListeningAction == ListeningMotion.RollShoulder)
                pose.look.z = 5 * Mathf.Sin(2 * Mathf.PI * fraction) * pose.idle;
            AddRecordedReaction(ref pose, t);
            SampleRecordedSpeech(time, talking, line, progress);
            pose.look += RecordedLook(talkPose.head) * (.37f * speakingBlend);
            pose.body += RecordedBody(FloorTorso()) * (.24f * handLift);

            if (talking)
                SpeakingAction = line switch
                {
                    AudienceLine.FirstQuestion when progress >= .51f => SpeakingMotion.CompareSides,
                    AudienceLine.SecondQuestion => SpeakingMotion.CountPoint,
                    AudienceLine.FollowUp => SpeakingMotion.ShowWidth,
                    AudienceLine.Feedback => SpeakingMotion.HandToChest,
                    _ => SpeakingMotion.OfferPalm
                };
            // The question changes emphasis between clauses with the hand at rest.
            float beat = line == AudienceLine.FirstQuestion
                ? progress < .51f ? progress / .51f : (progress - .51f) / .49f
                : progress;
            pose.speech = speakingBlend * Ease(.045f, .28f, beat) * (1 - Ease(.65f, .96f, beat));
            pose.brow = pose.speech * 15f;
            pose.smile = (line == AudienceLine.Greeting || line == AudienceLine.Feedback ? 15f : 5f) * pose.speech;
            return pose;
        }

        void AddRecordedReaction(ref SeatedPose pose, float time)
        {
            reactionWeight = 0;
            if (reactionMotion == null) return;
            bool nod = ListeningAction == ListeningMotion.DoubleNod;
            float delay = idleDuration * (nod ? .33f : .08f);
            float elapsed = Mathf.Max(0, time - delay);
            float speed = nod ? 1 : Mathf.Max(1, reactionMotion.Duration / (idleDuration * .84f));
            float sampleTime = elapsed * speed;
            reactionWeight = pose.idle * Ease(0, .30f, elapsed);
            if (nod) reactionWeight *= 1 - Ease(reactionMotion.Duration - .35f, reactionMotion.Duration, sampleTime);
            reactionPose = reactionMotion.Sample(sampleTime);
            pose.body += RecordedBody(reactionPose.body.torso) * ((hasDesk ? .22f : .46f) * reactionWeight);
            pose.look += RecordedLook(reactionPose.head) * (.75f * reactionWeight);
        }

        Vector3 RecordedBody(Vector3 value) => new(-Mathf.Clamp(value.x, -12, 12),
            -rightArm.side * Mathf.Clamp(value.y, -14, 14), rightArm.side * Mathf.Clamp(value.z, -9, 9));

        Vector3 RecordedLook(Vector3 value) => new(Mathf.Clamp(value.x, -15, 15),
            -rightArm.side * Mathf.Clamp(value.y, -18, 18), rightArm.side * Mathf.Clamp(value.z, -8, 8));

        void SampleRecordedSpeech(float time, bool talking, AudienceLine line, float progress)
        {
            if (talking)
            {
                if (!wasTalking || previousLine != line || progress < previousProgress - .2f)
                {
                    talkIndex = (talkIndex + 1 + movementRandom.Next(SpeechTakes.Length - 1)) % SpeechTakes.Length;
                    talkTake = SpeechTakes[talkIndex];
                    talkMotion = UpperBodyMotion.Get(talkTake);
                    talkOffset = Mathf.Min(talkMotion.Duration * .18f,
                        talkTake == OverteTake.PointBrief ? .10f : Range(.50f, .82f));
                    talkRate = Range(.94f, 1.08f);
                    talkTransition = wasTalking ? talkPose : talkMotion.Sample(talkOffset);
                    speechStart = time;
                }
                float elapsed = time - speechStart;
                float sampleTime = elapsed * talkRate;
                // A brief emphasis plays once. Longer conversational motion blends across its loop.
                UpperBodyPose next = talkTake == OverteTake.PointBrief
                    ? talkMotion.Sample(talkOffset + sampleTime)
                    : talkMotion.Loop(sampleTime, talkOffset, talkMotion.Duration);
                talkPose = UpperBodyPose.Blend(talkTransition, next, Ease(0, .29f, elapsed));
            }
            wasTalking = talking;
            previousLine = line;
            previousProgress = progress;
        }

        void AnimateHands(SeatedPose pose, float time, float dt)
        {
            UpdateContactFrame();
            float fraction = Mathf.Clamp01((time - idleStart) / idleDuration);
            // The dusting take sweeps across both sleeves. Shape that part around
            // the wearer's cuff and keep the recorded pace on entry and release.
            float cuff = ListeningAction == ListeningMotion.AdjustCuff
                ? .86f * Ease(0, .14f, fraction) * (1 - Ease(.82f, 1, fraction)) : 0;
            float restWeight = Mathf.Max(handLift, Mathf.Max(cuff, speakingBlend));
            var motion = RecordedSeatPose.Blend(sampled, quiet, restWeight);
            if (reactionWeight > 0 && !hasDesk)
                motion = RecordedSeatPose.Blend(motion, reactionPose.body, reactionWeight * .24f);
            if (speakingBlend > 0)
            {
                float lead = speakingBlend * (hasDesk ? .25f : .46f) * (talkTake == OverteTake.PointBrief ? .60f : 1f);
                float support = lead * (hasDesk ? .54f : .75f);
                motion.right = RecordedArm.Blend(motion.right, talkPose.body.right, primaryRight ? lead : support);
                motion.left = RecordedArm.Blend(motion.left, talkPose.body.left, primaryRight ? support : lead);
            }
            // A consistent order preserves clearance as the hands pass each other.
            ArmRig first = primaryRight ? rightArm : leftArm, second = primaryRight ? leftArm : rightArm;
            float curl = 8f + 3 * energy + 3 * Mathf.Sin(.82f * time + seed);
            float count = SpeakingAction == SpeakingMotion.CountPoint ? pose.speech : 0;
            countBlend = Mathf.MoveTowards(countBlend, count, dt * 5.5f);
            float think = ListeningAction == ListeningMotion.Think ? pose.idle : 0;
            float shape = Mathf.Max(countBlend, .67f * think);
            float rightLift = FloorLift(rightArm), leftLift = FloorLift(leftArm);
            CurlFingers(rightFingers, Mathf.Lerp(curl, 2, rightLift), primaryRight ? shape * (1 - rightLift) : 0);
            CurlFingers(leftFingers, Mathf.Lerp(curl, 2, leftLift), primaryRight ? 0 : shape * (1 - leftLift));
            SolveGesture(first, primaryRight ? motion.right : motion.left, true, second.solved ? second : null, pose, dt);
            SolveGesture(second, primaryRight ? motion.left : motion.right, false, first, pose, dt);
        }

        void SolveGesture(ArmRig arm, RecordedArm track, bool lead, ArmRig other, SeatedPose pose, float dt)
        {
            float side = arm.side;
            Vector3 center = Vector3.Lerp(pelvisPoint, shouldersPoint, .60f);
            BodySlice section = torsoSlices[torsoSlices.Length / 2];
            float front = center.z + section.center.y + section.half.y;
            float low = Mathf.Max(pelvisPoint.y + .145f, hasDesk ? deskSurface.max.y + palmRadius + .018f : 0);
            float forward = Mathf.Max(front + .115f, hasDesk ? deskSurface.min.z + .055f : 0);
            Vector3 mark = new(center.x + side * .085f, low, forward);
            Vector3 finger = new(side * -.30f, .02f, 1);
            Vector3 palmAcross = new(-side, 0, 0);
            Vector3 pole = new(side * .72f, -.76f, .06f);
            float weight = pose.idle;
            switch (ListeningAction)
            {
                case ListeningMotion.LeanIn or ListeningMotion.SettleBack when hasDesk:
                    // Let the torso move while the supporting forearms stay above
                    // the table edge at a comfortable distance from each other.
                    mark.x = center.x + side * .135f;
                    mark.z += .018f;
                    pole = new(side * .83f, -.77f, -.11f);
                    weight *= .62f;
                    break;
                case ListeningMotion.FoldArms:
                    mark = new(center.x + side * (lead ? .115f : .140f), low + (lead ? .072f : .025f),
                        front + (lead ? .180f : .220f));
                    finger = new(-side * .34f, .09f, .94f);
                    break;
                case ListeningMotion.Think when lead:
                    finger = new Vector3(-side * .14f, .58f, .80f).normalized;
                    mark = facePoint + new Vector3(side * (headRadii.x + .045f), -headRadii.y - .145f, headRadii.z + .095f);
                    palmAcross = new(-side * .83f, -.17f, .12f);
                    break;
                case ListeningMotion.AdjustCuff:
                    mark = new(center.x + side * (lead ? .130f : .136f),
                        low + (lead ? .075f : .022f), forward + (lead ? .054f : .023f));
                    pole = new(side * .69f, -.98f, -.23f);
                    finger = lead ? new(-side * .32f, .05f, .93f) : new(side * -.12f, .12f, 1);
                    break;
                case ListeningMotion.ClaspHands:
                    mark.x = center.x + side * (lead ? .107f : .105f);
                    mark.y += lead ? .043f : .006f;
                    mark.z += lead ? .055f : 0;
                    finger = new(side * -.65f, .05f, .90f);
                    if (hasDesk)
                    {
                        mark.x += side * .017f;
                        mark.z += .028f;
                    }
                    break;
                default:
                    weight = 0;
                    break;
            }
            if (pose.speech > .0001f)
            {
                float spread = SpeakingAction == SpeakingMotion.ShowWidth ? .215f : .115f;
                float y = Mathf.Max(low + .035f, shouldersPoint.y - (lead ? .15f : .26f));
                Vector3 speechMark = new(center.x + side * spread, y, front + (lead ? .225f : .165f));
                Vector3 pointing = new(side * .18f, .25f, 1);
                Vector3 speechAcross = SpeakingAction == SpeakingMotion.OfferPalm ? new(side * .60f, .86f, -.10f) :
                    SpeakingAction == SpeakingMotion.ShowWidth ? Vector3.up : new(-side * .30f, .80f, -.25f);
                if (lead && SpeakingAction == SpeakingMotion.HandToChest)
                {
                    speechMark = new(center.x + side * .060f, center.y + .038f, front + .080f);
                    pointing = new(-side * .80f, .46f, .22f);
                }
                if (lead && SpeakingAction == SpeakingMotion.CountPoint) pointing = new(side * -.13f, .85f, .65f);
                if (lead && SpeakingAction == SpeakingMotion.CompareSides) speechMark.x += side * .115f;
                float strength = pose.speech * (lead || SpeakingAction == SpeakingMotion.ShowWidth ? 1 : .20f);
                float total = weight * (1 - strength) + strength;
                mark = Vector3.Lerp(mark, speechMark, strength / total);
                finger = Vector3.Slerp(finger.normalized, pointing.normalized, strength / total);
                palmAcross = Vector3.Slerp(palmAcross.normalized, speechAcross.normalized, strength / total);
                weight = total;
            }
            SetArmPose(arm, track, mark, weight, finger.normalized, palmAcross.normalized, pole,
                FloorLift(arm), other, dt);
        }

        static void CurlFingers(FingerPose[] fingers, float curl, float count)
        {
            for (int i = 0; i < fingers.Length; i++)
            {
                FingerPose finger = fingers[i];
                if (finger.joint)
                    finger.joint.localRotation = finger.rest * Quaternion.AngleAxis(
                        Mathf.Lerp(curl * (.75f + .06f * i), i < 2 ? 1 : 35, count), finger.axis);
            }
        }
    }
}
