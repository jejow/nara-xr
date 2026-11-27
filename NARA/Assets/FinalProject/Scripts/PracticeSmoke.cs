using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Nara.Practice
{
    public sealed class PracticeSmoke : MonoBehaviour
    {
        const string RoomSwitchTopic = "Uji perpindahan kelas";
        const string RoomSwitchSummary = "Ringkasan tetap tersedia setelah kelas berganti.";
        const string SampleSentence = "Boleh jelaskan contoh yang kamu ajukan, cara mengukurnya, dan bagaimana tim akan memakai hasilnya? ";
        static bool? expectedIf101;
        NaraPractice app;
        string folder;

        public void StartRun(NaraPractice practice)
        {
            app = practice;
            folder = Environment.GetEnvironmentVariable("NARA_SMOKE_DIR");
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(.5f);
            Debug.Log($"NARA_SMOKE room={app.ClassroomName}");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-naraLive") >= 0)
            {
                yield return VerifyLive();
                yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-naraSmokeSwitch") >= 0)
            {
                var viewToSwitch = app.GetComponent<PracticeView>();
                if (!expectedIf101.HasValue)
                {
                    expectedIf101 = !app.IsIf101;
                    viewToSwitch.RestoreDraft(RoomSwitchTopic, RoomSwitchSummary);
                    string choice = expectedIf101.Value ? NaraPractice.IfClassroomName : "Kelas universitas";
                    GameObject.Find(choice).GetComponent<Button>().onClick.Invoke();
                    if (!Check(app.LoadingClassroom, "selecting the other classroom starts a scene load"))
                        yield break;
                    yield return new WaitForSecondsRealtime(30);
                    Check(false, "classroom scene switch completed");
                    yield break;
                }
                var topic = GameObject.Find("Input Judul presentasi").GetComponent<InputField>();
                GameObject.Find("Materi latihan").GetComponent<Button>().onClick.Invoke();
                var summary = GameObject.Find("Input Tujuan dan isi presentasi").GetComponent<InputField>();
                if (!Check(app.IsIf101 == expectedIf101.Value &&
                    topic.text == RoomSwitchTopic && summary.text == RoomSwitchSummary,
                    "the selected classroom loads and preserves the topic and summary"))
                    yield break;
                viewToSwitch.CloseDesktopSection();
            }
            if (!Check(app.sceneTriangles > 0 && app.sceneTriangles <= NaraPractice.SceneTriangleBudget &&
                app.Deck.Count > 0, "scene and deck"))
                yield break;
            string[] original = Occupants();
            if (!Check(app.audience.Profiles.Length == AvatarCatalog.ProfileCount && original != null,
                "24 classroom seats have distinct seated NPCs from the 30-profile catalog"))
                yield break;
            if (!Check(CheckQuestionDraw(), "all 24 seats can ask either question, with distinct speakers per session"))
                yield break;
            if (!Check(CheckVoiceBank(), "all 30 characters have their own 14 bilingual voice clips"))
                yield break;
            if (!Check(app.room.GetComponentsInChildren<TextMesh>(true).Length == 0, "audience view has no floating nameplate"))
                yield break;
            Transform presenter = app.practiceCamera.transform;
            if (!Check(CheckClassroomRows(), "four aligned rows face the front with a clear center aisle"))
                yield break;
            var surfaces = app.room.GetComponentsInChildren<MeshRenderer>();
            int deskTops = Array.FindAll(surfaces, surface =>
                surface.name.StartsWith("Table white trapezoidal top", StringComparison.Ordinal) &&
                surface.sharedMaterial && surface.sharedMaterial.GetTexture("_BumpMap")).Length;
            if (!Check(app.IsIf101
                ? deskTops == 0 && !app.confidenceScreen && !Array.Exists(surfaces, surface =>
                    surface.name.StartsWith("Teacher desk", StringComparison.Ordinal) ||
                    surface.name.StartsWith("Table white trapezoidal top", StringComparison.Ordinal))
                : deskTops == NaraAudience.ClassroomSeatCount,
                "IF 110 has no desks and the university room keeps its 24 Blender desks"))
                yield break;
            var speakingActor = app.audience.HeadAt(0).GetComponentInParent<NaraAvatarPuppet>();
            var mesh = speakingActor.GetComponentInChildren<SkinnedMeshRenderer>();
            Quaternion neutral = speakingActor.Head.localRotation;
            const float sample = 1f / 60;
            for (int i = 0; i < 10; i++)
                speakingActor.Animate(Time.unscaledTime + i * sample, sample, true, .8f, true,
                    presenter.position + presenter.right);
            int jaw = 0;
            while (jaw < mesh.sharedMesh.blendShapeCount && !mesh.sharedMesh.GetBlendShapeName(jaw).EndsWith("JawOpen", StringComparison.OrdinalIgnoreCase))
                jaw++;
            if (!Check(Quaternion.Angle(neutral, speakingActor.Head.localRotation) > .01f &&
                jaw < mesh.sharedMesh.blendShapeCount && mesh.GetBlendShapeWeight(jaw) > 0, "seated actor moves and drives jaw blend shape"))
                yield break;
            if (!Check(CheckMovement(), "individual idle schedules, all ten listening actions and five spoken gestures move the rig"))
                yield break;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-naraMotionTiming") >= 0)
                MeasureAudienceMotion();
            var view = app.GetComponent<PracticeView>();
            if (!Check(CheckSettings(view), "laptop mode, language, voice and timing preferences restore after returning to setup"))
                yield break;
            view.MoveFocus(false);
            GameObject firstControl = EventSystem.current.currentSelectedGameObject;
            if (!Check(firstControl && firstControl.GetComponent<InputField>() &&
                firstControl.GetComponent<Outline>().enabled, "keyboard reaches the topic and shows its focus"))
                yield break;
            view.MoveFocus(false);
            if (!Check(EventSystem.current.currentSelectedGameObject != firstControl, "keyboard focus moves to the next control"))
                yield break;
            EventSystem.current.SetSelectedGameObject(null);
            var startPanel = (RectTransform)GameObject.Find("Mulai latihan presentasi").transform;
            float coverage = startPanel.rect.width * startPanel.rect.height /
                (PracticeView.DesktopSize.x * PracticeView.DesktopSize.y);
            if (!Check(coverage < .21f && !GameObject.Find("Pengaturan materi") && !GameObject.Find("Pengaturan sesi") &&
                !GameObject.Find("Pilih karakter") && GameObject.Find(NaraPractice.IfClassroomName) && GameObject.Find("Kelas universitas"),
                "desktop setup has both classroom choices and access to settings"))
                yield break;
            GameObject.Find("Materi latihan").GetComponent<Button>().onClick.Invoke();
            if (!Check(GameObject.Find("Pengaturan materi") && !GameObject.Find("Mulai latihan presentasi"),
                "material panel opens from the setup thumbnail"))
                yield break;
            var summaryField = GameObject.Find("Input Tujuan dan isi presentasi").GetComponent<InputField>();
            string summaryDraft = summaryField.text;
            summaryField.text = "Ringkasan uji tampilan.";
            view.CloseDesktopSection();
            GameObject.Find("Pengaturan").GetComponent<Button>().onClick.Invoke();
            if (!Check(GameObject.Find("Pengaturan sesi") && !GameObject.Find("Pengaturan materi"),
                "session preferences open independently of the material panel"))
                yield break;
            view.CloseDesktopSection();
            GameObject.Find("Materi latihan").GetComponent<Button>().onClick.Invoke();
            if (!Check(summaryField.text == "Ringkasan uji tampilan.", "closing and reopening the material panel preserves a draft"))
                yield break;
            summaryField.text = summaryDraft;
            view.CloseDesktopSection();
            yield return Frame("01-setup");
            if (app.IsIf101)
            {
                var preview = GameObject.Find("Lihat PPT").GetComponent<Button>();
                preview.onClick.Invoke();
                Transform screen = app.slideScreen.transform;
                if (!Check(app.ViewingProjection && app.slideScreen.enabled && app.slideScreen.sharedMaterial.mainTexture == app.Slide &&
                    Vector3.Dot(-screen.forward, presenter.position - screen.position) > 2f &&
                    Vector3.Dot(presenter.forward, (screen.position - presenter.position).normalized) > .99f,
                    "the IF 110 setup button frames the lit PPT from inside the room"))
                    yield break;
                yield return Frame("01-if110-ppt");
                preview.onClick.Invoke();
                if (!Check(!app.ViewingProjection, "the PPT button returns to the audience view"))
                    yield break;
            }
            view.SetXr(true);
            Canvas ui = view.ControlsCanvas;
            if (!Check(ui.renderMode == RenderMode.WorldSpace && ui.transform.parent == app.controlsAnchor &&
                Mathf.Approximately(((RectTransform)ui.transform).rect.width * ui.transform.localScale.x, 1.2f) &&
                GameObject.Find("Persiapan headset") && !GameObject.Find("Panel desktop") && !GameObject.Find("Avatar headset"),
                "headset setup keeps settings and removes avatar selection"))
                yield break;
            yield return Frame("01-headset-setup");
            view.SetXr(false);
            if (!Check(ui.renderMode == RenderMode.ScreenSpaceOverlay && GameObject.Find("Panel desktop"),
                "returning from headset restores the desktop layout"))
                yield break;
            if (!Check(StartWithRearQuestion(false), "first speaker can come from a rear row"))
                yield break;
            AvatarProfile host = app.audience.ProfileAt(app.HostSeat);
            if (!Check(app.GreetingText.Contains(host.label) &&
                app.voice.clip == NaraPractice.VoiceClip(host, "intro", "id"),
                "a randomly seated host greets the presenter in Indonesian"))
                yield break;
            app.ChangeSlide(1);
            if (!Check(app.SlideIndex == Mathf.Min(1, app.Deck.Count - 1), "next slide"))
                yield break;
            if (!Check(app.slideScreen.enabled && app.slideScreen.sharedMaterial.mainTexture == app.Slide &&
                (app.IsIf101 || (app.confidenceScreen &&
                    app.confidenceScreen.sharedMaterial == app.slideScreen.sharedMaterial &&
                    Vector3.Dot(-app.confidenceScreen.transform.forward,
                        presenter.position - app.confidenceScreen.transform.position) > 0)),
                "the updated slide is visible on the projection and on the lectern monitor where fitted"))
                yield break;
            yield return null;
            if (!Check(GameObject.Find("Kendali presentasi 3D") && !GameObject.Find("Kontrol latihan"),
                "room presentation uses the compact controls"))
                yield break;
            if (app.IsIf101)
            {
                var projection = GameObject.Find("Lihat PPT").GetComponent<Button>();
                projection.onClick.Invoke();
                yield return Frame("02-if110-ppt");
                projection.onClick.Invoke();
            }
            app.TogglePause();
            float elapsed = app.Session.Elapsed;
            yield return new WaitForSecondsRealtime(.2f);
            if (!Check(app.Session.Paused && Mathf.Approximately(elapsed, app.Session.Elapsed), "paused timer"))
                yield break;
            app.TogglePause();
            yield return Frame("02-presentation");
            view.SetXr(true);
            yield return Frame("02-headset-presentation");
            view.SetXr(false);
            float greetingEnd = Time.realtimeSinceStartup + 8f;
            while (!string.IsNullOrEmpty(app.GreetingText) && Time.realtimeSinceStartup < greetingEnd)
                yield return null;
            if (!Check(string.IsNullOrEmpty(app.GreetingText), "greeting frees the floor before the presentation timer runs"))
                yield break;
            int firstSpeaker = app.QuestionerSeat;
            speakingActor = app.audience.HeadAt(firstSpeaker).GetComponentInParent<NaraAvatarPuppet>();
            app.Session.Tick(PracticeSession.Duration);
            yield return new WaitForSecondsRealtime(.74f);
            app.Answer("Terlalu cepat", false);
            var permission = GameObject.Find("Persilakan bertanya");
            if (!Check(app.Session.Phase == PracticePhase.FloorRequest && app.Session.MainQuestions == 0 &&
                app.Session.Attempts == 0 && !app.voice.isPlaying && !view.SpeechBubbleVisible && permission &&
                speakingActor.RequestHandHeld && speakingActor.FloorWrist.position.y > speakingActor.Head.position.y - .19f,
                "hand goes up and speech and answers wait for permission"))
                yield break;
            yield return new WaitForSecondsRealtime(.55f);
            if (!Check(app.Session.Phase == PracticePhase.FloorRequest && !app.voice.isPlaying &&
                speakingActor.RequestHandHeld && speakingActor.FloorWrist.position.y > speakingActor.Head.position.y - .19f,
                "recorded hold keeps the hand raised until permission"))
                yield break;
            yield return Frame("03-hand-raised");
            view.SetXr(true);
            RectTransform conversation = (RectTransform)GameObject.Find("Tanya jawab headset").transform;
            var corners = new Vector3[4];
            conversation.GetWorldCorners(corners);
            float panelTop = app.practiceCamera.WorldToViewportPoint(corners[1]).y;
            float actorHead = app.practiceCamera.WorldToViewportPoint(speakingActor.Head.position).y;
            if (!Check(panelTop < actorHead, "headset question panel sits below the speaking avatar's face"))
                yield break;
            yield return Frame("03-headset-hand-raised");
            view.SetXr(false);
            float raisedHeight = speakingActor.FloorWrist.position.y;
            permission.GetComponent<Button>().onClick.Invoke();
            app.GrantFloor();
            app.Answer("", true);
            yield return new WaitForSecondsRealtime(.65f);
            if (!Check(app.Session.Phase == PracticePhase.Asking && app.Session.MainQuestions == 1 &&
                app.Session.Skips == 0 && app.voice.isPlaying &&
                app.QuestionerSeat == firstSpeaker &&
                app.voice.clip == NaraPractice.VoiceClip(app.audience.ProfileAt(firstSpeaker), "question_1", "id") &&
                speakingActor.RequestHandAtRest && speakingActor.FloorWrist.position.y < raisedHeight - .13f,
                "grant lowers hand, speaks once and holds the answer controls"))
                yield break;
            GameObject bubble = GameObject.Find("Dialog NPC");
            Text[] lines = bubble ? bubble.GetComponentsInChildren<Text>() : Array.Empty<Text>();
            if (!Check(view.SpeechBubbleVisible && app.SpeakingSeat == firstSpeaker &&
                Array.Exists(lines, label => label.text == app.audience.ProfileAt(firstSpeaker).label) &&
                Array.Exists(lines, label => label.text == app.Session.Question) &&
                SpeechBounds(bubble, false).yMin > actorHead &&
                !GameObject.Find("Percakapan") && GameObject.Find("Kendali saat NPC bicara"),
                "the active speaker shows their own name and question above their seat"))
                yield break;
            yield return Frame("03-dialog");
            if (!Check(CheckCaptionEdges(view, speakingActor.Head, false), "desktop speech fits all four viewport edges and falls back behind a real wall"))
                yield break;
            view.SetXr(true);
            if (!Check(CheckCaptionEdges(view, speakingActor.Head, true), "XR speech fits the viewport, clears corner obstructions and respects a real wall"))
                yield break;
            yield return Frame("03-headset-dialog");
            view.SetXr(false);
            if (!Check(CheckLongCaption(view), "every word of a long spoken line appears on measured pages"))
                yield break;
            yield return WaitForAnswer();
            if (!Check(app.Session.Phase == PracticePhase.Question && !app.voice.isPlaying && !view.SpeechBubbleVisible &&
                app.CanReplay && GameObject.Find("Percakapan"),
                "answer opens and the bubble clears after the first question ends"))
                yield break;
            app.Replay();
            if (!Check(app.Session.Phase == PracticePhase.Asking && app.Session.MainQuestions == 1,
                "replay holds the floor without counting another question"))
                yield break;
            yield return WaitForAnswer();
            app.Answer("Mahasiswa membutuhkan tempat diskusi.", false);
            if (!Check(app.Session.Phase == PracticePhase.FloorRequest && app.QuestionerSeat != firstSpeaker &&
                app.Session.MainQuestions == 1 && app.Session.Attempts == 1, "second audience seat requests the floor"))
                yield break;
            app.GrantFloor();
            yield return new WaitForSecondsRealtime(.55f);
            if (!Check(app.voice.isPlaying &&
                app.voice.clip == NaraPractice.VoiceClip(app.audience.ProfileAt(app.QuestionerSeat), "question_2", "id") &&
                Vector3.Distance(app.voice.transform.position,
                app.audience.HeadAt(app.QuestionerSeat).position) < .04f, "voice follows the granted seat"))
                yield break;
            yield return WaitForAnswer();
            app.Answer("Kami mengukur waktu pencarian ruang.", false);
            if (!Check(app.Session.Phase == PracticePhase.Review && !app.Session.FollowUpAsked &&
                app.Session.Attempts == 2 && app.CanReplay, "offline feedback and playback"))
                yield break;
            yield return Frame("04-reflection");

            app.ResetPractice();
            string[] next = Occupants();
            if (!Check(next != null && !string.Equals(string.Join(",", original), string.Join(",", next)),
                "a new practice reshuffles the full classroom"))
                yield break;
            app.StartPractice(app.Deck.Topic, app.Deck.Summary, "en", true, false, false);
            host = app.audience.ProfileAt(app.HostSeat);
            if (!Check(app.GreetingText.Contains(host.label) &&
                app.voice.clip == NaraPractice.VoiceClip(host, "intro", "en"),
                "the random host speaks English in the laptop session"))
                yield break;
            if (!Check(app.Baseline && !app.room.activeSelf && app.voice.isActiveAndEnabled, "laptop baseline keeps speech available"))
                yield break;
            yield return Frame("05-laptop");
            app.SetNetworkAvailable(false);
            if (!Check(!app.Live && app.FallbackCount == 1 && app.SpeechText.StartsWith("We lost the connection"),
                "local disconnect immediately plays the English line"))
                yield break;
            app.ResetPractice();

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-naraMock") >= 0)
            {
                if (!Check(StartWithRearQuestion(true), "mock session picks a rear-row questioner separately from its host"))
                    yield break;
                app.FinishPresentation();
                yield return WaitForReply();
                if (!Check(app.Live && app.Mock && app.Session.Phase == PracticePhase.FloorRequest &&
                    app.QuestionerSeat != app.HostSeat && app.Session.MainQuestions == 0,
                    "mock HTTP prepares a question from a random NPC before it speaks"))
                    yield break;
                app.GrantFloor();
                yield return WaitForAnswer();
                app.Answer("Ruang diskusi penuh pada jam praktikum.", false);
                yield return WaitForReply();
                if (!Check(app.Session.Phase == PracticePhase.FloorRequest && app.Session.MainQuestions == 1, "mock HTTP second floor request"))
                    yield break;
                int secondSpeaker = app.QuestionerSeat;
                app.GrantFloor();
                yield return WaitForAnswer();
                app.Answer("Kami menghitung durasi peminjaman ruang.", false);
                yield return WaitForReply();
                if (!Check(app.Session.Phase == PracticePhase.FloorRequest && app.Session.IsFollowUp &&
                    app.QuestionerSeat == secondSpeaker && app.Session.Question.Contains("Kami menghitung durasi"),
                    "grounded follow-up requests the floor for the same speaker"))
                    yield break;
                app.GrantFloor();
                yield return WaitForAnswer();
                if (!Check(app.Session.Phase == PracticePhase.Question && !app.CanReplay,
                    "text-only follow-up opens the answer without an audio clip"))
                    yield break;
                yield return Frame("06-follow-up");
                app.Answer("Kami membandingkan waktu sebelum dan sesudah.", false);
                yield return WaitForReply();
                if (!Check(app.Session.Phase == PracticePhase.Review && app.Session.MainQuestions == 2 && app.Session.Attempts == 3,
                    "finished after two main questions and one follow-up"))
                    yield break;
                app.ResetPractice();
                app.StartPractice(app.Deck.Topic, app.Deck.Summary, "id", false, true, false);
                app.FinishPresentation();
                app.SetNetworkAvailable(false);
                yield return new WaitForSecondsRealtime(.45f);
                if (!Check(!app.Live && app.Session.Phase == PracticePhase.FloorRequest && app.Session.MainQuestions == 0 &&
                    !app.Mock, "late cloud response discarded, local question still waits for permission"))
                    yield break;
                app.GrantFloor();
                app.ResetPractice();
                yield return new WaitForSecondsRealtime(.65f);
                if (!Check(app.Session.Phase == PracticePhase.Setup && !app.voice.isPlaying,
                    "leaving the session cancels the pending spoken question"))
                    yield break;
            }
            if (!Check(CheckQuestionReader(view), "a long question can be reread page by page from the desktop and XR answer panels"))
                yield break;
            string previewDirectory = Environment.GetEnvironmentVariable("NARA_MOTION_FRAMES");
            if (!string.IsNullOrEmpty(previewDirectory))
                yield return RecordPreview(previewDirectory);
            Debug.Log("NARA_SMOKE PASS");
            Exit(0);
        }

        bool CheckMovement()
        {
            var initial = new HashSet<NaraAvatarPuppet.ListeningMotion>();
            for (int seat = 0; seat < app.audience.SeatCount; seat++)
                initial.Add(app.audience.HeadAt(seat).GetComponentInParent<NaraAvatarPuppet>().ListeningAction);
            var actor = app.audience.HeadAt(0).GetComponentInParent<NaraAvatarPuppet>();
            int profile = Array.IndexOf(app.audience.Profiles, app.audience.ProfileAt(0));
            var listening = new HashSet<NaraAvatarPuppet.ListeningMotion>();
            var spoken = new HashSet<NaraAvatarPuppet.SpeakingMotion>();
            var right = new Bounds(actor.RightWrist.position, Vector3.zero);
            var left = new Bounds(actor.LeftWrist.position, Vector3.zero);
            try
            {
                const float step = 1f / 15;
                float start = Time.unscaledTime;
                actor.SetMotionProfile(profile, start);
                for (int i = 0; i < 1900; i++)
                {
                    int phraseFrame = Mathf.Max(0, i - 1500);
                    bool talking = i >= 1500 && phraseFrame % 80 < 70;
                    AudienceLine cue = (AudienceLine)(1 + phraseFrame / 80);
                    actor.Animate(start + i * step, step, true, talking ? .72f : 0, talking,
                        app.practiceCamera.transform.position, line: cue, lineProgress: phraseFrame % 80 / 70f);
                    if (talking)
                        spoken.Add(actor.SpeakingAction);
                    else
                    {
                        listening.Add(actor.ListeningAction);
                        right.Encapsulate(actor.RightWrist.position);
                        left.Encapsulate(actor.LeftWrist.position);
                    }
                }
                Debug.Log($"NARA_MOTION startup={initial.Count} listening={listening.Count} spoken={spoken.Count} wristHeightRange={right.size.y:F2}/{left.size.y:F2}");
                for (int seat = 0; seat < app.audience.SeatCount; seat++)
                    if (app.audience.HeadAt(seat).GetComponentInParent<NaraAvatarPuppet>().UsesDeskMotion == app.IsIf101)
                        return false;
                return initial.Count >= 4 && listening.Count == 10 && spoken.Count == 5 &&
                    Mathf.Min(left.size.y, right.size.y) > .09f && Mathf.Max(left.size.y, right.size.y) > .19f;
            }
            finally
            {
                actor.SetMotionProfile(profile, Time.unscaledTime, 0, 1);
            }
        }

        void MeasureAudienceMotion()
        {
            var actors = new NaraAvatarPuppet[app.audience.SeatCount];
            var profiles = new int[actors.Length];
            for (int seat = 0; seat < actors.Length; seat++)
            {
                actors[seat] = app.audience.HeadAt(seat).GetComponentInParent<NaraAvatarPuppet>();
                profiles[seat] = Array.IndexOf(app.audience.Profiles, app.audience.ProfileAt(seat));
            }
            var samples = new double[180];
            var eyes = app.practiceCamera.transform.position;
            float start = Time.unscaledTime;
            for (int frame = -20; frame < samples.Length; frame++)
            {
                float t = (frame + 20) / 60f;
                long before = System.Diagnostics.Stopwatch.GetTimestamp();
                for (int seat = 0; seat < actors.Length; seat++)
                    actors[seat].Animate(start + t, 1f / 60, true, seat == 1 ? .65f : 0, seat == 1,
                        eyes, seat == 6 && t < 1.5f, line: AudienceLine.FirstQuestion, lineProgress: t / 3.4f);
                if (frame >= 0)
                    samples[frame] = (System.Diagnostics.Stopwatch.GetTimestamp() - before) * 1000.0 /
                        System.Diagnostics.Stopwatch.Frequency;
            }
            for (int seat = 0; seat < actors.Length; seat++)
                actors[seat].SetMotionProfile(profiles[seat], Time.unscaledTime, seat, 1);
            Array.Sort(samples);
            Debug.Log($"NARA_MOTION_TIMING seats={actors.Length} frames={samples.Length} " +
                $"animationOnlyMedianMs={samples[90]:F2} animationOnlyP95Ms={samples[170]:F2}");
        }

        Rect SpeechBounds(GameObject bubble, bool xr)
        {
            var corners = new Vector3[4];
            Vector2 min = Vector2.one * float.MaxValue, max = Vector2.one * float.MinValue;
            foreach (var graphic in bubble.GetComponentsInChildren<Graphic>())
            {
                graphic.rectTransform.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector2 p = xr ? (Vector2)app.practiceCamera.WorldToViewportPoint(corner) :
                        (Vector2)app.practiceCamera.ScreenToViewportPoint(RectTransformUtility.WorldToScreenPoint(null, corner));
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        bool CheckCaptionEdges(PracticeView view, Transform head, bool xr)
        {
            Camera camera = app.practiceCamera;
            Transform eyes = camera.transform;
            Vector3 face = head.position + .07f * Vector3.up, origin = eyes.position;
            Quaternion rotation = eyes.rotation;
            GameObject prop = null;
            try
            {
                Vector2[] places = { new(.045f, .42f), new(.955f, .42f), new(.13f, .925f), new(.87f, .925f) };
                foreach (Vector2 place in places)
                {
                    eyes.rotation = Quaternion.LookRotation(face - origin);
                    eyes.rotation = Quaternion.FromToRotation(camera.ViewportPointToRay(place).direction,
                        (face - origin).normalized) * eyes.rotation;
                    view.Refresh();
                    if (!view.SpeechBubbleVisible)
                    {
                        Debug.LogWarning($"NARA_CAPTION edge hidden at {place} xr={xr}");
                        return false;
                    }
                    Rect b = SpeechBounds(GameObject.Find("Dialog NPC"), xr);
                    if (b.xMin < .01f || b.xMax > .99f || b.yMin < .08f || b.yMax > .99f)
                    {
                        Debug.LogWarning($"NARA_CAPTION edge bounds {b} at {place} xr={xr}");
                        return false;
                    }
                }
                eyes.rotation = rotation;
                view.Refresh();
                if (xr && view.SpeechBubbleVisible)
                {
                    var corners = new Vector3[4];
                    ((RectTransform)GameObject.Find("Dialog NPC").transform).GetWorldCorners(corners);
                    prop = new GameObject("Smoke corner obstruction");
                    prop.transform.position = Vector3.Lerp(origin, Vector3.Lerp(corners[1], corners[2], .12f), .82f);
                    prop.AddComponent<BoxCollider>().size = new Vector3(.08f, .08f, .08f);
                    Physics.SyncTransforms();
                    view.Refresh();
                    if (view.SpeechBubbleVisible && camera.WorldToViewportPoint(GameObject.Find("Dialog NPC").transform.position).z >=
                        camera.WorldToViewportPoint(prop.transform.position).z)
                        return false;
                    prop.SetActive(false);
                }
                // Cross the actual rear wall and look back along the same line of sight.
                eyes.position = face + Vector3.ProjectOnPlane(face - origin, Vector3.up).normalized * 14;
                eyes.rotation = Quaternion.LookRotation(face - eyes.position);
                view.Refresh();
                return !view.SpeechBubbleVisible && GameObject.Find(xr ? "Tanya jawab headset" : "Percakapan");
            }
            finally
            {
                if (prop)
                {
                    prop.SetActive(false);
                    Destroy(prop);
                }
                eyes.SetPositionAndRotation(origin, rotation);
                view.Refresh();
            }
        }

        bool CheckLongCaption(PracticeView view)
        {
            var bubble = GameObject.Find("Dialog NPC");
            if (!bubble)
                return false;
            string line = string.Concat(SampleSentence, SampleSentence, SampleSentence, SampleSentence, SampleSentence, SampleSentence).TrimEnd();
            var text = bubble.GetComponentsInChildren<Text>()[1];
            var pages = new List<string>();
            try
            {
                for (int i = 0; i <= line.Length; i++)
                {
                    view.SetSpeechPage(line, 302, i / (float)line.Length);
                    if (text.preferredHeight > text.rectTransform.rect.height || string.IsNullOrEmpty(text.text))
                        return false;
                    if (pages.Count == 0 || pages[^1] != text.text)
                        pages.Add(text.text);
                }
                return pages.Count > 2 && string.Join(" ", pages).Equals(line, StringComparison.Ordinal);
            }
            finally
            {
                view.Refresh();
            }
        }

        bool CheckQuestionReader(PracticeView view)
        {
            string line = string.Concat(SampleSentence, SampleSentence, SampleSentence, SampleSentence).TrimEnd();
            app.Session.Start("Teks panjang", "", Array.Empty<DeckPage>());
            app.Session.BeginQuestions();
            app.Session.ApplyReply("question", line);
            app.Session.GrantFloor();
            app.Session.OpenAnswer();
            try
            {
                view.Refresh();
                var next = GameObject.Find("›")?.GetComponent<Button>();
                if (!next || !next.interactable)
                    return false;
                Text prompt = Array.Find(GameObject.Find("Percakapan").GetComponentsInChildren<Text>(),
                    text => text.text.StartsWith("Boleh jelaskan", StringComparison.Ordinal));
                var pages = new List<string>();
                for (int i = 0; i < 30 && prompt; i++)
                {
                    pages.Add(prompt.text);
                    if (!next.interactable)
                        break;
                    next.onClick.Invoke();
                }
                if (pages.Count < 2 || string.Join(" ", pages) != line || !view.TurnQuestionPage(-1))
                    return false;
                view.SetXr(true);
                bool moved = view.TurnQuestionPage(1) && view.TurnQuestionPage(-1);
                return moved && Array.Exists(GameObject.Find("Tanya jawab headset").GetComponentsInChildren<Text>(),
                    text => text.text.Contains("Baca teks"));
            }
            finally
            {
                view.SetXr(false);
                app.ResetPractice();
            }
        }

        IEnumerator RecordPreview(string directory)
        {
            Directory.CreateDirectory(directory);
            app.ResetPractice();
            // Choose two visible seats for the recording through the normal session draw.
            var state = Random.state;
            for (int seed = 0; seed < 128; seed++)
            {
                Random.InitState(seed);
                int host = Random.Range(0, app.audience.SeatCount);
                var (asker, _) = app.audience.DrawQuestionSeats();
                float a = app.practiceCamera.WorldToViewportPoint(app.audience.HeadAt(host).position).x;
                float b = app.practiceCamera.WorldToViewportPoint(app.audience.HeadAt(asker).position).x;
                if (host < 12 && asker < 12 && a is > .19f and < .81f && b is > .19f and < .81f && Mathf.Abs(a - b) > .22f)
                {
                    Random.InitState(seed);
                    break;
                }
            }
            app.StartPractice(app.Deck.Topic, app.Deck.Summary, "id", false, false, false);
            Random.state = state;
            bool requested = false, granted = false;
            int frame = 0;
            float start = Time.realtimeSinceStartup, next = start, request = 0, answeredAt = -1;
            using var times = new StreamWriter(Path.Combine(directory, "times.txt"));
            using var voices = new StreamWriter(Path.Combine(directory, "voices.tsv"));
            using var capture = new PlayerCapture(app, 960, 600);
            AudioClip previousClip = null;
            while (Time.realtimeSinceStartup - start < 22)
            {
                yield return null;
                float now = Time.realtimeSinceStartup;
                if (now < next)
                    continue;
                if (!requested && string.IsNullOrEmpty(app.GreetingText) && now - start > 4.2f)
                {
                    app.FinishPresentation();
                    requested = true;
                    request = now;
                }
                if (requested && !granted && now - request > 1.45f)
                {
                    app.GrantFloor();
                    granted = true;
                }
                // Record actual player frames at their observed times, including slow frames.
                times.WriteLine((now - start).ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
                AudioClip clip = app.voice.isPlaying ? app.voice.clip : null;
                if (clip && clip != previousClip && app.SpeakingSeat >= 0)
                {
                    string cue = (now - start - app.voice.time).ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                    voices.WriteLine($"{cue}\t{app.audience.ProfileAt(app.SpeakingSeat).id}\t{clip.name}");
                }
                previousClip = clip;
                capture.Save(Path.Combine(directory, $"{frame++:D4}.jpg"));
                next = now + 1f / 24;
                if (granted && app.Session.Phase == PracticePhase.Question)
                {
                    if (answeredAt < 0)
                        answeredAt = now;
                    if (now - answeredAt > .85f)
                        break;
                }
            }
            Debug.Log($"NARA_PREVIEW captured {frame} frames in {app.ClassroomName}");
        }

        IEnumerator VerifyLive()
        {
            string topic = "Peminjaman sepeda kampus";
            const string summary = "Mahasiswa meminjam sepeda melalui kartu mahasiswa. Tim menguji dua stasiun selama tiga minggu.";
            string expectedProvider = Environment.GetEnvironmentVariable("NARA_SMOKE_VOICE_PROVIDER");
            bool requireProgressive = Environment.GetEnvironmentVariable("NARA_SMOKE_REQUIRE_STREAM") == "1";
            string input = Environment.GetEnvironmentVariable("NARA_SMOKE_PRESENTATION");
            if (!string.IsNullOrEmpty(input))
            {
                if (!Check(Application.isBatchMode && !string.IsNullOrEmpty(folder) && app.CanImportDeck,
                    "live import verification uses isolated local practice data"))
                    yield break;
                app.Deck.UseSmokeFolder(Path.Combine(folder, "Practice"));
                app.ReloadDeck();
                app.ImportDeck(input);
                float deadline = Time.realtimeSinceStartup + 146;
                while (app.ImportingDeck && Time.realtimeSinceStartup < deadline)
                    yield return null;
                if (!Check(app.Deck.HasImportedFile && app.Deck.Count == 4 && app.Deck.Pages[1].text.Contains("41 detik"),
                    "PowerPoint imports with all four rendered slides and extracted evidence"))
                    yield break;
                app.ReloadDeck();
                app.PreviewSlide(1);
                if (!Check(app.SlideIndex == 1 && app.Slide && app.Slide.width > 800,
                    "an imported slide can be previewed after reloading the local archive"))
                    yield break;
                GameObject.Find("Materi latihan").GetComponent<Button>().onClick.Invoke();
                yield return Frame("imported-material");
                app.GetComponent<PracticeView>().CloseDesktopSection();
                topic = app.Deck.Topic;
            }
            app.StartPractice(topic, summary, "id", false, true, false);
            if (!string.IsNullOrEmpty(input))
            {
                app.ChangeSlide(1);
                yield return null;
                GameObject.Find("Tulis penjelasan").GetComponent<Button>().onClick.Invoke();
                var text = GameObject.Find("Input Tulis apa yang kamu jelaskan kepada audiens").GetComponent<InputField>();
                text.text = "Dalam presentasi ini aku menjelaskan bahwa loker dibuka memakai kartu mahasiswa. Uji pada dua stasiun menunjukkan waktu buka loker turun dari 96 ke 41 detik. Tim belum menguji apakah kartu berfungsi saat hujan.";
                GameObject.Find("Selesai").GetComponent<Button>().onClick.Invoke();
                TurnRequest context = app.Session.Request("id", null);
                if (!Check(context.slides.Length == 4 && context.presentedPages.Length == 2 &&
                    context.writtenPresentation.Contains("saat hujan"),
                    "session combines visited slides with the typed explanation"))
                    yield break;
                yield return Frame("presentation-input");
            }
            string speechFixture = Environment.GetEnvironmentVariable("NARA_SMOKE_PRESENTATION_PCM");
            if (!string.IsNullOrEmpty(speechFixture))
            {
                byte[] pcm = File.ReadAllBytes(speechFixture);
                app.BufferPresentation(pcm);
                float preparedUntil = Time.realtimeSinceStartup + 22f;
                while (app.PreparedPresentationBytes < pcm.Length && Time.realtimeSinceStartup < preparedUntil)
                    yield return null;
                if (!Check(app.PreparedPresentationBytes == pcm.Length && app.Session.Presentation.Length > 0,
                    "local narration is transcribed during the presentation"))
                    yield break;
            }
            float started = Time.realtimeSinceStartup;
            app.FinishPresentation();
            yield return WaitForReply();
            int first = app.QuestionerSeat;
            string question = app.Session.Question;
            if (!Check(app.Live && !app.Mock && app.FallbackCount == 0 && app.Session.Phase == PracticePhase.FloorRequest &&
                app.Session.MainQuestions == 0 && !app.voice.isPlaying &&
                Array.Exists(new[] { "sepeda", "stasiun", "peminjam", "kartu", "uji coba", "tiga minggu", "loker", "hujan", "41", "96" },
                    term => question.Contains(term, StringComparison.OrdinalIgnoreCase)),
                "live relay asks about the supplied topic and waits for the first seat to be granted the floor"))
                yield break;
            Debug.Log($"NARA_CAPTION_TIME first={Time.realtimeSinceStartup - started:F2}s");
            bool preparing = app.SpeechPending;
            float grantedAt = Time.realtimeSinceStartup;
            app.GrantFloor();
            yield return new WaitForSecondsRealtime(.65f);
            if (preparing && app.SpeechPending)
            {
                if (!Check(app.Session.Phase == PracticePhase.Question && !app.CanReplay,
                    "answer is available while the first character voice is still rendering"))
                    yield break;
                yield return Frame("answer-before-audio");
            }
            yield return WaitForVoice(first);
            if (!Check(app.SpeakingSeat == first && app.SpeechText == question &&
                app.voice.clip != NaraPractice.VoiceClip(app.audience.ProfileAt(first), "question_1", "id"),
                "first NPC plays generated speech for the live question"))
                yield break;
            Debug.Log($"NARA_VOICE_START first={app.VoiceStartedAt - grantedAt:F2}s provider={app.StreamVoiceProvider}");
            if (!string.IsNullOrEmpty(expectedProvider) &&
                !Check(app.StreamVoiceProvider == expectedProvider, "first NPC uses the expected streaming voice provider"))
                yield break;
            if (requireProgressive)
            {
                if (!Check(app.PlayingVoiceStream && app.SpeechPending && !app.CanReplay,
                    "Unity plays the first PCM frames while the HTTP response is still open"))
                    yield break;
                float captionAtStart = app.SubtitleProgress;
                yield return new WaitForSecondsRealtime(.22f);
                if (!Check(app.PlayingVoiceStream && app.SubtitleProgress > captionAtStart,
                    "the subtitle clock advances with streamed sample playback"))
                    yield break;
            }
            yield return Frame("live-question");
            yield return WaitForAnswer();

            started = Time.realtimeSinceStartup;
            app.Answer("Kami membandingkan jumlah peminjaman per jam di dua stasiun dan mencatat sepeda yang rusak.", false);
            yield return WaitForReply();
            int second = app.QuestionerSeat;
            if (!Check(app.Live && !app.Mock && app.FallbackCount == 0 && app.Session.Phase == PracticePhase.FloorRequest &&
                second != first && app.Session.MainQuestions == 1 && app.Session.Attempts == 1 &&
                !string.Equals(question, app.Session.Question, StringComparison.OrdinalIgnoreCase),
                "typed answer leads to a distinct live question from a second randomly selected NPC"))
                yield break;
            Debug.Log($"NARA_CAPTION_TIME typed={Time.realtimeSinceStartup - started:F2}s");
            while (app.SpeechPending)
                yield return null;
            app.GrantFloor();
            yield return WaitForVoice(second);
            if (!Check(app.SpeakingSeat == second && app.Session.Phase == PracticePhase.Question &&
                app.voice.clip != NaraPractice.VoiceClip(app.audience.ProfileAt(second), "question_2", "id"),
                "the player can answer even if the second NPC voice is already playing"))
                yield break;
            if (string.IsNullOrEmpty(speechFixture))
            {
                app.Answer("", true);
                yield return WaitForReply();
            }
            else
            {
                started = Time.realtimeSinceStartup;
                app.AnswerWithRecording(File.ReadAllBytes(speechFixture), started);
                yield return WaitForReply();
                if (!Check(app.Live && app.Session.Request("id", null).history[1].answer.Length > 0,
                    "recorded answer is transcribed and used in live dialogue"))
                    yield break;
                Debug.Log($"NARA_CAPTION_TIME mic={Time.realtimeSinceStartup - started:F2}s");
                if (app.Session.Phase == PracticePhase.FloorRequest)
                {
                    app.GrantFloor();
                    yield return WaitForVoice(second);
                    app.Answer("", true);
                    yield return WaitForReply();
                }
            }
            if (!Check(app.Live && !app.Mock && app.FallbackCount == 0 && app.Session.Phase == PracticePhase.Review &&
                !string.IsNullOrWhiteSpace(app.Session.Suggestion),
                "closing feedback text is available as soon as the relay replies"))
                yield break;
            yield return WaitForVoice(app.HostSeat);
            if (!Check(app.SpeakingSeat == app.HostSeat,
                "live closing feedback is played by the host"))
                yield break;
            if (requireProgressive)
            {
                float finishedBy = Time.realtimeSinceStartup + 14f;
                while (app.SpeakingSeat >= 0 && Time.realtimeSinceStartup < finishedBy)
                    yield return null;
                if (!Check(!app.SpeechPending && !app.PlayingVoiceStream && app.SpeakingSeat < 0 && app.CanReplay,
                    "streamed playback drains and ends at the last PCM frame"))
                    yield break;
                app.Replay();
                if (!Check(app.SpeakingSeat == app.HostSeat && !app.PlayingVoiceStream,
                    "the completed voice can be replayed without another download"))
                    yield break;
            }
            Debug.Log("NARA_SMOKE PASS");
            Exit(0);
        }

        IEnumerator WaitForVoice(int seat)
        {
            float until = Time.realtimeSinceStartup + 28f;
            while (app.SpeakingSeat != seat && (app.SpeechPending || app.Session.Phase == PracticePhase.Asking) &&
                Time.realtimeSinceStartup < until)
                yield return null;
        }

        IEnumerator WaitForReply()
        {
            float end = Time.realtimeSinceStartup + NaraPractice.TextTurnDeadline + 1;
            while (app.Session.Phase == PracticePhase.Waiting && Time.realtimeSinceStartup < end)
                yield return null;
        }

        string[] Occupants()
        {
            if (app.audience.SeatCount != NaraAudience.ClassroomSeatCount)
                return null;
            var names = new string[app.audience.SeatCount];
            var unique = new HashSet<string>();
            for (int i = 0; i < names.Length; i++)
            {
                var head = app.audience.HeadAt(i);
                names[i] = app.audience.ProfileAt(i).id;
                if (!head || !head.gameObject.activeInHierarchy || !unique.Add(names[i]))
                    return null;
            }
            return names;
        }

        bool CheckClassroomRows()
        {
            Transform[] seats = app.audience.seats;
            Vector3 right = (seats[1].position - seats[0].position).normalized;
            Vector3 forward = seats[0].forward;
            for (int start = 0; start < seats.Length; start += NaraAudience.SeatsPerRow)
            {
                if (Vector3.Distance(seats[start + 2].position, seats[start + 3].position) < 1.9f)
                    return false;
                for (int column = 0; column < NaraAudience.SeatsPerRow; column++)
                {
                    Transform seat = seats[start + column];
                    Vector3 span = seat.position - seats[start].position;
                    if (Vector3.Dot(seat.forward, forward) < .99f || Mathf.Abs(Vector3.Dot(forward, span)) > .015f ||
                        (column > 0 && Vector3.Dot(right, seat.position - seats[start + column - 1].position) < .85f))
                        return false;
                }
            }
            return Vector3.Dot(forward, seats[^1].position - seats[0].position) > 3.5f;
        }

        bool CheckQuestionDraw()
        {
            Random.State state = Random.state;
            try
            {
                Random.InitState(9348);
                var firstSeats = new HashSet<int>();
                var secondSeats = new HashSet<int>();
                for (int trial = 0; trial < 512; trial++)
                {
                    var (first, second) = app.audience.DrawQuestionSeats();
                    if (first == second || !app.audience.HeadAt(first) || !app.audience.HeadAt(second))
                        return false;
                    firstSeats.Add(first);
                    secondSeats.Add(second);
                }
                return firstSeats.Count == app.audience.SeatCount && secondSeats.Count == app.audience.SeatCount;
            }
            finally
            {
                Random.state = state;
            }
        }

        bool StartWithRearQuestion(bool live)
        {
            Random.State state = Random.state;
            try
            {
                for (int seed = 0; seed < 64; seed++)
                {
                    Random.InitState(seed);
                    int host = Random.Range(0, app.audience.SeatCount);
                    var (first, _) = app.audience.DrawQuestionSeats();
                    if (first < 2 * NaraAudience.SeatsPerRow || first == host)
                        continue;
                    Random.InitState(seed);
                    app.StartPractice(app.Deck.Topic, app.Deck.Summary, "id", false, live, false);
                    return app.QuestionerSeat == first && app.HostSeat == host;
                }
                return false;
            }
            finally
            {
                Random.state = state;
            }
        }

        bool CheckSettings(PracticeView view)
        {
            string[] keys = { PracticeView.ModePreference, PracticeView.LanguagePreference, PracticeView.VoicePreference,
                PracticeView.MetricsPreference };
            int[] previous = Array.ConvertAll(keys, key => PlayerPrefs.GetInt(key, -1));
            try
            {
                foreach (string key in keys)
                    PlayerPrefs.SetInt(key, 0);
                view.ReturnToSetup();
                view.SetBaseline(true);
                view.ToggleLanguage();
                view.ToggleCloud();
                view.ToggleMetrics();
                if (Array.Exists(keys, key => PlayerPrefs.GetInt(key) !=
                    (key == PracticeView.VoicePreference && !app.HasEndpoint ? 0 : 1)))
                    return false;

                view.ReturnToSetup();
                view.SetXr(true);
                view.SetXr(false);
                view.SelectBaseline();
                view.ToggleLanguage();
                view.ToggleCloud();
                view.ToggleMetrics();
                return Array.TrueForAll(keys, key => PlayerPrefs.GetInt(key) == 0);
            }
            finally
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    if (previous[i] < 0)
                        PlayerPrefs.DeleteKey(keys[i]);
                    else
                        PlayerPrefs.SetInt(keys[i], previous[i]);
                }
                PlayerPrefs.Save();
                view.ReturnToSetup();
            }
        }

        bool CheckVoiceBank()
        {
            var script = JsonUtility.FromJson<OfflineScript>(Resources.Load<TextAsset>("Nara/OfflineScript").text);
            var usedClips = new System.Collections.Generic.HashSet<AudioClip>();
            int female = 0, male = 0;
            foreach (var profile in app.audience.Profiles)
            {
                if (profile.gender == "female") female++;
                if (profile.gender == "male") male++;
                foreach (var line in script.lines)
                foreach (string locale in new[] { "id", "en" })
                {
                    AudioClip clip = NaraPractice.VoiceClip(profile, line.key, locale);
                    if (!clip || clip.channels != 1 || clip.samples < 12000 || !usedClips.Add(clip))
                    {
                        Debug.LogError($"NARA_VOICE_INVALID {profile.id}/{line.key}_{locale}");
                        return false;
                    }
                }
            }
            return female == 15 && male == 15 && usedClips.Count == 30 * 14;
        }

        IEnumerator WaitForAnswer()
        {
            float end = Time.realtimeSinceStartup + 15;
            while (app.Session.Phase == PracticePhase.Asking && Time.realtimeSinceStartup < end)
                yield return null;
        }

        IEnumerator Frame(string name)
        {
            if (!string.IsNullOrEmpty(folder) && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                yield return null;
                using var capture = new PlayerCapture(app, 1280, 800);
                capture.Save(Path.Combine(folder, name + ".png"));
            }
        }

        sealed class PlayerCapture : IDisposable
        {
            readonly Camera camera, originalCanvasCamera;
            readonly Canvas canvas;
            readonly PracticeView view;
            readonly RenderMode mode;
            readonly float originalDistance;
            readonly RenderTexture originalTarget, render;
            readonly Texture2D image;

            public PlayerCapture(NaraPractice app, int width, int height)
            {
                camera = app.practiceCamera;
                view = app.GetComponent<PracticeView>();
                canvas = view.ControlsCanvas;
                originalTarget = camera.targetTexture;
                mode = canvas.renderMode;
                originalCanvasCamera = canvas.worldCamera;
                originalDistance = canvas.planeDistance;
                render = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                camera.targetTexture = render;
                if (mode != RenderMode.WorldSpace)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = .15f;
                }
                Canvas.ForceUpdateCanvases();
                view.Refresh();
            }

            public void Save(string path)
            {
                var previous = RenderTexture.active;
                try
                {
                    camera.Render();
                    RenderTexture.active = render;
                    image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0, false);
                }
                finally
                {
                    RenderTexture.active = previous;
                }
                File.WriteAllBytes(path, path.EndsWith(".jpg", StringComparison.Ordinal) ? image.EncodeToJPG(88) : image.EncodeToPNG());
            }

            public void Dispose()
            {
                camera.targetTexture = originalTarget;
                canvas.renderMode = mode;
                canvas.worldCamera = originalCanvasCamera;
                canvas.planeDistance = originalDistance;
                Canvas.ForceUpdateCanvases();
                view.Refresh();
                RenderTexture.ReleaseTemporary(render);
                UnityEngine.Object.Destroy(image);
            }
        }

        static bool Check(bool correct, string step)
        {
            if (correct)
                Debug.Log("NARA_SMOKE OK " + step);
            else
            {
                Debug.LogError("NARA_SMOKE FAIL " + step);
                Exit(1);
            }
            return correct;
        }

        static void Exit(int status)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(status);
#else
            Application.Quit(status);
#endif
        }
    }
}
