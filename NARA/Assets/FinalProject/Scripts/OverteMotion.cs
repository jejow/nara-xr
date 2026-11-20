using System;
using System.IO;
using UnityEngine;

namespace Nara.Practice
{
    internal enum OverteTake
    {
        RaiseRight01, RaiseRight02, RaiseLeft03, Talk02, Talk03, Talk04, Lean, Look, Nod, Shift, Fidget,
        Acknowledge, NodYes, SlowNod, LookBrief, LookAndSettle, ShiftSeat, PointBrief
    }

    internal readonly struct UpperBodyPose
    {
        public readonly RecordedSeatPose body;
        public readonly Vector3 head;

        public UpperBodyPose(RecordedSeatPose body, Vector3 head)
        {
            this.body = body;
            this.head = head;
        }

        public static UpperBodyPose Blend(UpperBodyPose a, UpperBodyPose b, float weight) =>
            new(RecordedSeatPose.Blend(a.body, b.body, weight), Vector3.LerpUnclamped(a.head, b.head, weight));
    }

    internal sealed class UpperBodyMotion
    {
        static UpperBodyMotion[] overte, localRaises;
        readonly UpperBodyPose[] frames;
        public readonly float Duration, HoldStart, LowerStart;

        UpperBodyMotion(BinaryReader input, bool raise)
        {
            int count = input.ReadInt32();
            Duration = input.ReadSingle();
            HoldStart = input.ReadSingle();
            LowerStart = input.ReadSingle();
            bool validStages = raise
                ? HoldStart > 0 && LowerStart > HoldStart && LowerStart < Duration
                : HoldStart == 0 && LowerStart == 0;
            if (count < 2 || count > 16000 || float.IsNaN(Duration) || float.IsInfinity(Duration) || Duration <= 0 || !validStages)
                throw new InvalidDataException("Invalid motion timing.");
            frames = new UpperBodyPose[count];
            for (int i = 0; i < count; i++)
                frames[i] = new UpperBodyPose(RecordedSeatPose.Read(input), RecordedSeatPose.ReadVector(input));
        }

        public static UpperBodyMotion Get(OverteTake take)
        {
            if (overte == null)
            {
                TextAsset source = Resources.Load<TextAsset>("Nara/OverteMotion");
                if (!source) throw new InvalidDataException("NARA Overte motion bank is missing.");
                using var input = new BinaryReader(new MemoryStream(source.bytes));
                int count = Enum.GetValues(typeof(OverteTake)).Length;
                if (input.ReadUInt32() != 0x31564f4e || input.ReadInt32() != count)
                    throw new InvalidDataException("Unexpected Overte motion bank version.");
                var tracks = new UpperBodyMotion[count];
                for (int i = 0; i < count; i++) tracks[i] = new UpperBodyMotion(input, i <= (int)OverteTake.RaiseLeft03);
                if (input.BaseStream.Position != input.BaseStream.Length)
                    throw new InvalidDataException("Unexpected extra data in the Overte motion bank.");
                overte = tracks;
            }
            return overte[(int)take];
        }

        public static UpperBodyMotion[] LocalRaises
        {
            get
            {
                if (localRaises != null) return localRaises;
                localRaises = Array.Empty<UpperBodyMotion>();
                TextAsset source = Resources.Load<TextAsset>("Nara/LocalAudienceRaises");
                if (!source) return localRaises;
                try
                {
                    using var input = new BinaryReader(new MemoryStream(source.bytes));
                    uint version = input.ReadUInt32();
                    int count = input.ReadInt32();
                    if (version != 0x3159484e || count < 1 || count > 8)
                        throw new InvalidDataException("Unexpected local motion bank version.");
                    var tracks = new UpperBodyMotion[count];
                    for (int i = 0; i < count; i++) tracks[i] = new UpperBodyMotion(input, true);
                    if (input.BaseStream.Position != input.BaseStream.Length)
                        throw new InvalidDataException("Unexpected data after local motions.");
                    localRaises = tracks;
                }
                catch (Exception error) when (error is IOException or InvalidDataException)
                {
                    Debug.LogWarning("NARA could not load the local audience motions.");
                }
                return localRaises;
            }
        }

        public UpperBodyPose Sample(float seconds)
        {
            float frame = Mathf.Clamp01(seconds / Duration) * (frames.Length - 1);
            int i = Mathf.Min((int)frame, frames.Length - 2);
            return UpperBodyPose.Blend(frames[i], frames[i + 1], frame - i);
        }

        public UpperBodyPose Loop(float elapsed, float start, float end)
        {
            float length = end - start;
            float blendTime = Mathf.Min(.32f, length * .2f);
            float loopLength = length - blendTime;
            float time = elapsed < length ? elapsed : blendTime + Mathf.Repeat(elapsed - length, loopLength);
            float blend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(loopLength, length, time));
            return blend > 0
                ? UpperBodyPose.Blend(Sample(start + time), Sample(start + time - loopLength), blend)
                : Sample(start + time);
        }
    }
}
