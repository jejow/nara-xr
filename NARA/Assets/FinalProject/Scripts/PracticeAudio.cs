using System;
using System.IO;
using UnityEngine;

namespace Nara.Practice
{
    public sealed class PracticeMicrophone : IDisposable
    {
        const int SampleRate = 16000;
        AudioClip input;
        string device;
        float beganAt;
        int limit;

        public bool Recording => input != null;
#if UNITY_ANDROID && !UNITY_EDITOR
        public static bool PermissionGranted => UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#else
        public static bool PermissionGranted => Application.HasUserAuthorization(UserAuthorization.Microphone);
#endif
        public float Duration => Recording ? Time.realtimeSinceStartup - beganAt : 0;
        public bool ReachedLimit => Recording && Duration >= limit;

        public bool Begin(int seconds)
        {
            if (Recording || !PermissionGranted)
                return false;
            string[] devices = Microphone.devices;
            if (devices.Length == 0)
                return false;
            device = devices[0];
            limit = Mathf.Clamp(seconds, 1, PracticeSession.Duration);
            input = Microphone.Start(device, false, limit, SampleRate);
            beganAt = Time.realtimeSinceStartup;
            return Recording;
        }

        public byte[] Finish()
        {
            if (!Recording)
                return null;
            AudioClip clip = input;
            int frames = Microphone.GetPosition(device);
            if (frames <= 0 && Duration >= limit - .15f)
                frames = clip.samples;
            Microphone.End(device);
            input = null;
            try
            {
                if (frames < clip.frequency / 5)
                    return null;
                frames = Mathf.Min(frames, clip.samples);
                float[] raw = new float[frames * clip.channels];
                if (!clip.GetData(raw, 0))
                    return null;
                int count = Mathf.FloorToInt(frames * (SampleRate / (float)clip.frequency));
                byte[] pcm = new byte[count * 2];
                for (int i = 0; i < count; i++)
                {
                    int frame = Mathf.Min(frames - 1, (int)((long)i * clip.frequency / SampleRate));
                    float total = 0;
                    for (int channel = 0; channel < clip.channels; channel++)
                        total += raw[frame * clip.channels + channel];
                    short sample = (short)Mathf.RoundToInt(Mathf.Clamp(total / clip.channels, -1, 1) * 32767);
                    pcm[2 * i] = (byte)sample;
                    pcm[2 * i + 1] = (byte)(sample >> 8);
                }
                return pcm;
            }
            finally
            {
                UnityEngine.Object.Destroy(clip);
            }
        }

        public void Dispose()
        {
            if (!Recording)
                return;
            Microphone.End(device);
            UnityEngine.Object.Destroy(input);
            input = null;
        }

        public static byte[] EncodeWav(byte[] pcm)
        {
            if (pcm == null || pcm.Length == 0)
                return null;
            using var buffer = new MemoryStream(pcm.Length + 44);
            using var writer = new BinaryWriter(buffer);
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(pcm.Length + 36);
            writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(pcm.Length);
            writer.Write(pcm);
            return buffer.ToArray();
        }

        public static AudioClip DecodeWav(string encoded)
        {
            if (string.IsNullOrEmpty(encoded) || encoded.Length > 5_000_000)
                return null;
            byte[] wav;
            try { wav = Convert.FromBase64String(encoded); }
            catch (FormatException) { return null; }
            if (wav.Length < 44 || BitConverter.ToUInt32(wav, 0) != 0x46464952 || BitConverter.ToUInt32(wav, 8) != 0x45564157)
                return null;

            int channels = 0, rate = 0, format = 0, bits = 0, dataOffset = 0, dataLength = 0;
            for (int i = 12; i <= wav.Length - 8;)
            {
                uint type = BitConverter.ToUInt32(wav, i);
                int size = BitConverter.ToInt32(wav, i + 4);
                if (size < 0 || size > wav.Length - i - 8)
                    return null;
                if (type == 0x20746D66 && size >= 16)
                {
                    format = BitConverter.ToUInt16(wav, i + 8);
                    channels = BitConverter.ToUInt16(wav, i + 10);
                    rate = BitConverter.ToInt32(wav, i + 12);
                    bits = BitConverter.ToUInt16(wav, i + 22);
                }
                if (type == 0x61746164)
                {
                    dataOffset = i + 8;
                    dataLength = size;
                }
                i += size + 8 + (size & 1);
            }
            if (format != 1 || channels < 1 || channels > 2 || bits != 16 || rate < 8000 || rate > 96000 || dataLength < channels * 2)
                return null;
            int frames = dataLength / (channels * 2);
            float[] samples = new float[frames * channels];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = BitConverter.ToInt16(wav, dataOffset + i * 2) / 32768f;
            AudioClip clip = AudioClip.Create("NARA reply", frames, channels, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }

    [Serializable]
    public sealed class RecordedLine
    {
        public string key;
        public string id;
        public string en;
    }

    [Serializable]
    public sealed class OfflineScript
    {
        public RecordedLine[] lines;
    }
}
