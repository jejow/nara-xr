using UnityEngine;
using UnityEngine.UI;

namespace Nara.Practice
{
    public sealed partial class PracticeView
    {
        enum DesktopSection { Start, Slides, Settings }

        DesktopSection desktopSection;
        RectTransform desktopBackdrop, preparationShade, masthead, setup, startCard, slidesCard, settingsCard, roomHint;
        RectTransform stageBar, laptopFrame, conversation, answerArea, review, timerFill;
        RawImage setupThumbnail, setupSlide, laptopSlide;
        Text deckCount, deckStatus, setupNotice, settingsNotice, stageClock, stageSlide, stageHint;
        Text desktopPrompt, desktopStatus, desktopNumber, desktopHint, desktopNotice, reviewMessage, reviewCounts;
        Button roomChoice, laptopChoice, languageChoice, metricsChoice, cloudChoice;
        Button stagePrevious, stageNext, stagePause, grantFloor, submit, talk, skip, replay, testConnection, reviewReplay;

        void BuildDesktop(RectTransform canvasRoot)
        {
            desktopBackdrop = Panel(canvasRoot, "Latar desktop", Navy, 0, 0, 0, 0, false);
            Stretch(desktopBackdrop);
            var shade = Group(canvasRoot, "Gradasi ruang", 0, 0, 0, 0);
            preparationShade = shade;
            Stretch(shade);
            RawImage shadeImage = shade.gameObject.AddComponent<RawImage>();
            shadeImage.raycastTarget = false;
            shadeImage.texture = MakePreparationShade();
            desktopRoot = Centered(canvasRoot, "Panel desktop", DesktopSize);

            masthead = Card(desktopRoot, "NARA", Navy, 44, 38, 124, 55);
            Label(masthead, "NARA", 26, FontStyle.Bold, White, 0, 0, 124, 55, TextAnchor.MiddleCenter);
            setup = Group(desktopRoot, "Persiapan latihan", 0, 0, 1440, 900);
            BuildStartCard(setup);
            BuildSlidesCard(setup);
            BuildSettingsCard(setup);
            roomHint = Card(setup, "Kendali pandangan", Navy, 1101, 800, 295, 52);
            Label(roomHint, "Klik kanan + seret untuk melihat", 16, FontStyle.Normal, Muted,
                10, 0, 275, 52, TextAnchor.MiddleCenter);

            laptopFrame = Card(desktopRoot, "Tampilan presentasi laptop", Dark, 112, 107, 1216, 660);
            laptopSlide = SlideFrame(laptopFrame, 15, 12, 1186, 636);
            BuildDesktopPresentation(desktopRoot);
            BuildDesktopConversation(desktopRoot);

            review = Card(desktopRoot, "Refleksi", Dark, 324, 231, 792, 436);
            Label(review, "Latihan selesai", 45, FontStyle.Bold, White, 37, 27, 716, 65);
            reviewCounts = Label(review, "", 20, FontStyle.Normal, Muted, 38, 106, 716, 42);
            Panel(review, "Pemisah", Stroke, 38, 168, 716, 1, false);
            Label(review, "Saran untuk sesi berikutnya", 19, FontStyle.Bold, Tint, 38, 190, 716, 30);
            reviewMessage = Label(review, "", 28, FontStyle.Normal, White, 38, 232, 716, 107, TextAnchor.UpperLeft, 20);
            reviewReplay = Control(review, "Putar saran", 38, 353, 238, 52, app.Replay);
            Control(review, "Latihan baru", 291, 353, 463, 52, app.ResetPractice, true, 20);
            BuildAvatarPicker(desktopRoot);
        }

        void BuildStartCard(RectTransform root)
        {
            startCard = Card(root, "Mulai latihan presentasi", Dark, 44, 188, 412, 624);
            Label(startCard, "Latihan\npresentasi", 53, FontStyle.Bold, White, 28, 24, 358, 130, TextAnchor.UpperLeft);
            Label(startCard, "3 menit · 2 pertanyaan", 20, FontStyle.Normal, Muted, 30, 158, 344, 37);
            Panel(startCard, "Pemisah", Stroke, 29, 211, 354, 1, false);
            Label(startCard, "Topik", 18, FontStyle.Bold, Tint, 29, 232, 353, 31);
            topic = Field(startCard, 28, 268, 356, 53, "Judul presentasi", 100);
            roomChoice = Control(startCard, "Ruang 3D", 28, 344, 172, 51, () => baseline = false);
            laptopChoice = Control(startCard, "Layar laptop", 212, 344, 172, 51, () => baseline = true);

            var material = Panel(startCard, "Materi latihan", Raised, 28, 420, 356, 67);
            MakeButton(material, () => SetDesktopSection(DesktopSection.Slides));
            setupThumbnail = SlideFrame(material, 11, 9, 87, 49);
            Label(material, "Materi latihan", 19, FontStyle.Bold, White, 111, 7, 205, 28);
            deckCount = Label(material, "", 16, FontStyle.Normal, Muted, 111, 35, 205, 24);
            Label(material, "›", 28, FontStyle.Normal, Tint, 316, 11, 28, 45, TextAnchor.MiddleCenter);
            Control(startCard, "Pilih avatar", 28, 501, 172, 43, ToggleAvatarPicker);
            Control(startCard, "Pengaturan", 212, 501, 172, 43, () => SetDesktopSection(DesktopSection.Settings));
            Control(startCard, "Mulai latihan", 28, 562, 356, 49, Begin, true, 21);

            setupNotice = Label(root, "", 19, FontStyle.Bold, White, 47, 827, 670, 44);
        }

        void BuildSlidesCard(RectTransform root)
        {
            slidesCard = Card(root, "Pengaturan materi", Dark, 44, 126, 554, 720);
            Label(slidesCard, "Materi latihan", 37, FontStyle.Bold, White, 29, 22, 366, 53);
            Control(slidesCard, "Kembali", 414, 29, 111, 43, CloseDesktopSection);
            Label(slidesCard, "Ringkasan", 21, FontStyle.Bold, Tint, 30, 102, 493, 32);
            summary = Field(slidesCard, 29, 142, 496, 109, "Tujuan dan isi presentasi", 1500, true);
            setupSlide = SlideFrame(slidesCard, 29, 279, 496, 279);
            Control(slidesCard, "Buka folder slide", 29, 582, 241, 53, OpenSlides);
            Control(slidesCard, "Muat ulang", 284, 582, 241, 53, app.ReloadDeck);
            deckStatus = Label(slidesCard, "", 18, FontStyle.Normal, Muted, 30, 655, 496, 43,
                TextAnchor.MiddleLeft, 16);
        }

        void BuildSettingsCard(RectTransform root)
        {
            settingsCard = Card(root, "Pengaturan sesi", Dark, 44, 195, 500, 528);
            Label(settingsCard, "Pengaturan", 37, FontStyle.Bold, White, 28, 24, 318, 54);
            Control(settingsCard, "Kembali", 357, 31, 115, 42, CloseDesktopSection);
            Label(settingsCard, "Bahasa", 20, FontStyle.Bold, Tint, 28, 111, 443, 31);
            languageChoice = Control(settingsCard, "Bahasa Indonesia", 28, 149, 444, 53, ToggleLanguage);
            Label(settingsCard, "Suara", 20, FontStyle.Bold, Tint, 28, 224, 443, 31);
            cloudChoice = Control(settingsCard, "Suara offline", 28, 262, 444, 53, ToggleCloud);
            settingsNotice = Label(settingsCard, "", 17, FontStyle.Normal, Muted, 28, 328, 444, 65);
            metricsChoice = Control(settingsCard, "Catat waktu nonaktif", 28, 429, 444, 53, ToggleMetrics);
        }

        void BuildDesktopPresentation(RectTransform root)
        {
            stageBar = Card(root, "Kendali presentasi 3D", Navy, 168, 790, 1104, 73);
            stageClock = Label(stageBar, "03:00", 29, FontStyle.Bold, White, 17, 5, 109, 53, TextAnchor.MiddleCenter);
            var track = Panel(stageBar, "Sisa waktu", Raised, 26, 59, 91, 4, false);
            timerFill = Panel(track, "Waktu berjalan", Accent, 0, 0, 91, 4, false);
            stagePrevious = Control(stageBar, "‹", 151, 13, 45, 47, () => app.ChangeSlide(-1), false, 25);
            stagePrevious.gameObject.name = "Slide sebelumnya";
            stageSlide = Label(stageBar, "", 20, FontStyle.Bold, Tint, 205, 13, 111, 47, TextAnchor.MiddleCenter);
            stageNext = Control(stageBar, "›", 325, 13, 45, 47, () => app.ChangeSlide(1), false, 25);
            stageNext.gameObject.name = "Slide berikutnya";
            stagePause = Control(stageBar, "Jeda", 399, 13, 103, 47, app.TogglePause);
            Control(stageBar, "Masuk Q&A", 515, 13, 163, 47, app.FinishPresentation, true, 19);
            Control(stageBar, "Akhiri", 691, 13, 103, 47, app.ResetPractice);
            stageHint = Label(stageBar, "", 17, FontStyle.Normal, Muted, 819, 11, 265, 52, TextAnchor.MiddleCenter);
        }

        void BuildDesktopConversation(RectTransform root)
        {
            conversation = Card(root, "Percakapan", Dark, 134, 624, 1172, 239);
            desktopStatus = Label(conversation, "", 20, FontStyle.Bold, Tint, 28, 11, 583, 38);
            desktopNumber = Label(conversation, "", 17, FontStyle.Normal, Muted, 619, 11, 349, 38, TextAnchor.MiddleRight);
            testConnection = Control(conversation, "Uji koneksi", 993, 11, 151, 37, () => app.SetNetworkAvailable(false), fontSize: 16);
            desktopPrompt = Label(conversation, "", 30, FontStyle.Bold, White, 28, 57, 1116, 78, TextAnchor.UpperLeft, 21);
            grantFloor = Control(conversation, "Persilakan bertanya", 28, 142, 261, 51, app.GrantFloor, true, 19);
            desktopHint = Label(conversation, "", 18, FontStyle.Normal, Muted, 312, 142, 700, 51);
            Control(conversation, "Akhiri", 1034, 142, 110, 51, app.ResetPractice);
            desktopNotice = Label(conversation, "", 16, FontStyle.Normal, Tint, 28, 197, 1116, 31);

            answerArea = Group(conversation, "Jawaban", 28, 142, 984, 51);
            answer = Field(answerArea, 0, 0, 375, 51, "Ketik jawaban", 2000);
            submit = Control(answerArea, "Sudah jawab", 388, 0, 145, 51, Submit, true, 18);
            talk = Control(answerArea, "Tahan bicara", 546, 0, 145, 51, () => { }, fontSize: 17);
            talk.gameObject.AddComponent<HoldToTalk>().App = app;
            replay = Control(answerArea, "Putar ulang", 704, 0, 143, 51, app.Replay, fontSize: 17);
            skip = Control(answerArea, "Lewati", 860, 0, 124, 51, () => app.Answer("", true), fontSize: 17);
        }

        void SetDesktopSection(DesktopSection section)
        {
            desktopSection = section;
            ClearFocus();
            Refresh();
        }

        public void CloseDesktopSection()
        {
            if (desktopSection != DesktopSection.Start)
                SetDesktopSection(DesktopSection.Start);
        }

        void RefreshDesktop(PracticePhase state)
        {
            bool preparing = state == PracticePhase.Setup;
            bool presenting = state == PracticePhase.Presentation;
            bool reviewing = state == PracticePhase.Review;
            Show(masthead, preparing || reviewing || app.Baseline);
            Show(setup, preparing && !avatarPickerOpen);
            Show(avatarPanel, preparing && avatarPickerOpen);
            Show(stageBar, presenting);
            Show(laptopFrame, app.Baseline && !preparing && !reviewing);
            Show(conversation, !preparing && !presenting && !reviewing);
            Show(review, reviewing);
            if (preparing)
            {
                if (!avatarPickerOpen)
                    RefreshDesktopSetup();
                return;
            }
            if (reviewing)
            {
                Set(reviewCounts, SessionReport());
                Set(reviewMessage, app.Session.Suggestion);
                reviewReplay.interactable = app.CanReplay;
                return;
            }
            if (app.Baseline)
            {
                float height = presenting ? 660 : 485;
                laptopFrame.sizeDelta = new Vector2(1216, height);
                ((RectTransform)laptopSlide.transform.parent).sizeDelta = new Vector2(1186, height - 24);
                ShowSlide(laptopSlide, app.Slide);
            }
            if (presenting)
            {
                Set(stageClock, Remaining());
                Set(stageSlide, $"{app.SlideIndex + 1} / {app.Deck.Slides.Count}");
                timerFill.sizeDelta = new Vector2(91 * app.Session.Remaining / PracticeSession.Duration, 4);
                stagePrevious.interactable = app.SlideIndex > 0;
                stageNext.interactable = app.SlideIndex + 1 < app.Deck.Slides.Count;
                SetButton(stagePause, app.Session.Paused ? "Lanjut" : "Jeda");
                Set(stageHint, app.Recording ? "Mikrofon aktif" :
                    app.Baseline ? "← / → Ganti slide  ·  P Jeda" : "V Monitor  ·  P Jeda");
                return;
            }
            RefreshDesktopQuestion(state);
        }

        void RefreshDesktopSetup()
        {
            Show(startCard, desktopSection == DesktopSection.Start);
            Show(slidesCard, desktopSection == DesktopSection.Slides);
            Show(settingsCard, desktopSection == DesktopSection.Settings);
            Show(roomHint, desktopSection == DesktopSection.Start);
            Set(setupNotice, app.Notice);
            switch (desktopSection)
            {
                case DesktopSection.Start:
                    Set(deckCount, $"{app.Deck.Slides.Count} slide");
                    ShowSlide(setupThumbnail, app.Slide);
                    Choice(roomChoice, !baseline);
                    Choice(laptopChoice, baseline);
                    break;
                case DesktopSection.Slides:
                    Set(deckStatus, app.Deck.Status);
                    ShowSlide(setupSlide, app.Slide);
                    break;
                case DesktopSection.Settings:
                    SetButton(languageChoice, english ? "English" : "Bahasa Indonesia");
                    SetButton(metricsChoice, metrics ? "Catat waktu aktif" : "Catat waktu nonaktif");
                    SetButton(cloudChoice, cloud ? "Suara live" : "Suara offline");
                    Choice(cloudChoice, cloud);
                    cloudChoice.interactable = app.HasEndpoint;
                    Set(settingsNotice, "Mode live mengirim topik, audio, dan jawaban ke layanan AI.");
                    break;
            }
        }

        void RefreshDesktopQuestion(PracticePhase state)
        {
            bool requesting = state == PracticePhase.FloorRequest;
            bool answering = state == PracticePhase.Question;
            Set(desktopStatus, QuestionStatus(state));
            Set(desktopNumber, QuestionNumber(state));
            Set(desktopPrompt, QuestionText(state));
            Set(desktopNotice, app.Notice);
            testConnection.interactable = app.FallbackCount == 0 || app.Live;
            Show(grantFloor, requesting);
            Show(desktopHint, !answering);
            Set(desktopHint, requesting ? "Enter memberi giliran." :
                state == PracticePhase.Asking ? "Dengarkan pertanyaan sampai selesai." : "");
            Show(answerArea, answering);
            Show(talk, app.Live);
            Set((Text)answer.placeholder, app.Live ? "Ketik jawaban" : "Ketik atau jawab lantang");
            ((RectTransform)replay.transform).anchoredPosition = new Vector2(app.Live ? 704 : 546, 0);
            ((RectTransform)skip.transform).anchoredPosition = new Vector2(app.Live ? 860 : 702, 0);
            answer.interactable = !app.Recording;
            submit.interactable = !app.Recording;
            replay.interactable = app.CanReplay && !app.Recording;
            skip.interactable = !app.Recording;
            SetButton(submit, app.Live ? "Kirim" : "Sudah jawab");
            SetButton(talk, app.Recording ? "Merekam…" : "Tahan bicara");
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static Texture2D MakePreparationShade()
        {
            const int width = 128;
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false);
            var colors = new Color32[width];
            for (int x = 0; x < width; x++)
            {
                float amount = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.08f, .70f, x / (width - 1f)));
                colors[x] = new Color32(9, 12, 23, (byte)(191 * amount));
            }
            texture.SetPixels32(colors);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply(false, true);
            return texture;
        }
    }
}
