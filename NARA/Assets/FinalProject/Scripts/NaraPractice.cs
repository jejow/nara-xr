using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace Nara.Practice
{
    public sealed partial class NaraPractice : MonoBehaviour
    {
        public const int SceneTriangleBudget = 285000;
        public const string UniversityScene = "NaraSeminar";
        public const string If101Scene = "NaraIF101";
        public const string IfClassroomName = "IF 110";
        const string ClassroomPreference = "Nara.Practice.IF101";
        static (string topic, string summary)? classroomDraft;
        public Camera practiceCamera;
        public Transform controlsAnchor;
        public GameObject room;
        public MeshRenderer slideScreen;
        public MeshRenderer confidenceScreen;
        public float desktopPitch;
        public AudioSource voice;
        public NaraAudience audience;
        public int sceneTriangles;

        internal const float TextTurnDeadline = 42f;
        internal const float AudioTurnDeadline = 60f;
        const float PresentationAudioDeadline = 138f;
        readonly PracticeMicrophone mic = new();
        readonly MemoryStream presentationAudio = new();
        readonly Queue<(string caption, AudioClip clip, int seat, AudienceLine cue)> speech = new();
        AudienceLine currentLine;
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
        bool loadingClassroom;
        int fallbacks, questionSeat, speakerSeat = -1, firstSeat, secondSeat;
        bool greeting, answerWhileSpeaking;
        float questionStartsAt = -1, answerOpensAt, floorGrantedAt = -10;

        public PracticeSession Session { get; } = new();
        public PracticeDeck Deck { get; } = new();
        public int SlideIndex { get; private set; }
        public bool Baseline { get; private set; }
        public bool Live { get; private set; }
        public bool Mock => usedMock;
        public bool XrActive => rig != null && rig.Active;
        public bool HasEndpoint => !string.IsNullOrEmpty(settings?.endpoint);
        public bool LiveAvailable => HasEndpoint && (!NaraQuestBridge.IsStandalone || NaraQuestBridge.Configured);
        public bool Recording => mic.Recording;
        public string Language => language;
        public string SpeechText { get; private set; } = "";
        internal float VoiceStartedAt { get; private set; }
        public int SpeakingSeat => voice && voice.isPlaying ? speakerSeat : -1;
        public string GreetingText => greeting ? SpeechText : "";
        public string Notice => Time.unscaledTime < noticeUntil ? notice : "";
        public Texture2D Slide => Deck.GetSlide(SlideIndex);
        public bool CanReplay => lastAudio && Session.Phase is PracticePhase.Question or PracticePhase.Review;
        public int QuestionerSeat => questionSeat;
        public string QuestionerName => audience.ProfileAt(questionSeat).label;
        public int HostSeat { get; private set; }
        public int FallbackCount => fallbacks;
        public bool IsIf101 => gameObject.scene.name == If101Scene;
        public string ClassroomName => IsIf101 ? IfClassroomName : "Kelas universitas";
        public bool LoadingClassroom => loadingClassroom;
        public bool ViewingProjection => rig && rig.ViewingProjection;

        void Awake()
        {
            if (!Application.isEditor && !IsIf101 && PlayerPrefs.GetInt(ClassroomPreference) == 1 &&
                LoadClassroomScene(If101Scene))
            {
                enabled = false;
                return;
            }
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
            if (classroomDraft is { } draft)
            {
                view.RestoreDraft(draft.topic, draft.summary);
                classroomDraft = null;
            }
            ApplySlide();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-naraSmoke") >= 0)
                gameObject.AddComponent<PracticeSmoke>().StartRun(this);
        }

        public void SelectClassroom(bool useIf101, string topic, string summary)
        {
            if (loadingClassroom || ImportingDeck || Session.Phase != PracticePhase.Setup || useIf101 == IsIf101)
                return;
            if (!LoadClassroomScene(useIf101 ? If101Scene : UniversityScene))
            {
                Notify("Ruang kelas belum tersedia.");
                return;
            }
            classroomDraft = (topic, summary);
            PlayerPrefs.SetInt(ClassroomPreference, useIf101 ? 1 : 0);
            PlayerPrefs.Save();
            Notify(useIf101 ? $"Memuat {IfClassroomName}…" : "Memuat kelas universitas…");
        }

        public void ToggleProjection() => rig.ToggleProjection();

        bool LoadClassroomScene(string scene)
        {
            if (!Application.CanStreamedLevelBeLoaded(scene))
                return false;
            loadingClassroom = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single) != null;
            return loadingClassroom;
        }

        void Update()
        {
            if (NaraQuestBridge.IsStandalone)
                ReceiveQuestDocument();
            if (greeting && !voice.isPlaying)
                greeting = false;
            if (Session.Phase == PracticePhase.Presentation && !Session.Paused && !greeting && !view.QuestTyping)
            {
                Session.Tick(Time.unscaledDeltaTime);
                if (Session.Remaining <= 0)
                    FinishPresentation();
            }
            if (mic.ReachedLimit)
                EndTalk();
            CapturePresentationInBackground();
            AdvanceVoicePlayback();
            if (!voice.isPlaying)
            {
                SpeechText = "";
                if (speech.Count > 0)
                {
                    var line = speech.Dequeue();
                    Speak(line.caption, line.clip, line.seat, line.cue);
                }
            }
            AdvanceQuestion();
            AnimateAudience();
        }

        void LateUpdate() => view.Refresh();

        public void ReloadDeck()
        {
            if (Session.Phase != PracticePhase.Setup || ImportingDeck)
                return;
            Deck.Reload();
            settings = VoiceSettings.Load(Deck.Folder);
            SlideIndex = 0;
            ApplySlide();
            view?.LoadSetup();
        }

        public void StartPractice(string topic, string summary, string selectedLanguage, bool baseline, bool cloudConsent, bool metrics)
        {
            if (loadingClassroom || ImportingDeck || Session.Phase != PracticePhase.Setup || string.IsNullOrWhiteSpace(topic))
                return;
            ResetAudio();
            HostSeat = UnityEngine.Random.Range(0, audience.SeatCount);
            (firstSeat, secondSeat) = audience.DrawQuestionSeats();
            questionSeat = firstSeat;
            language = selectedLanguage == "en" ? "en" : "id";
            Baseline = baseline && !XrActive;
            Live = cloudConsent && LiveAvailable && networkAvailable;
            usedMock = sawLiveReply = false;
            logMetrics = metrics;
            fallbacks = 0;
            replyTimes.Clear();
            SlideIndex = 0;
            Session.Start(topic, summary ?? "", Deck.Pages);
            ApplySlide();
            ApplyPresentationMode();
            AvatarProfile lead = audience.ProfileAt(HostSeat);
            Speak(Text("intro").Replace("{name}", lead.label), VoiceClip(lead, "intro", language), HostSeat, AudienceLine.Greeting);
            greeting = voice.isPlaying;
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
            if (Deck.Count == 0 || Session.Phase == PracticePhase.Setup || Session.Phase == PracticePhase.Review)
                return;
            int next = Mathf.Clamp(SlideIndex + delta, 0, Deck.Count - 1);
            if (next == SlideIndex)
                return;
            SlideIndex = next;
            Session.VisitPage(next + 1);
            ApplySlide();
            audience.SlideChanged(slideScreen.transform.position, Time.unscaledTime);
        }

        void ApplySlide()
        {
            if (!screenMaterial)
                return;
            Texture2D image = Slide;
            screenMaterial.mainTexture = image;
            float ratio = image ? (float)image.width / image.height : 16f / 9;
            slideScreen.transform.localScale = FitSlide(projectionSize, ratio);
            slideScreen.enabled = image;
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
            if (greeting)
                StopSpeech();
            float started = Time.realtimeSinceStartup;
            StorePresentationAudio();
            Session.BeginQuestions();
            if (transcriptRequest != null)
                waiting = StartCoroutine(WaitForPreparedPresentation(started));
            else
                SendPresentationTurn(started);
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
                Speak(Session.Question, lastAudio, questionSeat, QuestionCue());
                answerOpensAt = now + (!answerWhileSpeaking && lastAudio ? lastAudio.length + .22f : 0f);
            }
            if (now >= answerOpensAt && (answerWhileSpeaking || !voice.isPlaying))
                Session.OpenAnswer();
        }

        AudienceLine QuestionCue() => Session.IsFollowUp ? AudienceLine.FollowUp :
            Session.MainQuestions == 1 ? AudienceLine.FirstQuestion : AudienceLine.SecondQuestion;

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
            if (!Live || Recording || greeting || Session.Paused || (Session.Phase != PracticePhase.Question && Session.Phase != PracticePhase.Presentation))
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
            AnswerWithRecording(recording, started);
        }

        internal void AnswerWithRecording(byte[] recording, float started)
        {
            if (recording == null)
            {
                SpeakNotice("unusable");
                return;
            }
            Session.RecordAnswer("", false);
            NextTurn(PracticeMicrophone.EncodeWav(recording), started);
        }

        void StorePresentationAudio() => BufferPresentation(mic.Finish());

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
            CancelVoiceDownload();
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
            TurnRequest turn = Session.Request(language, audio);
            turn.speaker = audience.ProfileAt(SeatForTurn(turn.operation)).id;
            turn.lead = audience.ProfileAt(HostSeat).id;
            turn.deferAudio = true;
            waiting = StartCoroutine(RequestTurn(turn, started));
        }

        IEnumerator RequestTurn(TurnRequest turn, float started)
        {
            StopSpeech();
            float deadline = string.IsNullOrEmpty(turn.audio) ? TextTurnDeadline :
                turn.operation == PracticeSession.FirstQuestion ? PresentationAudioDeadline : AudioTurnDeadline;
            using var request = RelayPost("/turn", JsonUtility.ToJson(turn), (int)deadline);
            activeRequest = request;
            var operation = TrySend(request);
            if (operation == null)
            {
                activeRequest = null;
                waiting = null;
                FallBack("connection", false);
                yield break;
            }
            processingAudioTurn = !string.IsNullOrEmpty(turn.audio);
            while (!operation.isDone && Time.realtimeSinceStartup - started < deadline &&
                   Application.internetReachability != NetworkReachability.NotReachable)
                yield return null;
            activeRequest = null;
            waiting = null;
            processingAudioTurn = false;
            float seconds = Time.realtimeSinceStartup - started;
            replyTimes.Add(seconds);
            if (!operation.isDone || seconds >= deadline)
            {
                request.Abort();
                FallBack(seconds >= deadline ? "timeout" : "connection", false);
                yield break;
            }
            if (request.result != UnityWebRequest.Result.Success)
            {
                string key = request.responseCode == 422 ? "unusable" :
                    request.responseCode == 504 ? "timeout" : "connection";
                FallBack(key, key == "unusable" && !string.IsNullOrEmpty(turn.audio));
                if (NaraQuestBridge.IsStandalone && request.responseCode is 401 or 403)
                    Notify("AISUBS menolak kunci. Periksa kunci dan akses GPT-6.1 Sol di Pengaturan Quest.");
                yield break;
            }
            TurnReply reply;
            try { reply = JsonUtility.FromJson<TurnReply>(request.downloadHandler.text); }
            catch (ArgumentException) { reply = null; }
            int seat = SeatForTurn(turn.operation, reply?.kind);
            if (reply == null || reply.speaker != audience.ProfileAt(seat).id ||
                reply.source is not ("live" or "mock") || !ValidFollowUp(reply, turn))
            {
                FallBack("unusable", false);
                yield break;
            }
            AudioClip clip = PracticeMicrophone.DecodeWav(reply.audio);
            bool textOnly = string.IsNullOrEmpty(reply.audio) &&
                (reply.source == "live" || reply.kind == PracticeSession.FollowUp);
            if ((!clip && !textOnly) || !Session.ApplyReply(reply.kind, reply.text))
            {
                if (clip)
                    Destroy(clip);
                FallBack("unusable", false);
                yield break;
            }
            Session.SetLatestTranscript(turn.operation, reply.transcript);
            if (turn.operation == PracticeSession.FirstQuestion && !string.IsNullOrEmpty(turn.audio) &&
                string.IsNullOrWhiteSpace(reply.transcript))
                Notify("Rekaman tidak terbaca. Pertanyaan memakai materi yang tersedia.", 8);
            usedMock |= reply.source == "mock";
            sawLiveReply |= reply.source == "live";
            if (!clip && !reply.audioDeferred && reply.source == "live")
                Notify("Suara karakter belum tersedia. Lanjutkan dengan teks.", 6);
            ReplaceGeneratedAudio(clip);
            ShowReply(reply.text, clip, seat, false, reply.audioDeferred);
            if (reply.audioDeferred && !clip)
                StartDeferredVoice(reply, seat);
        }

        int SeatForTurn(string operation, string kind = null) => kind == PracticeSession.Feedback
            ? HostSeat
            : operation switch
            {
                PracticeSession.FirstQuestion => firstSeat,
                PracticeSession.SecondQuestion => secondSeat,
                PracticeSession.AfterSecond => questionSeat,
                _ => HostSeat,
            };

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
            int seat = SeatForTurn(Session.Operation, kind);
            string text = Text(key);
            if (Session.ApplyReply(kind, text))
                ShowReply(text, VoiceClip(audience.ProfileAt(seat), key, language), seat, queue);
        }

        string Text(string key) => language == "en" ? script[key].en : script[key].id;
        internal static AudioClip VoiceClip(AvatarProfile avatar, string key, string locale) =>
            Resources.Load<AudioClip>($"Nara/Voices/{avatar.id}/{key}_{locale}");

        void SpeakNotice(string key)
        {
            StopSpeech();
            string text = Text(key);
            Notify(text, 7);
            Speak(text, VoiceClip(audience.ProfileAt(HostSeat), key, language), HostSeat);
        }

        void ShowReply(string text, AudioClip audio, int seat, bool queue, bool readyToAnswer = false)
        {
            lastAudio = audio;
            answerWhileSpeaking = readyToAnswer;
            if (Session.Phase == PracticePhase.FloorRequest)
            {
                questionSeat = seat;
                return;
            }
            AudienceLine cue = Session.Phase == PracticePhase.Review ? AudienceLine.Feedback : AudienceLine.Notice;
            if (queue && voice.isPlaying && audio)
                speech.Enqueue((text, audio, seat, cue));
            else
                Speak(text, audio, seat, cue);
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
                Speak(Session.Suggestion, lastAudio, HostSeat, AudienceLine.Feedback);
        }

        void Speak(string text, AudioClip clip, int seat, AudienceLine cue = AudienceLine.Notice)
        {
            if (!clip)
                return;
            if (streamClip && clip != streamClip)
            {
                voice.Stop();
                ClearStreamPlayback();
            }
            SpeechText = text;
            currentLine = cue;
            speakerSeat = seat;
            Transform speaker = audience.HeadAt(seat);
            if (speaker)
                voice.transform.position = speaker.position;
            voice.clip = clip;
            VoiceStartedAt = Time.realtimeSinceStartup;
            voice.Play();
        }

        void StopSpeech()
        {
            speech.Clear();
            voice.Stop();
            voice.clip = null;
            ClearStreamPlayback();
            speakerSeat = -1;
            greeting = false;
            SpeechText = "";
        }

        void AnimateAudience()
        {
            int activeSpeaker = SpeakingSeat;
            float amplitude = 0, articulation = .5f;
            if (activeSpeaker >= 0)
            {
                voice.GetOutputData(voiceSamples, 0);
                float edges = 0, previous = voiceSamples[0];
                foreach (float sample in voiceSamples)
                {
                    amplitude += sample * sample;
                    float difference = sample - previous;
                    edges += difference * difference;
                    previous = sample;
                }
                articulation = Mathf.InverseLerp(.43f, .064f, Mathf.Sqrt(edges / Mathf.Max(.0001f, amplitude)));
                amplitude = Mathf.Clamp01(Mathf.Sqrt(amplitude / voiceSamples.Length) * 7.5f);
            }
            Transform speaker = audience.HeadAt(activeSpeaker);
            if (speaker)
                voice.transform.position = speaker.position;
            float now = Time.unscaledTime;
            float nod = Session.Phase == PracticePhase.Asking ? Mathf.Sin(Mathf.PI * Mathf.Clamp01((now - floorGrantedAt) / .62f)) : 0;
            bool listens = (Session.Phase == PracticePhase.Presentation && !Session.Paused) || Session.Phase == PracticePhase.Question;
            Transform eyes = practiceCamera.transform;
            Vector3 presenter = XrActive ? eyes.position : eyes.parent.position;
            audience.Animate(now, listens, amplitude, articulation, activeSpeaker,
                presenter, questionSeat, Session.Phase == PracticePhase.FloorRequest, nod, currentLine,
                activeSpeaker >= 0 ? SpeechProgress : 0);
        }

        public void Notify(string text, float seconds = 4)
        {
            notice = text;
            noticeUntil = Time.unscaledTime + seconds;
        }

        public void ResetPractice()
        {
            bool completedOrStarted = Session.Phase != PracticePhase.Setup;
            if (Session.Phase != PracticePhase.Setup && Session.Phase != PracticePhase.Review)
                WriteMetrics(false);
            CancelRequest();
            ResetAudio();
            Session.Reset();
            Baseline = Live = false;
            networkAvailable = true;
            SlideIndex = 0;
            noticeUntil = 0;
            if (completedOrStarted)
                audience.Shuffle();
            ApplySlide();
            ApplyPresentationMode();
            view.ReturnToSetup();
        }

        void CancelRequest()
        {
            CancelVoiceDownload();
            CancelTranscriptDownload();
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
            CancelTranscriptDownload();
            StopSpeech();
            questionStartsAt = -1;
            floorGrantedAt = -10;
            answerWhileSpeaking = false;
            ReplaceGeneratedAudio(null);
            lastAudio = null;
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
            CancelDeckImport();
            CancelRequest();
            if (voice)
                StopSpeech();
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
