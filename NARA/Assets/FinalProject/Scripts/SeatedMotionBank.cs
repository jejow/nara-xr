using System;
using System.IO;
using UnityEngine;

namespace Nara.Practice
{
    // Coordinates are shoulder-relative arm lengths. X points out on each arm, Y up,
    // Z toward the presenter. Importers and differently sized avatars share this space.
    internal struct RecordedArm
    {
        public Vector3 wrist, elbow, fingers, across;
        public static RecordedArm Blend(RecordedArm a, RecordedArm b, float t) => new()
        {
            wrist = Vector3.LerpUnclamped(a.wrist, b.wrist, t),
            elbow = Vector3.LerpUnclamped(a.elbow, b.elbow, t),
            fingers = Vector3.LerpUnclamped(a.fingers, b.fingers, t),
            across = Vector3.LerpUnclamped(a.across, b.across, t)
        };
    }

    internal struct RecordedSeatPose
    {
        public Vector3 torso;
        public RecordedArm right, left;
        internal static Vector3 ReadVector(BinaryReader input) => new(input.ReadSingle(), input.ReadSingle(), input.ReadSingle());

        internal static RecordedSeatPose Read(BinaryReader input)
        {
            RecordedArm ReadArm() => new() { wrist = ReadVector(input), elbow = ReadVector(input),
                fingers = ReadVector(input), across = ReadVector(input) };
            return new RecordedSeatPose { torso = ReadVector(input), right = ReadArm(), left = ReadArm() };
        }

        public static RecordedSeatPose Blend(RecordedSeatPose a, RecordedSeatPose b, float t) => new()
        {
            torso = Vector3.LerpUnclamped(a.torso, b.torso, t),
            right = RecordedArm.Blend(a.right, b.right, t), left = RecordedArm.Blend(a.left, b.left, t)
        };
    }

    internal sealed class SeatedMotionBank
    {
        const int Actions = 10;
        static SeatedMotionBank[] recordings;
        readonly RecordedSeatPose[] frames;
        public readonly float Duration;

        SeatedMotionBank(BinaryReader input)
        {
            int count = input.ReadInt32();
            Duration = input.ReadSingle();
            if (count < 2 || count > 16000 || !(Duration > 0))
                throw new InvalidDataException("Invalid seated motion track.");
            frames = new RecordedSeatPose[count];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = RecordedSeatPose.Read(input);
        }

        public static SeatedMotionBank Get(bool desk, NaraAvatarPuppet.ListeningMotion action)
        {
            if (recordings == null)
            {
                var source = Resources.Load<TextAsset>("Nara/SeatedMotion");
                if (!source)
                    throw new InvalidDataException("NARA seated motion bank is missing.");
                using var input = new BinaryReader(new MemoryStream(source.bytes));
                if (input.ReadUInt32() != 0x314d534e || input.ReadInt32() != 2 * Actions)
                    throw new InvalidDataException("Unexpected NARA seated motion bank version.");
                var tracks = new SeatedMotionBank[2 * Actions];
                for (int i = 0; i < tracks.Length; i++)
                    tracks[i] = new SeatedMotionBank(input);
                recordings = tracks;
            }
            return recordings[(desk ? Actions : 0) + (int)action];
        }

        public RecordedSeatPose Sample(float seconds)
        {
            float frame = Mathf.Clamp01(seconds / Duration) * (frames.Length - 1);
            int i = Mathf.Min((int)frame, frames.Length - 2);
            return RecordedSeatPose.Blend(frames[i], frames[i + 1], frame - i);
        }
    }
}
