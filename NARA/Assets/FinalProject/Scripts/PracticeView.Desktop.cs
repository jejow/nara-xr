using UnityEngine;
using UnityEngine.UI;

namespace Nara.Practice
{
    public sealed partial class PracticeView
    {
        enum DesktopSection { Start, Slides, Settings }

        DesktopSection desktopSection;
        RectTransform desktopBackdrop, preparationShade, masthead, setup, startCard, slidesCard, settingsCard, roomHint;
        RectTransform stageBar, writtenPanel, laptopFrame, conversation, listeningBar, answerArea, review, timerFill;
        RawImage setupThumbnail, setupSlide, laptopSlide;
        Text deckCount, deckStatus, deckPreviewNumber, setupNotice, settingsNotice, stageClock, stageSlide, stageHint;
        RectTransform stageGreetingCard;
        Text stageGreeting;
        Text desktopPrompt, desktopStatus, desktopNumber, listeningNumber, desktopHint, desktopNotice, reviewMessage, reviewCounts, reviewSpeaker;
        Button roomChoice, laptopChoice, universityChoice, if101Choice, setupProjection, stageProjection, startPractice;
        Button selectFile, exampleDeck, previewPrevious, previewNext, stageMic, writePresentation;
        InputField writtenPresentation;
        bool composingPresentation;
        Button languageChoice, metricsChoice, cloudChoice;
        Button stagePrevious, stageNext, stagePause, grantFloor, submit, talk, skip, replay, reviewReplay;
        Button previousText, nextText;
        Text textPage;

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
            setupProjection = Control(setup, "Lihat PPT", 1101, 738, 295, 50, app.ToggleProjection);
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
            reviewSpeaker = Label(review, "", 19, FontStyle.Bold, Tint, 38, 190, 716, 30);
            reviewMessage = Label(review, "", 28, FontStyle.Normal, White, 38, 232, 716, 107, TextAnchor.UpperLeft, 20);
            reviewReplay = Control(review, "Putar saran", 38, 353, 238, 52, app.Replay);
            Control(review, "Latihan baru", 291, 353, 463, 52, app.ResetPractice, true, 20);
        }

        void BuildStartCard(RectTransform root)
        {
            startCard = Card(root, "Mulai latihan presentasi", Dark, 44, 156, 412, 656);
            Label(startCard, "Latihan", 48, FontStyle.Bold, White, 28, 21, 358, 64);
            Label(startCard, "presentasi", 48, FontStyle.Bold, White, 28, 74, 358, 64);
            Label(startCard, "3 menit · 2 pertanyaan", 20, FontStyle.Normal, Muted, 30, 148, 344, 37);
            Panel(startCard, "Pemisah", Stroke, 29, 196, 354, 1, false);
            Label(startCard, "Topik", 18, FontStyle.Bold, Tint, 29, 207, 353, 31);
            topic = Field(startCard, 28, 241, 356, 49, "Judul presentasi", 100);
            roomChoice = Control(startCard, "Ruang 3D", 28, 303, 172, 44, () => SetBaseline(false));
            laptopChoice = Control(startCard, "Layar laptop", 212, 303, 172, 44, () => SetBaseline(true));
            Label(startCard, "Kelas", 18, FontStyle.Bold, Tint, 29, 357, 353, 25);
            universityChoice = Control(startCard, "Kelas universitas", 28, 389, 172, 44, () => SelectClassroom(false), false, 17);
            if101Choice = Control(startCard, NaraPractice.IfClassroomName, 212, 389, 172, 44, () => SelectClassroom(true));

            var material = Panel(startCard, "Materi latihan", Raised, 28, 450, 356, 67);
            MakeButton(material, () => SetDesktopSection(DesktopSection.Slides));
            setupThumbnail = SlideFrame(material, 11, 9, 87, 49);
            Label(material, "Materi latihan", 19, FontStyle.Bold, White, 111, 7, 205, 28);
            deckCount = Label(material, "", 16, FontStyle.Normal, Muted, 111, 35, 205, 24);
            Label(material, "›", 28, FontStyle.Normal, Tint, 316, 11, 28, 45, TextAnchor.MiddleCenter);
            Control(startCard, "Pengaturan", 28, 530, 356, 43, () => SetDesktopSection(DesktopSection.Settings));
            startPractice = Control(startCard, "Mulai latihan", 28, 585, 356, 49, Begin, true, 21);

            setupNotice = Label(root, "", 19, FontStyle.Bold, White, 47, 827, 670, 44);
        }

        void BuildSlidesCard(RectTransform root)
        {
            slidesCard = Card(root, "Pengaturan materi", Dark, 44, 126, 554, 720);
            Label(slidesCard, "Materi latihan", 37, FontStyle.Bold, White, 29, 22, 366, 53);
            Control(slidesCard, "Kembali", 414, 29, 111, 43, CloseDesktopSection);
            Label(slidesCard, "Ringkasan", 21, FontStyle.Bold, Tint, 30, 102, 493, 32);
            summary = Field(slidesCard, 29, 142, 496, 97, "Tujuan dan isi presentasi", 1500, true);
            setupSlide = SlideFrame(slidesCard, 29, 256, 496, 242);
            previewPrevious = Control(slidesCard, "‹", 29, 510, 47, 41, () => app.PreviewSlide(-1), fontSize: 24);
            deckPreviewNumber = Label(slidesCard, "", 18, FontStyle.Bold, Tint, 83, 510, 108, 41, TextAnchor.MiddleCenter);
            previewNext = Control(slidesCard, "›", 199, 510, 47, 41, () => app.PreviewSlide(1), fontSize: 24);
            Control(slidesCard, "Folder slide", 261, 510, 126, 41, OpenSlides, fontSize: 15);
            Control(slidesCard, "Muat ulang", 400, 510, 125, 41, app.ReloadDeck, fontSize: 15);
            selectFile = Control(slidesCard, "Impor berkas", 29, 570, 241, 51, app.PickDeck, true, 19);
            exampleDeck = Control(slidesCard, "Tutup berkas", 284, 570, 241, 51, app.UseExampleDeck, fontSize: 17);
            deckStatus = Label(slidesCard, "", 18, FontStyle.Normal, Muted, 30, 633, 496, 64,
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
            Label(settingsCard, "Tersimpan otomatis di perangkat ini", 17, FontStyle.Normal, Muted, 28, 488, 444, 29);
        }

        void BuildDesktopPresentation(RectTransform root)
        {
            stageGreetingCard = Card(root, "Sapaan panel", Navy, 360, 726, 720, 54);
            stageGreeting = Label(stageGreetingCard, "", 19, FontStyle.Bold, White, 16, 0, 688, 54, TextAnchor.MiddleCenter);
            stageBar = Card(root, "Kendali presentasi 3D", Navy, 75, 790, 1290, 73);
            stageClock = Label(stageBar, "03:00", 29, FontStyle.Bold, White, 17, 5, 109, 53, TextAnchor.MiddleCenter);
            var track = Panel(stageBar, "Sisa waktu", Raised, 26, 59, 91, 4, false);
            timerFill = Panel(track, "Waktu berjalan", Accent, 0, 0, 91, 4, false);
            stagePrevious = Control(stageBar, "‹", 151, 13, 45, 47, () => app.ChangeSlide(-1), false, 25);
            stagePrevious.gameObject.name = "Slide sebelumnya";
            stageSlide = Label(stageBar, "", 20, FontStyle.Bold, Tint, 205, 13, 111, 47, TextAnchor.MiddleCenter);
            stageNext = Control(stageBar, "›", 325, 13, 45, 47, () => app.ChangeSlide(1), false, 25);
            stageNext.gameObject.name = "Slide berikutnya";
            stagePause = Control(stageBar, "Jeda", 384, 13, 88, 47, app.TogglePause);
            stageMic = Control(stageBar, "Mulai mic", 486, 13, 126, 47, ToggleMicrophone, fontSize: 16);
            writePresentation = Control(stageBar, "Tulis penjelasan", 625, 13, 174, 47, OpenWrittenPresentation, fontSize: 17);
            Control(stageBar, "Masuk Q&A", 812, 13, 157, 47, app.FinishPresentation, true, 19);
            Control(stageBar, "Akhiri", 982, 13, 90, 47, app.ResetPractice);
            stageProjection = Control(stageBar, "Lihat PPT", 1085, 13, 185, 47, app.ToggleProjection, fontSize: 17);
            stageHint = Label(stageBar, "", 16, FontStyle.Normal, Muted, 1085, 11, 185, 52, TextAnchor.MiddleCenter);

            writtenPanel = Card(root, "Penjelasan tertulis", Dark, 292, 251, 856, 454);
            Label(writtenPanel, "Penjelasan presentasi", 32, FontStyle.Bold, White, 29, 20, 707, 45);
            writtenPresentation = Field(writtenPanel, 28, 86, 800, 279,
                "Tulis apa yang kamu jelaskan kepada audiens", 7000, true);
            writtenPresentation.onValueChanged.AddListener(app.Session.WritePresentation);
            Control(writtenPanel, "Selesai", 649, 386, 179, 48, CloseWrittenPresentation, true, 19);
        }

        void BuildDesktopConversation(RectTransform root)
        {
            conversation = Card(root, "Percakapan", Dark, 134, 624, 1172, 239);
            desktopStatus = Label(conversation, "", 20, FontStyle.Bold, Tint, 28, 11, 583, 38);
            desktopNumber = Label(conversation, "", 17, FontStyle.Normal, Muted, 619, 11, 525, 38, TextAnchor.MiddleRight);
            previousText = Control(conversation, "‹", 409, 12, 43, 36, () => TurnQuestionPage(-1));
            textPage = Label(conversation, "", 16, FontStyle.Normal, Tint, 455, 12, 74, 36, TextAnchor.MiddleCenter);
            nextText = Control(conversation, "›", 532, 12, 43, 36, () => TurnQuestionPage(1));
            desktopPrompt = Label(conversation, "", 30, FontStyle.Bold, White, 28, 57, 1116, 78, TextAnchor.UpperLeft, 21);
            grantFloor = Control(conversation, "Persilakan bertanya", 28, 142, 261, 51, app.GrantFloor, true, 19);
            desktopHint = Label(conversation, "", 18, FontStyle.Normal, Muted, 312, 142, 700, 51);
            Control(conversation, "Akhiri", 1034, 142, 110, 51, app.ResetPractice);
            desktopNotice = Label(conversation, "", 16, FontStyle.Normal, Tint, 28, 197, 1116, 31);

            answerArea = Group(conversation, "Jawaban", 28, 142, 984, 51);
            answer = Field(answerArea, 0, 0, 375, 51, "Ketik jawaban", 2000);
            submit = Control(answerArea, "Sudah jawab", 388, 0, 145, 51, Submit, true, 18);
            talk = Control(answerArea, "Mulai mic", 546, 0, 145, 51, ToggleMicrophone, fontSize: 17);
            replay = Control(answerArea, "Putar ulang", 704, 0, 143, 51, app.Replay, fontSize: 17);
            skip = Control(answerArea, "Lewati", 860, 0, 124, 51, () => app.Answer("", true), fontSize: 17);

            listeningBar = Card(root, "Kendali saat NPC bicara", Navy, 395, 789, 650, 74);
            listeningNumber = Label(listeningBar, "", 20, FontStyle.Bold, Tint, 19, 10, 470, 54);
            Control(listeningBar, "Akhiri", 506, 13, 129, 48, app.ResetPractice);
        }

        void ToggleMicrophone()
        {
            if (app.Recording)
                app.EndTalk();
            else
                app.BeginTalk();
        }

        void OpenWrittenPresentation()
        {
            if (app.Session.Phase != PracticePhase.Presentation || app.Recording)
                return;
            composingPresentation = true;
            writtenPresentation.SetTextWithoutNotify(app.Session.WrittenPresentation);
            Refresh();
            writtenPresentation.ActivateInputField();
        }

        void CloseWrittenPresentation()
        {
            composingPresentation = false;
            ClearFocus();
            Refresh();
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
            bool seeingSpeaker = state == PracticePhase.Asking && SpeechBubbleVisible;
            Show(stageGreetingCard, presenting && !SpeechBubbleVisible && !string.IsNullOrEmpty(app.GreetingText));
            Set(stageGreeting, string.IsNullOrEmpty(app.GreetingText) ? "" : SpokenPage);
            Show(masthead, preparing || reviewing || app.Baseline);
            Show(setup, preparing);
            Show(stageBar, presenting && !composingPresentation);
            Show(writtenPanel, presenting && composingPresentation);
            Show(laptopFrame, app.Baseline && !preparing && !reviewing);
            Show(conversation, !preparing && !presenting && !reviewing && !seeingSpeaker);
            Show(listeningBar, seeingSpeaker);
            Show(review, reviewing);
            if (preparing)
            {
                RefreshDesktopSetup();
                return;
            }
            if (reviewing)
            {
                Set(reviewCounts, SessionReport());
                Set(reviewSpeaker, $"Saran dari {HostName}");
                Set(reviewMessage, string.IsNullOrEmpty(app.SpeechText) ? app.Session.Suggestion : app.SpeechText);
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
                Set(stageSlide, $"{app.SlideIndex + 1} / {app.Deck.Count}");
                timerFill.sizeDelta = new Vector2(91 * app.Session.Remaining / PracticeSession.Duration, 4);
                stagePrevious.interactable = app.SlideIndex > 0;
                stageNext.interactable = app.SlideIndex + 1 < app.Deck.Count;
                SetButton(stagePause, app.Session.Paused ? "Lanjut" : "Jeda");
                Show(stageMic, app.Live);
                Show(writePresentation, app.Live);
                stageMic.interactable = app.Recording || !app.Session.Paused && string.IsNullOrEmpty(app.GreetingText);
                writePresentation.interactable = !app.Recording;
                SetButton(stageMic, app.Recording ? "Matikan mic" : "Mulai mic");
                bool project = app.IsIf101 && !app.Baseline && !app.Recording;
                Show(stageProjection, project);
                SetButton(stageProjection, app.ViewingProjection ? "Lihat audiens  ·  V" : "Lihat PPT  ·  V");
                Show(stageHint, !project);
                Set(stageHint, !string.IsNullOrEmpty(app.Notice) ? app.Notice : app.Recording ? "Mikrofon aktif" :
                    app.Baseline ? "← / → Ganti slide" : "V Monitor");
                return;
            }
            if (seeingSpeaker)
            {
                Set(listeningNumber, QuestionNumber(state));
            }
            else
                RefreshDesktopQuestion(state);
        }

        void RefreshDesktopSetup()
        {
            Show(startCard, desktopSection == DesktopSection.Start);
            Show(slidesCard, desktopSection == DesktopSection.Slides);
            Show(settingsCard, desktopSection == DesktopSection.Settings);
            Show(roomHint, desktopSection == DesktopSection.Start);
            Show(setupProjection, app.IsIf101 && desktopSection == DesktopSection.Start);
            SetButton(setupProjection, app.ViewingProjection ? "Lihat audiens  ·  V" : "Lihat PPT  ·  V");
            Set(setupNotice, app.Notice);
            switch (desktopSection)
            {
                case DesktopSection.Start:
                    Set(deckCount, $"{app.Deck.Count} slide");
                    ShowSlide(setupThumbnail, app.Slide);
                    Choice(roomChoice, !baseline);
                    Choice(laptopChoice, baseline);
                    Choice(universityChoice, !app.IsIf101);
                    Choice(if101Choice, app.IsIf101);
                    startPractice.interactable = universityChoice.interactable = if101Choice.interactable =
                        !app.LoadingClassroom && !app.ImportingDeck;
                    break;
                case DesktopSection.Slides:
                    Set(deckStatus, string.IsNullOrEmpty(app.DeckImportNotice) ? app.Deck.Status : app.DeckImportNotice);
                    Set(deckPreviewNumber, $"{app.SlideIndex + 1} / {app.Deck.Count}");
                    selectFile.interactable = !app.ImportingDeck;
                    Show(exampleDeck, app.Deck.HasImportedFile);
                    exampleDeck.interactable = !app.ImportingDeck;
                    previewPrevious.interactable = !app.ImportingDeck && app.SlideIndex > 0;
                    previewNext.interactable = !app.ImportingDeck && app.SlideIndex + 1 < app.Deck.Count;
                    ShowSlide(setupSlide, app.Slide);
                    break;
                case DesktopSection.Settings:
                    SetButton(languageChoice, english ? "English" : "Bahasa Indonesia");
                    SetButton(metricsChoice, metrics ? "Catat waktu aktif" : "Catat waktu nonaktif");
                    SetButton(cloudChoice, cloud ? "Suara live" : "Suara offline");
                    Choice(cloudChoice, cloud);
                    cloudChoice.interactable = app.LiveAvailable;
                    Set(settingsNotice, LiveDataNotice);
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
            Show(previousText, CanPageQuestion);
            Show(textPage, CanPageQuestion);
            Show(nextText, CanPageQuestion);
            if (CanPageQuestion)
            {
                Set(textPage, $"{readingPage + 1} / {speechPages.Count}");
                previousText.interactable = readingPage > 0;
                nextText.interactable = readingPage + 1 < speechPages.Count;
            }
            Set(desktopNotice, answering && app.PreparingSpeech && string.IsNullOrEmpty(app.Notice)
                ? "Suara NPC sedang disiapkan. Kamu bisa langsung menjawab." : app.Notice);
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
            SetButton(talk, app.Recording ? "Kirim rekaman" : "Mulai mic");
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
