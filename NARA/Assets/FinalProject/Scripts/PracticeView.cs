using System;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nara.Practice
{
    public sealed partial class PracticeView : MonoBehaviour
    {
        public static readonly Vector2 DesktopSize = new(1440, 900);
        public static readonly Vector2 HeadsetSize = new(960, 600);
        public const float HeadsetScale = .00125f;
        internal const string ModePreference = "Nara.Practice.Laptop";
        internal const string LanguagePreference = "Nara.Practice.English";
        internal const string VoicePreference = "Nara.Practice.LiveVoice";
        const string LiveDataNotice = "Mode live mengirim teks slide, penjelasan, dan jawaban ke layanan AI.";
        internal const string MetricsPreference = "Nara.Practice.LogMetrics";

        static readonly Color Navy = new Color32(13, 16, 26, 255);
        static readonly Color Dark = new Color32(23, 27, 42, 255);
        static readonly Color Raised = new Color32(40, 46, 66, 255);
        static readonly Color Stroke = new Color32(74, 80, 104, 255);
        static readonly Color Ink = new Color32(28, 21, 52, 255);
        static readonly Color Muted = new Color32(190, 194, 217, 255);
        static readonly Color Accent = new Color32(199, 186, 255, 255);
        static readonly Color Tint = new Color32(223, 213, 255, 255);
        static readonly Color White = new Color32(248, 246, 255, 255);
        static readonly Color Focus = new Color32(239, 220, 160, 255);

        NaraPractice app;
        Font regular, bold;
        Sprite rounded;
        Canvas canvas;
        internal Canvas ControlsCanvas => canvas;
        CanvasScaler scaler;
        RectTransform desktopRoot, headsetRoot;
        InputField topic, summary, answer;
        bool cloud, metrics, baseline, english, xrView;
        PracticePhase previousPhase = PracticePhase.Setup;

        public bool Editing => topic && (topic.isFocused || summary.isFocused || answer.isFocused || writtenPresentation.isFocused);
        internal bool AnswerInProgress => answer && (answer.isFocused || !string.IsNullOrWhiteSpace(answer.text));
        string HostName => app.audience.ProfileAt(app.HostSeat).label;

        public void Initialise(NaraPractice practice)
        {
            app = practice;
            Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            regular = Resources.Load<Font>("Nara/Fonts/Inter-Regular") ?? fallback;
            bold = Resources.Load<Font>("Nara/Fonts/Inter-Bold") ?? fallback;
            rounded = RoundedSprite();
            var view = new GameObject("NARA controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = view.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scaler = view.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = DesktopSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            if (!NaraQuestBridge.IsStandalone && EventSystem.current == null)
                new GameObject("Desktop input", typeof(EventSystem), typeof(StandaloneInputModule));
            var root = (RectTransform)canvas.transform;
            BuildDesktop(root);
            headsetRoot = Centered(root, "Panel headset", HeadsetSize);
            BuildHeadset(headsetRoot);
            BuildSpeechBubble();
            LoadSetup();
            Refresh();
        }

        public void LoadSetup()
        {
            if (!topic)
                return;
            topic.text = app.Deck.Topic;
            summary.text = app.Deck.Summary;
            baseline = PlayerPrefs.GetInt(ModePreference) == 1;
            english = PlayerPrefs.GetInt(LanguagePreference, app.Deck.Language == "en" ? 1 : 0) == 1;
            cloud = app.LiveAvailable && PlayerPrefs.GetInt(VoicePreference) == 1;
            metrics = PlayerPrefs.GetInt(MetricsPreference) == 1;
            answer.text = "";
        }

        public void ReturnToSetup()
        {
            desktopSection = DesktopSection.Start;
            LoadSetup();
            ClearFocus();
            Refresh();
        }

        public void Begin()
        {
            if (app.LoadingClassroom || app.ImportingDeck)
                return;
            if (!xrView && desktopSection != DesktopSection.Start)
            {
                SetDesktopSection(DesktopSection.Start);
                return;
            }
            if (string.IsNullOrWhiteSpace(topic.text))
            {
                app.Notify("Isi topik untuk memulai latihan.");
                if (!xrView)
                    topic.ActivateInputField();
                return;
            }
            app.StartPractice(topic.text, summary.text, english ? "en" : "id", baseline && !xrView, cloud, metrics);
            ClearFocus();
            Refresh();
        }

        internal void RestoreDraft(string draftTopic, string draftSummary)
        {
            topic.SetTextWithoutNotify(draftTopic);
            summary.SetTextWithoutNotify(draftSummary);
            Refresh();
        }

        public void SelectClassroom(bool useIf101) => app.SelectClassroom(useIf101, topic.text, summary.text);

        public void ToggleClassroom() => SelectClassroom(!app.IsIf101);

        public void ToggleLanguage() => SavePreference(LanguagePreference, english = !english);
        public void ToggleMetrics() => SavePreference(MetricsPreference, metrics = !metrics);

        public void ToggleCloud()
        {
            if (app.LiveAvailable)
                SavePreference(VoicePreference, cloud = !cloud);
            else if (NaraQuestBridge.IsStandalone)
                app.Notify("Buka Pengaturan Quest untuk memasukkan kunci AISUBS.");
        }

        public void SetBaseline(bool useLaptop)
        {
            if (!xrView)
                SavePreference(ModePreference, baseline = useLaptop);
        }

        public void SelectBaseline() => SetBaseline(!baseline);

        static void SavePreference(string key, bool enabled)
        {
            PlayerPrefs.SetInt(key, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void Submit()
        {
            if (app.Session.Phase != PracticePhase.Question)
                return;
            app.Answer(answer.text, false);
            if (app.Session.Phase != PracticePhase.Question || !app.Live)
                answer.text = "";
        }

        void OpenSlides()
        {
            Directory.CreateDirectory(app.Deck.Folder);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            Application.OpenURL(new Uri(app.Deck.Folder + Path.DirectorySeparatorChar).AbsoluteUri);
#else
            app.Notify("Folder slide " + app.Deck.Folder);
#endif
        }

        public void SetXr(bool active)
        {
            if (xrView == active)
                return;
            xrView = active;
            ClearFocus();
            var rect = (RectTransform)canvas.transform;
            if (active)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = app.practiceCamera;
                scaler.enabled = false;
                rect.SetParent(app.controlsAnchor, false);
                rect.sizeDelta = HeadsetSize;
                rect.localPosition = Vector3.zero;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one * HeadsetScale;
            }
            else
            {
                rect.SetParent(null, false);
                rect.localScale = Vector3.one;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                scaler.enabled = true;
            }
            SetSpeechMode();
            Refresh();
        }

        public void MoveFocus(bool backwards)
        {
            if (xrView || !EventSystem.current)
                return;
            var controls = desktopRoot.GetComponentsInChildren<Selectable>();
            GameObject focus = EventSystem.current.currentSelectedGameObject;
            int current = Array.FindIndex(controls, item => item.gameObject == focus);
            if (current < 0)
                current = backwards ? 0 : -1;
            for (int i = 1; i <= controls.Length; i++)
            {
                int index = (current + (backwards ? -i : i) + controls.Length) % controls.Length;
                if (!controls[index].IsInteractable())
                    continue;
                controls[index].Select();
                if (controls[index] is InputField field)
                    field.ActivateInputField();
                return;
            }
        }

        static void ClearFocus()
        {
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(null);
        }

        public void Refresh()
        {
            PracticePhase state = app.Session.Phase;
            if (state != previousPhase)
            {
                if (state != PracticePhase.Presentation)
                    composingPresentation = false;
                readingPage = 0;
                if (state is not (PracticePhase.Asking or PracticePhase.Question))
                    answer.text = "";
                ClearFocus();
                previousPhase = state;
            }
            Show(desktopRoot, !xrView);
            Show(desktopBackdrop, !xrView && (state == PracticePhase.Review || app.Baseline));
            Show(preparationShade, !xrView && state == PracticePhase.Setup);
            Show(headsetRoot, xrView);
            RefreshSpeechBubble(state);
            if (xrView)
                RefreshHeadset(state);
            else
                RefreshDesktop(state);
        }

        string Remaining()
        {
            int seconds = Mathf.CeilToInt(app.Session.Remaining);
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }

        string QuestionText(PracticePhase state) => state switch
        {
            PracticePhase.FloorRequest => app.QuestionerName + (app.Baseline ? " meminta giliran bertanya." : " mengangkat tangan."),
            PracticePhase.Asking or PracticePhase.Question when measuredSpeech == app.Session.Question => SpokenPage,
            PracticePhase.Asking or PracticePhase.Question => app.Session.Question,
            PracticePhase.Waiting when app.PreparingTranscript => "Membaca rekaman…",
            PracticePhase.Waiting when app.Session.Operation == PracticeSession.Feedback => "Menyiapkan saran…",
            _ => "Menyiapkan pertanyaan…"
        };

        string QuestionStatus(PracticePhase state) => state switch
        {
            PracticePhase.FloorRequest when !app.Baseline => NaraAudience.SeatLabel(app.QuestionerSeat),
            PracticePhase.FloorRequest => "Permintaan giliran",
            PracticePhase.Asking => app.QuestionerName + " sedang bertanya",
            PracticePhase.Question => app.Recording ? "Mikrofon aktif" : "Giliranmu menjawab",
            _ => "Tanya jawab"
        };

        string QuestionNumber(PracticePhase state)
        {
            int number = app.Session.MainQuestions + (state == PracticePhase.FloorRequest ? 1 : 0);
            if (state == PracticePhase.Waiting)
                return "";
            return app.Session.IsFollowUp ? "Pertanyaan lanjutan" : $"Pertanyaan {number} dari 2";
        }

        string SessionReport() =>
            $"{app.Session.Elapsed:0} detik presentasi  ·  {app.Session.MainQuestions} pertanyaan  ·  {app.Session.Attempts} jawaban";

        static void Show(RectTransform rect, bool visible)
        {
            if (rect.gameObject.activeSelf != visible)
                rect.gameObject.SetActive(visible);
        }

        static void Show(Selectable item, bool visible) => Show((RectTransform)item.transform, visible);
        static void Show(Graphic item, bool visible) => Show(item.rectTransform, visible);

        static void Set(Text label, string text)
        {
            if (label.text != text)
                label.text = text;
        }

        static void SetButton(Button button, string text) => Set(button.GetComponentInChildren<Text>(true), text);
        static void Choice(Button button, bool selected)
        {
            button.image.color = selected ? Accent : Raised;
            button.GetComponentInChildren<Text>(true).color = selected ? Ink : White;
        }

        static RectTransform Centered(RectTransform parent, string name, Vector2 size)
        {
            var rect = Group(parent, name, 0, 0, size.x, size.y);
            rect.pivot = rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        static RectTransform Group(RectTransform parent, string name, float x, float y, float w, float h)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        RectTransform Panel(RectTransform parent, string name, Color color, float x, float y, float w, float h, bool corners = true)
        {
            var rect = Group(parent, name, x, y, w, h);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (corners)
            {
                image.sprite = rounded;
                image.type = Image.Type.Sliced;
            }
            return rect;
        }

        RectTransform Card(RectTransform parent, string name, Color color, float x, float y, float width, float height)
        {
            RectTransform frame = Panel(parent, name, Stroke, x, y, width, height);
            Panel(frame, "Permukaan", color, 1, 1, width - 2, height - 2);
            return frame;
        }

        Text Label(RectTransform parent, string text, int size, FontStyle style, Color color, float x, float y, float w, float h,
            TextAnchor anchor = TextAnchor.MiddleLeft, int minSize = 0)
        {
            var label = Group(parent, "Teks", x, y, w, h).gameObject.AddComponent<Text>();
            label.font = style == FontStyle.Bold ? bold : regular;
            label.fontSize = size;
            label.fontStyle = bold == regular ? style : FontStyle.Normal;
            label.text = text;
            label.color = color;
            label.alignment = anchor;
            label.raycastTarget = false;
            label.supportRichText = false;
            if (minSize > 0)
            {
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = minSize;
                label.resizeTextMaxSize = size;
            }
            return label;
        }

        Button Control(RectTransform parent, string title, float x, float y, float w, float h, UnityAction click,
            bool primary = false, int fontSize = 18)
        {
            var rect = Panel(parent, title, primary ? Accent : Raised, x, y, w, h);
            Button button = MakeButton(rect, click);
            Label(rect, title, fontSize, FontStyle.Bold, primary ? Ink : White, 8, 0, w - 16, h, TextAnchor.MiddleCenter);
            return button;
        }

        static Button MakeButton(RectTransform rect, UnityAction click)
        {
            var button = rect.gameObject.AddComponent<Button>();
            AttachSelectable(button);
            button.onClick.AddListener(click);
            return button;
        }

        static void AttachSelectable(Selectable control)
        {
            control.targetGraphic = control.GetComponent<Image>();
            control.targetGraphic.raycastTarget = true;
            control.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = control.colors;
            colors.highlightedColor = new Color(.91f, .91f, .98f);
            colors.pressedColor = new Color(.78f, .79f, .91f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(.66f, .70f, .69f, .70f);
            control.colors = colors;
            var outline = control.gameObject.AddComponent<Outline>();
            outline.effectColor = Focus;
            outline.effectDistance = new Vector2(2, -2);
            outline.enabled = false;
            control.gameObject.AddComponent<PracticeFocus>().Outline = outline;
        }

        InputField Field(RectTransform parent, float x, float y, float w, float h, string placeholder, int limit, bool multiline = false)
        {
            var rect = Panel(parent, "Input " + placeholder, Stroke, x, y, w, h);
            Panel(rect, "Isi input", Navy, 1.5f, 1.5f, w - 3, h - 3);
            var field = rect.gameObject.AddComponent<InputField>();
            AttachSelectable(field);
            field.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            field.characterLimit = limit;
            TextAnchor anchor = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            field.textComponent = Label(rect, "", 20, FontStyle.Normal, White, 16, 9, w - 32, h - 18, anchor);
            field.placeholder = Label(rect, placeholder, 20, FontStyle.Normal, Muted, 16, 9, w - 32, h - 18, anchor);
            field.caretColor = Accent;
            field.customCaretColor = true;
            field.selectionColor = new Color(.58f, .44f, .91f, .45f);
            return field;
        }

        RawImage SlideFrame(RectTransform parent, float x, float y, float w, float h)
        {
            var frame = Panel(parent, "Layar slide", Dark, x, y, w, h, false);
            var image = Group(frame, "Gambar slide", 0, 0, w, h).gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            var aspect = image.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 16f / 9;
            return image;
        }

        static void ShowSlide(RawImage image, Texture2D slide)
        {
            if (image.texture == slide)
                return;
            image.texture = slide;
            if (slide)
                image.GetComponent<AspectRatioFitter>().aspectRatio = (float)slide.width / slide.height;
        }

        static Sprite RoundedSprite()
        {
            const int size = 56, radius = 18;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(x + .5f - size / 2f) - (size / 2f - radius));
                    float dy = Mathf.Max(0, Mathf.Abs(y + .5f - size / 2f) - (size / 2f - radius));
                    byte alpha = (byte)(255 * Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx * dx + dy * dy)));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }

    public sealed class PracticeFocus : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public Outline Outline { private get; set; }
        public void OnSelect(BaseEventData eventData) => Outline.enabled = true;
        public void OnDeselect(BaseEventData eventData) => Outline.enabled = false;
        void OnDisable()
        {
            if (Outline)
                Outline.enabled = false;
        }
    }

}
