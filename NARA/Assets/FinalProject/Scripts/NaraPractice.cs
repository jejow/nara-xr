using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Nara.Practice
{
    public sealed class NaraPractice : MonoBehaviour
    {
        public Camera practiceCamera;
        public Transform controlsAnchor;
        public GameObject room;
        public MeshRenderer slideScreen;
        public MeshRenderer confidenceScreen;
        public float desktopPitch;
        public AudioSource voice;
        public NaraAudience audience;
        public int sceneTriangles;

        const float Deadline = 8f;
        readonly PracticeMicrophone mic = new();
        readonly MemoryStream presentationAudio = new();
        readonly Queue<(string caption, AudioClip clip)> speech = new();
        readonly Dictionary<string, RecordedLine> script = new();
        readonly List<float> replyTimes = new();
        readonly float[] voiceSamples = new float[128];
        PracticeView view;
        PracticeRig rig;
        VoiceSettings settings;
        Coroutine waiting;
        UnityWebRequest activeRequest;
        AudioClip lastAudio;
        AudioClip generatedAudio;
        Material screenMaterial;
        Vector3 projectionSize, confidenceSize;
        string notice = "";
        float noticeUntil;
        string language = "id";
        bool networkAvailable = true;
        bool logMetrics;
        bool sawLiveReply;
        bool usedMock;
        int fallbacks, questionSeat, secondSeat = 2;
        float questionStartsAt = -1, answerOpensAt, floorGrantedAt = -10;

        public PracticeSession Session { get; } = new();
        public PracticeDeck Deck { get; } = new();
        public int SlideIndex { get; private set; }
        public bool Baseline { get; private set; }
        public bool Live { get; private set; }
        public bool Mock => usedMock;
        public bool XrActive => rig != null && rig.Active;
        public bool HasEndpoint => !string.IsNullOrEmpty(settings?.endpoint);
        public bool Recording => mic.Recording;
        public string Language => language;
        public string SpeechText { get; private set; } = "";
        public string Notice => Time.unscaledTime < noticeUntil ? notice : "";
        public Texture2D Slide => SlideIndex < Deck.Slides.Count ? Deck.Slides[SlideIndex] : null;
        public bool CanReplay => lastAudio && Session.Phase is PracticePhase.Question or PracticePhase.Review;
        public int QuestionerSeat => questionSeat;
        public string QuestionerName => audience.Profiles[audience.Selection(questionSeat)].label;
        public int FallbackCount => fallbacks;

        void Awake()
        {
            OfflineScript offline = JsonUtility.FromJson<OfflineScript>(Resources.Load<TextAsset>("Nara/OfflineScript").text);
            foreach (RecordedLine line in offline.lines)
                script.Add(line.key, line);
            screenMaterial = slideScreen.material;
            projectionSize = slideScreen.transform.localScale;
            if (confidenceScreen)
            {
                confidenceScreen.sharedMaterial = screenMaterial;
                confidenceSize = confidenceScreen.transform.localScale;
            }
            audience.Initialise();
            view = gameObject.AddComponent<PracticeView>();
            rig = gameObject.AddComponent<PracticeRig>();
            rig.Initialise(this);
            ReloadDeck();
            view.Initialise(this);
            ApplySlide();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-naraSmoke") >= 0)
                gameObject.AddComponent<PracticeSmoke>().StartRun(this);
        }

        void Update()
        {
            if (Session.Phase == PracticePhase.Presentation && !Session.Paused)
            {
                Session.Tick(Time.unscaledDeltaTime);
                if (Session.Remaining <= 0)
                    FinishPresentation();
            }
            if (mic.ReachedLimit)
                EndTalk();
            if (!voice.isPlaying)
            {
                SpeechText = "";
                if (speech.Count > 0)
                {
                    var line = speech.Dequeue();
                    Speak(line.caption, line.clip);
                }
            }
            AdvanceQuestion();
            AnimateAudience();
            view.Refresh();
        }

        public void ReloadDeck()
        {
            if (Session.Phase != PracticePhase.Setup)
                return;
            Deck.Reload();
            settings = VoiceSettings.Load(Deck.Folder);
            SlideIndex = 0;
            ApplySlide();
            view?.LoadSetup();
        }

        public void StartPractice(string topic, string summary, string selectedLanguage, bool baseline, bool cloudConsent, bool metrics)
        {
            if (Session.Phase != PracticePhase.Setup || string.IsNullOrWhiteSpace(topic))
                return;
            ResetAudio();
            questionSeat = 0;
            secondSeat = secondSeat == 1 ? 2 : 1;
            language = selectedLanguage == "en" ? "en" : "id";
            Baseline = baseline && !XrActive;
            Live = cloudConsent && HasEndpoint && networkAvailable;
            usedMock = sawLiveReply = false;
            logMetrics = metrics;
            fallbacks = 0;
            replyTimes.Clear();
            SlideIndex = 0;
            Session.Start(topic, summary ?? "");
            ApplySlide();
            ApplyPresentationMode();
            if (cloudConsent && !Live)
                Notify("Koneksi belum tersedia. Sesi memakai pertanyaan tersimpan.");
        }

        public void ApplyPresentationMode()
        {
            if (XrActive)
                Baseline = false;
            room.SetActive(!Baseline || Session.Phase == PracticePhase.Setup);
            voice.spatialBlend = Baseline ? 0 : .55f;
        }

        public void ChangeSlide(int delta)
        {
            if (Deck.Slides.Count == 0 || Session.Phase == PracticePhase.Setup || Session.Phase == PracticePhase.Review)
                return;
            SlideIndex = Mathf.Clamp(SlideIndex + delta, 0, Deck.Slides.Count - 1);
            ApplySlide();
        }

        void ApplySlide()
        {
            if (!screenMaterial)
                return;
            Texture2D image = Slide;
            screenMaterial.mainTexture = image;
            float ratio = image ? (float)image.width / image.height : 16f / 9;
            slideScreen.transform.localScale = FitSlide(projectionSize, ratio);
            if (confidenceScreen)
                confidenceScreen.transform.localScale = FitSlide(confidenceSize, ratio);
        }

        static Vector3 FitSlide(Vector3 size, float ratio)
        {
            size.x = Mathf.Min(size.x, size.y * ratio);
            size.y = Mathf.Min(size.y, size.x / ratio);
            return size;
        }

        public void TogglePause()
        {
            if (Session.Phase != PracticePhase.Presentation)
                return;
            if (!Session.Paused)
                StorePresentationAudio();
            Session.TogglePause();
        }

        public void FinishPresentation()
        {
            if (Session.Phase != PracticePhase.Presentation)
                return;
            float started = Time.realtimeSinceStartup;
            StorePresentationAudio();
            byte[] audio = Live ? PracticeMicrophone.EncodeWav(presentationAudio.ToArray()) : null;
            presentationAudio.SetLength(0);
            Session.BeginQuestions();
            NextTurn(audio, started);
        }

        public void GrantFloor()
        {
            if (!Session.GrantFloor())
                return;
            StopSpeech();
            floorGrantedAt = Time.unscaledTime;
            questionStartsAt = floorGrantedAt + .38f;
        }

        void AdvanceQuestion()
        {
            if (Session.Phase != PracticePhase.Asking)
                return;
            float now = Time.unscaledTime;
            if (questionStartsAt >= 0)
            {
                if (now < questionStartsAt)
                    return;
                questionStartsAt = -1;
                Speak(Session.Question, lastAudio);
                float duration = lastAudio ? lastAudio.length + .22f : Mathf.Clamp(Session.Question.Length / 24f, 1.25f, 5f);
                answerOpensAt = now + duration;
            }
            if (now >= answerOpensAt && !voice.isPlaying)
                Session.OpenAnswer();
        }

        public void Answer(string text, bool skipped)
        {
            if (Session.Phase != PracticePhase.Question || Recording)
                return;
            if (Live && !skipped && string.IsNullOrWhiteSpace(text))
            {
                Notify("Ketik jawaban atau tahan tombol bicara.");
                return;
            }
            StopSpeech();
            Session.RecordAnswer((text ?? "").Trim(), skipped);
            NextTurn(null, Time.realtimeSinceStartup);
        }

        public void BeginTalk()
        {
            if (!Live || Recording || Session.Paused || (Session.Phase != PracticePhase.Question && Session.Phase != PracticePhase.Presentation))
                return;
            StartCoroutine(RequestMicrophone());
        }

        IEnumerator RequestMicrophone()
        {
            if (!PracticeMicrophone.PermissionGranted)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
#else
                yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
#endif
                Notify("Jika izin mikrofon diberikan, tahan tombol bicara lagi.");
                yield break;
            }
            int duration = Session.Phase == PracticePhase.Presentation ? Mathf.Max(1, Mathf.CeilToInt(Session.Remaining)) : 45;
            StopSpeech();
            if (!mic.Begin(duration))
                Notify("Mikrofon belum tersedia. Kamu bisa menjawab dengan teks.");
        }

        public void EndTalk()
        {
            if (!Recording)
                return;
            float started = Time.realtimeSinceStartup;
            if (Session.Phase == PracticePhase.Presentation)
            {
                StorePresentationAudio();
                return;
            }
            byte[] recording = mic.Finish();
            if (Session.Phase != PracticePhase.Question)
                return;
            if (recording == null)
            {
                SpeakNotice("unusable");
                return;
            }
            Session.RecordAnswer("", false);
            NextTurn(PracticeMicrophone.EncodeWav(recording), started);
        }

        void StorePresentationAudio()
        {
            byte[] part = mic.Finish();
            if (part == null)
                return;
            int bytes = Mathf.Min(part.Length, 16000 * 2 * PracticeSession.Duration - (int)presentationAudio.Length);
            if (bytes > 0)
                presentationAudio.Write(part, 0, bytes);
        }

        public void SetNetworkAvailable(bool available)
        {
            networkAvailable = available;
            if (available)
            {
                Notify("Koneksi uji dipulihkan. Mode live dapat dipilih pada sesi berikutnya.");
                return;
            }
            mic.Dispose();
            presentationAudio.SetLength(0);
            CancelRequest();
            Live = false;
            fallbacks++;
            if (Session.Phase is PracticePhase.FloorRequest or PracticePhase.Asking)
            {
                Notify(Text("connection"), 7);
                return;
            }
            SpeakNotice("connection");
            if (Session.Phase == PracticePhase.Waiting)
                LocalTurn(true);
        }

        void NextTurn(byte[] audio, float started)
        {
            if (!Live)
            {
                LocalTurn(false);
                return;
            }
            if (!networkAvailable || Application.internetReachability == NetworkReachability.NotReachable)
            {
                FallBack("connection", false);
                return;
            }
            waiting = StartCoroutine(RequestTurn(Session.Request(language, audio), started));
        }

        IEnumerator RequestTurn(TurnRequest turn, float started)
        {
            StopSpeech();
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(turn));
            using var request = new UnityWebRequest(settings.endpoint + "/turn", "POST");
            activeRequest = request;
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = (int)Deadline;
            request.redirectLimit = 0;
            request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(settings.accessToken))
                request.SetRequestHeader("Authorization", "Bearer " + settings.accessToken);
            UnityWebRequestAsyncOperation operation = null;
            try { operation = request.SendWebRequest(); }
            catch (InvalidOperationException) { }
            if (operation == null)
            {
                activeRequest = null;
                waiting = null;
                FallBack("connection", false);
                yield break;
            }
            while (!operation.isDone && Time.realtimeSinceStartup - started < Deadline &&
                   Application.internetReachability != NetworkReachability.NotReachable)
                yield return null;
            activeRequest = null;
            waiting = null;
            float seconds = Time.realtimeSinceStartup - started;
            replyTimes.Add(seconds);
            if (!operation.isDone || seconds >= Deadline)
            {
                request.Abort();
                FallBack(seconds >= Deadline ? "timeout" : "connection", false);
                yield break;
            }
            if (request.result != UnityWebRequest.Result.Success)
            {
                string key = request.responseCode == 422 ? "unusable" :
                    request.responseCode == 504 ? "timeout" : "connection";
                FallBack(key, key == "unusable" && !string.IsNullOrEmpty(turn.audio));
                yield break;
            }
            TurnReply reply;
            try { reply = JsonUtility.FromJson<TurnReply>(request.downloadHandler.text); }
            catch (ArgumentException) { reply = null; }
            Session.SetLatestTranscript(reply?.transcript);
            if (reply == null || !ValidFollowUp(reply, turn) || !Session.ApplyReply(reply.kind, reply.text))
            {
                FallBack("unusable", !string.IsNullOrEmpty(turn.audio));
                yield break;
            }
            usedMock |= reply.source == "mock";
            sawLiveReply |= reply.source == "live";
            AudioClip clip = PracticeMicrophone.DecodeWav(reply.audio);
            if (generatedAudio)
                Destroy(generatedAudio);
            generatedAudio = clip;
            ShowReply(reply.text, clip, false);
        }

        static bool ValidFollowUp(TurnReply reply, TurnRequest turn)
        {
            if (reply.kind != PracticeSession.FollowUp)
                return true;
            string answer = string.IsNullOrWhiteSpace(reply.transcript) ? turn.answer : reply.transcript;
            string anchor = (reply.anchor ?? "").Trim();
            return anchor.Length >= 4 && (answer ?? "").IndexOf(anchor, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void FallBack(string key, bool canRetry)
        {
            fallbacks++;
            SpeakNotice(key);
            if (canRetry && Session.RetryAnswer())
                return;
            Live = false;
            LocalTurn(true);
        }

        void LocalTurn(bool queue)
        {
            string key = Session.Operation;
            string kind = "question";
            if (key == PracticeSession.AfterSecond || key == PracticeSession.Feedback)
                key = kind = PracticeSession.Feedback;
            string text = Text(key);
            if (Session.ApplyReply(kind, text))
                ShowReply(text, Clip(key), queue);
        }

        string Text(string key) => language == "en" ? script[key].en : script[key].id;
        AudioClip Clip(string key) => Resources.Load<AudioClip>("Nara/" + key + "_" + language);

        void SpeakNotice(string key)
        {
            StopSpeech();
            string text = Text(key);
            Notify(text, 7);
            Speak(text, Clip(key));
        }

        void ShowReply(string text, AudioClip audio, bool queue)
        {
            lastAudio = audio;
            if (Session.Phase == PracticePhase.FloorRequest)
            {
                if (!Session.IsFollowUp)
                    questionSeat = Session.MainQuestions == 0 ? 0 : secondSeat;
                return;
            }
            if (queue && voice.isPlaying && audio)
                speech.Enqueue((text, audio));
            else
                Speak(text, audio);
            if (Session.Phase == PracticePhase.Review)
                WriteMetrics(true);
        }

        public void Replay()
        {
            if (!CanReplay || Recording)
                return;
            StopSpeech();
            if (Session.RepeatQuestion())
                questionStartsAt = Time.unscaledTime;
            else
                Speak(Session.Suggestion, lastAudio);
        }

        void Speak(string text, AudioClip clip)
        {
            if (!clip)
                return;
            SpeechText = text;
            voice.clip = clip;
            voice.Play();
        }

        void StopSpeech()
        {
            speech.Clear();
            voice.Stop();
            voice.clip = null;
            SpeechText = "";
        }

        void AnimateAudience()
        {
            float amplitude = 0;
            if (voice.isPlaying)
            {
                voice.GetOutputData(voiceSamples, 0);
                foreach (float sample in voiceSamples)
                    amplitude += sample * sample;
                amplitude = Mathf.Clamp01(Mathf.Sqrt(amplitude / voiceSamples.Length) * 7);
            }
            bool questionVoice = Session.Phase is PracticePhase.Asking or PracticePhase.Review;
            Transform speaker = audience.HeadAt(questionVoice ? questionSeat : 0);
            if (speaker)
                voice.transform.position = speaker.position;
            float now = Time.unscaledTime;
            float nod = Session.Phase == PracticePhase.Asking ? Mathf.Sin(Mathf.PI * Mathf.Clamp01((now - floorGrantedAt) / .62f)) : 0;
            bool listens = (Session.Phase == PracticePhase.Presentation && !Session.Paused) || Session.Phase == PracticePhase.Question;
            audience.Animate(now, Session.Elapsed, listens, amplitude, questionVoice && voice.isPlaying,
                practiceCamera.transform.position, questionSeat, Session.Phase == PracticePhase.FloorRequest, nod);
        }

        public void Notify(string text, float seconds = 4)
        {
            notice = text;
            noticeUntil = Time.unscaledTime + seconds;
        }

        public void ResetPractice()
        {
            if (Session.Phase != PracticePhase.Setup && Session.Phase != PracticePhase.Review)
                WriteMetrics(false);
            CancelRequest();
            ResetAudio();
            Session.Reset();
            Baseline = Live = false;
            networkAvailable = true;
            SlideIndex = 0;
            noticeUntil = 0;
            ApplySlide();
            ApplyPresentationMode();
            view.ReturnToSetup();
        }

        void CancelRequest()
        {
            activeRequest?.Abort();
            if (waiting != null)
                StopCoroutine(waiting);
            activeRequest?.Dispose();
            activeRequest = null;
            waiting = null;
        }

        void ResetAudio()
        {
            mic.Dispose();
            presentationAudio.SetLength(0);
            StopSpeech();
            questionStartsAt = -1;
            floorGrantedAt = -10;
            if (generatedAudio)
                Destroy(generatedAudio);
            generatedAudio = lastAudio = null;
        }

        [Serializable]
        sealed class SessionMetrics
        {
            public string condition, mode;
            public float presentationSeconds;
            public int questions, answerAttempts, skips, fallbacks;
            public bool completed, followUp;
            public float[] replySeconds;
        }

        void WriteMetrics(bool completed)
        {
            if (!logMetrics)
                return;
            logMetrics = false;
            string folder = Path.Combine(Application.persistentDataPath, "Evaluation");
            try
            {
                Directory.CreateDirectory(folder);
                foreach (string file in Directory.GetFiles(folder, "session-*.json"))
                {
                    if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-7))
                        File.Delete(file);
                }
                var record = new SessionMetrics
                {
                    condition = XrActive ? "headset" : Baseline ? "laptop" : "desktop_3d",
                    mode = usedMock ? "mock" : sawLiveReply ? (fallbacks > 0 ? "live_fallback" : "live") : "offline",
                    presentationSeconds = Session.Elapsed, questions = Session.MainQuestions, answerAttempts = Session.Attempts,
                    skips = Session.Skips, fallbacks = fallbacks, completed = completed,
                    followUp = Session.FollowUpAsked, replySeconds = replyTimes.ToArray()
                };
                File.WriteAllText(Path.Combine(folder, $"session-{Guid.NewGuid():N}.json"), JsonUtility.ToJson(record, true));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogWarning("NARA could not save evaluation timing.");
            }
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused)
                mic.Dispose();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
                mic.Dispose();
        }

        void OnDestroy()
        {
            CancelRequest();
            mic.Dispose();
            presentationAudio.Dispose();
            Deck.Dispose();
            if (generatedAudio)
                Destroy(generatedAudio);
            if (screenMaterial)
                Destroy(screenMaterial);
        }
    }
}
