using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Nara.Practice
{
    // Unity calls ReceiveData on the main thread. A complete line may span several callbacks.
    internal sealed class PracticeVoiceStream : DownloadHandlerScript
    {
        readonly byte[] line = new byte[12_288];
        readonly int textLength;
        int used;
        bool started;

        public PracticeVoiceStream(int textLength) : base(new byte[8192])
        {
            this.textLength = textLength;
            Audio = new StreamedNpcAudio(textLength);
        }

        public StreamedNpcAudio Audio { get; }
        public bool Complete { get; private set; }
        public bool Failed { get; private set; }
        public string Provider { get; private set; }

        [Serializable]
        sealed class Packet
        {
            public string type, data, provider;
            public int sampleRate, frame, chars;
        }

        protected override bool ReceiveData(byte[] data, int length)
        {
            if (data == null || length == 0)
                return true;
            if (length < 0 || length > data.Length || Failed)
                return false;
            for (int i = 0; i < length; i++)
            {
                if (data[i] == '\n')
                {
                    if (used == 0 || Complete || !ReadLine())
                        return false;
                    used = 0;
                }
                else
                {
                    if (used == line.Length)
                        return false;
                    line[used++] = data[i];
                }
            }
            return true;
        }

        bool ReadLine()
        {
            Packet packet;
            try { packet = JsonUtility.FromJson<Packet>(Encoding.UTF8.GetString(line, 0, used)); }
            catch (ArgumentException) { return false; }
            switch (packet?.type)
            {
                case "start":
                    if (started || packet.sampleRate != StreamedNpcAudio.SampleRate ||
                        packet.provider is not ("cartesia" or "piper" or "supertonic"))
                        return false;
                    Provider = packet.provider;
                    started = true;
                    return true;
                case "audio":
                    if (!started || string.IsNullOrEmpty(packet.data))
                        return false;
                    try { return Audio.Append(Convert.FromBase64String(packet.data)); }
                    catch (FormatException) { return false; }
                case "cue":
                    return started && Audio.AddCue(packet.frame, packet.chars);
                case "end":
                    if (!started || packet.frame != Audio.Frames || packet.frame == 0 || packet.chars != textLength)
                        return false;
                    Audio.AddCue(packet.frame, packet.chars);
                    Complete = true;
                    return true;
                case "error":
                    Failed = true;
                    return true;
                default:
                    return false;
            }
        }

        protected override void CompleteContent()
        {
            Failed |= !Complete || used != 0;
        }
    }

    internal sealed class StreamedNpcAudio
    {
        public const int SampleRate = 24_000;
        const int MaxFrames = SampleRate * 60;
        const int LeadFrames = SampleRate * 160 / 1000;
        readonly object gate = new();
        readonly int captionLength;
        readonly List<(int output, int source, int count)> spans = new(16);
        readonly List<(int frame, int chars)> cues = new();
        float[] samples = new float[SampleRate / 2];
        int length, read, output, lastAudioFrame;
        bool finished;

        public StreamedNpcAudio(int captionLength) => this.captionLength = captionLength;
        public int Frames { get { lock (gate) return length; } }

        public bool HasLead => Frames >= LeadFrames;

        public bool Append(byte[] pcm)
        {
            if (pcm.Length == 0 || (pcm.Length & 1) != 0)
                return false;
            int count = pcm.Length / 2;
            lock (gate)
            {
                if (finished || count > MaxFrames - length)
                    return false;
                if (length + count > samples.Length)
                    Array.Resize(ref samples, Math.Min(MaxFrames, Math.Max(samples.Length * 2, length + count)));
                for (int i = 0; i < count; i++)
                    samples[length + i] = (short)(pcm[i * 2] | pcm[i * 2 + 1] << 8) / 32768f;
                length += count;
            }
            return true;
        }

        public bool AddCue(int frame, int chars)
        {
            if (frame < 0 || frame > MaxFrames || chars < 0 || chars > captionLength || cues.Count >= 512)
                return false;
            if (cues.Count > 0)
            {
                var previous = cues[^1];
                // Provider timings can repeat at a chunk boundary.
                if (frame < previous.frame || chars < previous.chars)
                    return true;
                if (frame == previous.frame)
                {
                    cues[^1] = (frame, chars);
                    return true;
                }
            }
            cues.Add((frame, chars));
            return true;
        }

        public void Finish()
        {
            lock (gate)
                finished = true;
        }

        public AudioClip CreatePlayback() =>
            AudioClip.Create("Live NPC voice stream", MaxFrames + SampleRate * 30, 1, SampleRate, true, Read);

        public AudioClip CreateReplay()
        {
            // Unity's mixer only reads this array. The download has already finished.
            float[] replay;
            lock (gate)
            {
                if (!finished || length == 0)
                    return null;
                replay = new float[length];
                Array.Copy(samples, replay, length);
            }
            var clip = AudioClip.Create("Replay live NPC voice", replay.Length, 1, SampleRate, false);
            if (clip.SetData(replay, 0))
                return clip;
            UnityEngine.Object.Destroy(clip);
            return null;
        }

        void Read(float[] data)
        {
            int copied;
            lock (gate)
            {
                copied = Math.Min(length - read, data.Length);
                if (copied > 0)
                {
                    Array.Copy(samples, read, data, 0, copied);
                    if (lastAudioFrame == output && spans.Count > 0)
                    {
                        var previous = spans[^1];
                        spans[^1] = (previous.output, previous.source, previous.count + copied);
                    }
                    else
                        spans.Add((output, read, copied));
                    read += copied;
                    lastAudioFrame = output + copied;
                }
                output += data.Length;
            }
            Array.Clear(data, copied, data.Length - copied);
        }

        public bool ReachedEnd(int outputFrame)
        {
            lock (gate)
                return finished && read == length && outputFrame >= lastAudioFrame;
        }

        public int SourceFrame(int outputFrame)
        {
            // Underflow produces silence in the virtual clip. The subtitle clock skips that silence.
            lock (gate)
            {
                for (int i = spans.Count - 1; i >= 0; i--)
                {
                    var span = spans[i];
                    if (outputFrame >= span.output)
                        return span.source + Math.Min(outputFrame - span.output, span.count);
                }
            }
            return 0;
        }

        public float Progress(int frame)
        {
            lock (gate)
            {
                int estimated = finished ? length : Math.Max(length, captionLength * SampleRate / 14);
                return estimated > 0 ? Mathf.Clamp01(frame / (float)estimated) : 0;
            }
        }

        public float CaptionProgress(int frame)
        {
            if (cues.Count == 0 || captionLength == 0)
                return Progress(frame);
            var previous = (frame: 0, chars: 0);
            foreach (var cue in cues)
            {
                if (frame < cue.frame)
                    return Mathf.Lerp(previous.chars, cue.chars,
                        Mathf.InverseLerp(previous.frame, cue.frame, frame)) / captionLength;
                previous = cue;
            }
            return Mathf.Max(previous.chars / (float)captionLength, Progress(frame));
        }
    }
}
