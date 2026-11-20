using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Nara.Practice
{
    public sealed partial class NaraPractice
    {
        const int TranscriptChunkSeconds = 12;
        const int RecordingBytesPerSecond = PracticeMicrophone.SampleRate * 2;
        const int TranscriptChunkBytes = TranscriptChunkSeconds * RecordingBytesPerSecond;
        const int VoiceDeadline = 26;
        const int TranscriptDeadline = 19;

        UnityWebRequest voiceRequest, transcriptRequest;
        Coroutine voiceDownload, transcriptDownload;
        int transcribedBytes;
        bool transcriptUnavailable;
        bool processingAudioTurn;
        public bool SpeechPending => voiceRequest != null;
        public bool PreparingTranscript => transcriptRequest != null || processingAudioTurn;
        internal int PreparedPresentationBytes => transcribedBytes;

        [Serializable]
        sealed class SpeechInput
        {
            public string speaker, language, text;
        }

        [Serializable]
        sealed class TranscriptInput
        {
            public string language, audio;
        }

        [Serializable]
        sealed class TranscriptOutput
        {
            public string transcript;
        }

        UnityWebRequest RelayPost(string path, string json, int timeout)
        {
            var request = new UnityWebRequest(settings.endpoint + path, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = timeout,
                redirectLimit = 0
            };
            request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(settings.accessToken))
                request.SetRequestHeader("Authorization", "Bearer " + settings.accessToken);
            return request;
        }

        static UnityWebRequestAsyncOperation TrySend(UnityWebRequest request)
        {
            try { return request.SendWebRequest(); }
            catch (InvalidOperationException) { return null; }
        }

        IEnumerator DownloadVoice(TurnReply reply, int seat)
        {
            var input = new SpeechInput { speaker = reply.speaker, language = language, text = reply.text };
            using var request = RelayPost("/speech", JsonUtility.ToJson(input), VoiceDeadline);
            voiceRequest = request;
            var operation = TrySend(request);
            if (operation != null)
                yield return operation;
            voiceRequest = null;
            voiceDownload = null;
            AudioClip clip = operation != null && request.result == UnityWebRequest.Result.Success
                ? PracticeMicrophone.DecodeWav(request.downloadHandler.data) : null;
            if (!clip)
            {
                Notify("Suara NPC belum tersedia. Kamu bisa lanjut lewat teks.", 6);
                yield break;
            }
            ReplaceGeneratedAudio(clip);
            lastAudio = clip;
            // The player may have already started answering from the caption.
            if (Session.Phase == PracticePhase.Review)
                Speak(reply.text, clip, seat, AudienceLine.Feedback);
            else if (Session.Phase == PracticePhase.Question && !Recording && !view.AnswerInProgress)
                Speak(reply.text, clip, seat, QuestionCue());
        }

        void ReplaceGeneratedAudio(AudioClip clip)
        {
            if (generatedAudio)
                Destroy(generatedAudio);
            generatedAudio = clip;
        }

        void CancelVoiceDownload()
        {
            voiceRequest?.Abort();
            if (voiceDownload != null)
                StopCoroutine(voiceDownload);
            voiceRequest?.Dispose();
            voiceRequest = null;
            voiceDownload = null;
        }

        void CapturePresentationInBackground()
        {
            if (!Live || Session.Phase != PracticePhase.Presentation || transcriptUnavailable)
                return;
            BufferPresentation(mic.ReadChunk(TranscriptChunkSeconds));
            if (transcriptRequest != null)
                return;
            int available = (int)presentationAudio.Length - transcribedBytes;
            int minimum = Recording ? TranscriptChunkBytes : RecordingBytesPerSecond / 2;
            if (available >= minimum)
                transcriptDownload = StartCoroutine(PrepareTranscript(Mathf.Min(TranscriptChunkBytes, available)));
        }

        internal void BufferPresentation(byte[] pcm)
        {
            if (pcm == null)
                return;
            int count = Mathf.Min(pcm.Length,
                RecordingBytesPerSecond * PracticeSession.Duration - (int)presentationAudio.Length);
            if (count > 0)
                presentationAudio.Write(pcm, 0, count);
        }

        byte[] PresentationWav(int offset, int count)
        {
            if (count == 0)
                return null;
            return PracticeMicrophone.EncodeWav(new ArraySegment<byte>(presentationAudio.GetBuffer(), offset, count));
        }

        IEnumerator PrepareTranscript(int count)
        {
            var input = new TranscriptInput
            {
                language = language,
                audio = Convert.ToBase64String(PresentationWav(transcribedBytes, count))
            };
            using var request = RelayPost("/transcribe", JsonUtility.ToJson(input), TranscriptDeadline);
            transcriptRequest = request;
            var operation = TrySend(request);
            if (operation != null)
                yield return operation;
            transcriptRequest = null;
            transcriptDownload = null;

            TranscriptOutput output = null;
            if (operation != null && request.result == UnityWebRequest.Result.Success)
            {
                try { output = JsonUtility.FromJson<TranscriptOutput>(request.downloadHandler.text); }
                catch (ArgumentException) { }
            }
            if (output?.transcript == null)
            {
                transcriptUnavailable = true;
                yield break;
            }
            transcribedBytes += count;
            if (!string.IsNullOrWhiteSpace(output.transcript))
            {
                string speech = (Session.Presentation + " " + output.transcript).Trim();
                Session.Presentation = speech.Length <= 7000 ? speech : speech[^7000..];
            }
        }

        IEnumerator WaitForPreparedPresentation(float started)
        {
            while (transcriptRequest != null)
                yield return null;
            waiting = null;
            SendPresentationTurn(started);
        }

        void SendPresentationTurn(float started)
        {
            byte[] audio = Live ? PresentationWav(transcribedBytes, (int)presentationAudio.Length - transcribedBytes) : null;
            presentationAudio.SetLength(0);
            NextTurn(audio, started);
        }

        void CancelTranscriptDownload()
        {
            transcriptRequest?.Abort();
            if (transcriptDownload != null)
                StopCoroutine(transcriptDownload);
            transcriptRequest?.Dispose();
            transcriptRequest = null;
            transcriptDownload = null;
            transcribedBytes = 0;
            transcriptUnavailable = false;
            processingAudioTurn = false;
        }
    }
}
