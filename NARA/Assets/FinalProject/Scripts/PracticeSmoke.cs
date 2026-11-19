using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Nara.Practice
{
    public sealed class PracticeSmoke : MonoBehaviour
    {
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
            if (!Check(app.sceneTriangles > 0 && app.sceneTriangles <= 150000 && app.Deck.Slides.Count > 0, "scene and deck"))
                yield break;
            if (!Check(app.audience.Profiles.Length == AvatarCatalog.ProfileCount && app.audience.HeadAt(NaraAudience.LeadSeat),
                "30 avatar choices with a seated lead"))
                yield break;
            if (!Check(CheckVoiceBank(), "all 30 characters have their own 14 bilingual voice clips"))
                yield break;
            if (!Check(app.room.GetComponentsInChildren<TextMesh>(true).Length == 0, "audience view has no floating nameplate"))
                yield break;
            Transform presenter = app.practiceCamera.transform;
            Transform[] seats = app.audience.seats;
            float left = presenter.InverseTransformPoint(seats[1].position).x;
            float right = presenter.InverseTransformPoint(seats[2].position).x;
            Vector3 towardPresenter = Vector3.ProjectOnPlane(presenter.position - seats[0].position, Vector3.up);
            if (!Check(left < right && Vector3.Dot(-seats[0].forward, towardPresenter.normalized) > .85f,
                "audience faces the presenter and seat labels match the view"))
                yield break;
            foreach (Transform seat in seats)
            {
                Vector3 visible = app.practiceCamera.WorldToViewportPoint(seat.GetComponentInChildren<NaraAvatarPuppet>().Head.position);
                if (!Check(visible.z > 0 && visible.x is > .03f and < .97f && visible.y is > .1f and < .9f,
                    "seat visible from the presenter position"))
                    yield break;
            }
            int[] original = { app.audience.Selection(0), app.audience.Selection(1), app.audience.Selection(2) };
            app.audience.Select(0, original[1], false);
            if (!Check(app.audience.Selection(0) == original[1] && app.audience.Selection(1) == original[0], "avatar selection swaps occupied seats"))
                yield break;
            for (int i = 0; i < app.audience.Profiles.Length; i++)
            {
                app.audience.Select(0, i, false);
                yield return null;
                if (app.audience.Selection(0) != i || !app.audience.HeadAt(0))
                {
                    Check(false, "all 30 avatar prefabs can take the speaking seat");
                    yield break;
                }
            }
            for (int i = 0; i < original.Length; i++)
                app.audience.Select(i, original[i], false);
            Check(true, "all 30 avatar prefabs can take the speaking seat");
            var speakingActor = app.audience.HeadAt(0).GetComponentInParent<NaraAvatarPuppet>();
            var mesh = speakingActor.GetComponentInChildren<SkinnedMeshRenderer>();
            Quaternion neutral = speakingActor.Head.localRotation;
            speakingActor.Animate(.09f, 6, true, .8f, true, presenter.position);
            int jaw = 0;
            while (jaw < mesh.sharedMesh.blendShapeCount && !mesh.sharedMesh.GetBlendShapeName(jaw).EndsWith("JawOpen", StringComparison.OrdinalIgnoreCase))
                jaw++;
            if (!Check(Quaternion.Angle(neutral, speakingActor.Head.localRotation) > .01f &&
                jaw < mesh.sharedMesh.blendShapeCount && mesh.GetBlendShapeWeight(jaw) > 0, "seated actor moves and drives jaw blend shape"))
                yield break;
            var view = app.GetComponent<PracticeView>();
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
            if (!Check(coverage < .21f && !GameObject.Find("Pengaturan materi") && !GameObject.Find("Pengaturan sesi"),
                "compact setup keeps the detailed controls behind separate panels"))
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
            view.ToggleAvatarPicker();
            if (!Check(GameObject.Find("Pilihan karakter") && !GameObject.Find("Persiapan latihan"),
                "desktop avatar picker gets its own focusable panel"))
                yield break;
            if (!Check(GameObject.Find("Pilih karakter") == null &&
                GameObject.Find("Pilihan karakter").GetComponentsInChildren<Text>().Length > 14,
                "avatar menu exposes roles, portraits and voice preview"))
                yield break;
            app.PreviewAvatar(original[0], true);
            if (!Check(app.voice.clip == NaraPractice.VoiceClip(app.audience.Profiles[original[0]], "intro", "en") &&
                app.SpeechText.Contains(app.audience.Profiles[original[0]].label), "picker plays the selected character's English voice"))
                yield break;
            app.StopAvatarPreview();
            yield return Frame("01-desktop-avatars");
            view.SetXr(true);
            Canvas ui = FindFirstObjectByType<Canvas>();
            if (!Check(ui.renderMode == RenderMode.WorldSpace && ui.transform.parent == app.controlsAnchor &&
                Mathf.Approximately(((RectTransform)ui.transform).rect.width * ui.transform.localScale.x, 1.2f) &&
                GameObject.Find("Avatar headset") && !GameObject.Find("Panel desktop"),
                "headset has a dedicated world-space avatar picker"))
                yield break;
            yield return Frame("01-headset-lead");
            view.NextAvatarSeat();
            view.Refresh();
            yield return Frame("01-headset-avatars");
            view.ToggleAvatarPicker();
            yield return Frame("01-headset-setup");
            view.SetXr(false);
            if (!Check(ui.renderMode == RenderMode.ScreenSpaceOverlay && GameObject.Find("Panel desktop"),
                "returning from headset restores the desktop layout"))
                yield break;
            app.StartPractice(app.Deck.Topic, app.Deck.Summary, "id", false, false, false);
            if (!Check(app.GreetingText.Contains(app.audience.ProfileAt(NaraAudience.LeadSeat).label) &&
                app.voice.clip == NaraPractice.VoiceClip(app.audience.ProfileAt(NaraAudience.LeadSeat), "intro", "id"),
                "lead greets the presenter in the selected language"))
                yield break;
            app.ChangeSlide(1);
            if (!Check(app.SlideIndex == Mathf.Min(1, app.Deck.Slides.Count - 1), "next slide"))
                yield break;
            if (!Check(app.confidenceScreen && app.confidenceScreen.sharedMaterial == app.slideScreen.sharedMaterial &&
                app.confidenceScreen.sharedMaterial.mainTexture == app.Slide &&
                Vector3.Dot(-app.confidenceScreen.transform.forward, presenter.position - app.confidenceScreen.transform.position) > 0,
                "wall and lectern display the same slide, monitor faces presenter"))
                yield break;
            yield return null;
            if (!Check(GameObject.Find("Kendali presentasi 3D") && !GameObject.Find("Kontrol latihan"),
                "room presentation uses the compact controls"))
                yield break;
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
            float wristAtRest = speakingActor.RightWrist.position.y;
            app.Session.Tick(PracticeSession.Duration);
            yield return new WaitForSecondsRealtime(.74f);
            app.Answer("Terlalu cepat", false);
            var permission = GameObject.Find("Persilakan bertanya");
            if (!Check(app.Session.Phase == PracticePhase.FloorRequest && app.Session.MainQuestions == 0 &&
                app.Session.Attempts == 0 && !app.voice.isPlaying && permission &&
                speakingActor.RightWrist.position.y > wristAtRest + .25f,
                "hand goes up and speech and answers wait for permission"))
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
            permission.GetComponent<Button>().onClick.Invoke();
            app.GrantFloor();
            app.Answer("", true);
            yield return new WaitForSecondsRealtime(.65f);
            if (!Check(app.Session.Phase == PracticePhase.Asking && app.Session.MainQuestions == 1 &&
                app.Session.Skips == 0 && app.voice.isPlaying &&
                app.voice.clip == NaraPractice.VoiceClip(app.audience.ProfileAt(0), "question_1", "id") &&
                speakingActor.RightWrist.position.y < wristAtRest + .09f,
                "grant lowers hand, speaks once and holds the answer controls"))
                yield break;
            yield return WaitForAnswer();
            if (!Check(app.Session.Phase == PracticePhase.Question && !app.voice.isPlaying && app.CanReplay,
                "answer opens after the first question ends"))
                yield break;
            app.Replay();
            if (!Check(app.Session.Phase == PracticePhase.Asking && app.Session.MainQuestions == 1,
                "replay holds the floor without counting another question"))
                yield break;
            yield return WaitForAnswer();
            app.Answer("Mahasiswa membutuhkan tempat diskusi.", false);
            if (!Check(app.Session.Phase == PracticePhase.FloorRequest && app.QuestionerSeat != 0 &&
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
            int satria = Array.FindIndex(app.audience.Profiles, profile => profile.id == "Male_Adult_13");
            app.audience.Select(NaraAudience.LeadSeat, satria, false);
            app.StartPractice(app.Deck.Topic, app.Deck.Summary, "en", true, false, false);
            if (!Check(app.GreetingText.Contains("Satria") &&
                app.voice.clip == NaraPractice.VoiceClip(app.audience.ProfileAt(NaraAudience.LeadSeat), "intro", "en"),
                "Satria's English greeting follows his model into the lead role"))
                yield break;
            if (!Check(app.Baseline && !app.room.activeSelf && app.voice.isActiveAndEnabled, "laptop baseline keeps speech available"))
                yield break;
            yield return Frame("05-laptop");
            app.SetNetworkAvailable(false);
            if (!Check(!app.Live && app.FallbackCount == 1 && app.SpeechText.StartsWith("We lost the connection"),
                "local disconnect immediately plays the English line"))
                yield break;
            app.ResetPractice();
            for (int i = 0; i < original.Length; i++)
                app.audience.Select(i, original[i], false);

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-naraMock") >= 0)
            {
                app.StartPractice(app.Deck.Topic, app.Deck.Summary, "id", false, true, false);
                app.FinishPresentation();
                yield return WaitForReply();
                if (!Check(app.Live && app.Mock && app.Session.Phase == PracticePhase.FloorRequest &&
                    app.Session.MainQuestions == 0, "mock HTTP prepares a question before it speaks"))
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
            Debug.Log("NARA_SMOKE PASS");
            Exit(0);
        }

        IEnumerator WaitForReply()
        {
            float end = Time.realtimeSinceStartup + 9;
            while (app.Session.Phase == PracticePhase.Waiting && Time.realtimeSinceStartup < end)
                yield return null;
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
                CaptureFrame(Path.Combine(folder, name + ".png"));
            }
        }

        void CaptureFrame(string path)
        {
            Camera camera = app.practiceCamera;
            Canvas canvas = FindFirstObjectByType<Canvas>();
            RenderTexture previous = RenderTexture.active;
            RenderTexture originalTarget = camera.targetTexture;
            RenderMode mode = canvas.renderMode;
            Camera originalCanvasCamera = canvas.worldCamera;
            float originalDistance = canvas.planeDistance;
            RenderTexture render = RenderTexture.GetTemporary(1280, 800, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = render;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .15f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = render;
                image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = originalTarget;
                RenderTexture.active = previous;
                canvas.renderMode = mode;
                canvas.worldCamera = originalCanvasCamera;
                canvas.planeDistance = originalDistance;
                RenderTexture.ReleaseTemporary(render);
                Destroy(image);
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
