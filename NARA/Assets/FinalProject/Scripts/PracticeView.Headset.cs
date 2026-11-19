using UnityEngine;
using UnityEngine.UI;

namespace Nara.Practice
{
    public sealed partial class PracticeView
    {
        RectTransform xrSetup, xrAvatars, xrPresentation, xrConversation, xrReview;
        Text xrTopic, xrDeck, xrVoice, xrLanguage, xrMetrics, xrConsent;
        Text xrRole, xrAvatar, xrClock, xrSlide, xrPause, xrMic;
        Text xrStatus, xrQuestion, xrQuestionNumber, xrAction, xrAnswers, xrNotice, xrReport, xrSuggestion;
        RawImage xrPortrait;

        void BuildHeadset(RectTransform root)
        {
            xrSetup = Card(root, "Persiapan headset", Dark, 72, 65, 816, 473);
            Label(xrSetup, "NARA", 23, FontStyle.Bold, Tint, 30, 16, 756, 32);
            Label(xrSetup, "Latihan presentasi", 43, FontStyle.Bold, White, 29, 56, 757, 63);
            xrTopic = Label(xrSetup, "", 30, FontStyle.Bold, White, 30, 126, 755, 51, TextAnchor.MiddleLeft, 24);
            xrDeck = Label(xrSetup, "", 22, FontStyle.Normal, Muted, 30, 179, 754, 33);
            xrVoice = HeadsetAction(xrSetup, "B", "Suara offline", 30, 225, 371);
            xrLanguage = HeadsetAction(xrSetup, "Y", "Bahasa Indonesia", 415, 225, 371);
            HeadsetAction(xrSetup, "Grip kiri", "Pilih avatar", 30, 293, 371);
            xrMetrics = HeadsetAction(xrSetup, "Pelatuk kiri", "Catat waktu mati", 415, 293, 371);
            xrConsent = Label(xrSetup, "", 21, FontStyle.Normal, Tint, 31, 351, 753, 44,
                TextAnchor.MiddleLeft, 19);
            HeadsetAction(xrSetup, "A", "Mulai latihan", 30, 398, 756, true);

            xrAvatars = Card(root, "Avatar headset", Dark, 58, 64, 844, 474);
            Label(xrAvatars, "Pilih avatar", 41, FontStyle.Bold, White, 28, 20, 400, 55);
            HeadsetAction(xrAvatars, "Grip kiri", "Tutup", 528, 20, 285);
            var photo = Card(xrAvatars, "Foto avatar", Navy, 30, 95, 312, 312);
            xrPortrait = Group(photo, "Potret", 6, 6, 300, 300).gameObject.AddComponent<RawImage>();
            xrPortrait.texture = Resources.Load<Texture2D>("Nara/AvatarPortraits");
            xrPortrait.raycastTarget = false;
            xrRole = Label(xrAvatars, "", 24, FontStyle.Bold, Tint, 369, 107, 444, 43);
            xrAvatar = Label(xrAvatars, "", 42, FontStyle.Bold, White, 368, 162, 444, 59, TextAnchor.MiddleLeft, 27);
            HeadsetAction(xrAvatars, "A", "Pindah kursi", 369, 243, 444);
            HeadsetAction(xrAvatars, "Y", "Sebelumnya", 369, 323, 216);
            HeadsetAction(xrAvatars, "B", "Berikutnya", 597, 323, 216);

            xrPresentation = Card(root, "Kendali presentasi headset", Navy, 52, 404, 856, 168);
            xrClock = Label(xrPresentation, "03:00", 39, FontStyle.Bold, White, 24, 7, 145, 55);
            xrSlide = Label(xrPresentation, "", 25, FontStyle.Bold, Tint, 177, 9, 203, 49);
            xrMic = Label(xrPresentation, "", 22, FontStyle.Normal, Muted, 402, 9, 429, 49, TextAnchor.MiddleRight);
            HeadsetAction(xrPresentation, "Y", "Kembali", 22, 64, 169);
            HeadsetAction(xrPresentation, "B", "Lanjut slide", 201, 64, 192);
            xrPause = HeadsetAction(xrPresentation, "Grip kiri", "Jeda", 403, 64, 205);
            HeadsetAction(xrPresentation, "A", "Tanya jawab", 619, 64, 213, true);
            Label(xrPresentation, "Tahan X untuk mengakhiri latihan", 21, FontStyle.Normal, Muted, 24, 128, 806, 29);

            xrConversation = Card(root, "Tanya jawab headset", Navy, 55, 302, 850, 270);
            xrStatus = Label(xrConversation, "", 25, FontStyle.Bold, Tint, 26, 12, 504, 38, TextAnchor.MiddleLeft, 22);
            xrQuestionNumber = Label(xrConversation, "", 21, FontStyle.Normal, Muted, 534, 12, 291, 38, TextAnchor.MiddleRight, 19);
            xrQuestion = Label(xrConversation, "", 30, FontStyle.Bold, White, 26, 57, 798, 103, TextAnchor.UpperLeft, 22);
            var answerCue = Panel(xrConversation, "Giliran", Accent, 26, 164, 419, 54);
            xrAction = Label(answerCue, "", 24, FontStyle.Bold, Ink, 13, 0, 393, 54, TextAnchor.MiddleCenter, 20);
            xrAnswers = Label(xrConversation, "", 21, FontStyle.Normal, Muted, 461, 157, 364, 66, TextAnchor.MiddleLeft, 20);
            xrNotice = Label(xrConversation, "", 21, FontStyle.Normal, Muted, 26, 227, 798, 32, TextAnchor.MiddleLeft, 19);

            xrReview = Card(root, "Refleksi headset", Dark, 69, 91, 822, 407);
            Label(xrReview, "Latihan selesai", 43, FontStyle.Bold, White, 29, 23, 764, 57);
            xrReport = Label(xrReview, "", 23, FontStyle.Normal, Muted, 30, 87, 762, 55);
            xrSuggestion = Label(xrReview, "", 31, FontStyle.Bold, White, 30, 157, 762, 143, TextAnchor.UpperLeft, 23);
            HeadsetAction(xrReview, "Grip kiri", "Putar saran", 30, 321, 373);
            HeadsetAction(xrReview, "A", "Latihan baru", 420, 321, 372, true);
        }

        Text HeadsetAction(RectTransform parent, string key, string title, float x, float y, float width, bool primary = false)
        {
            var strip = Panel(parent, title, primary ? Accent : Raised, x, y, width, 55);
            float keyWidth = key.Length > 8 ? 128 : key.Length > 1 ? 100 : 38;
            var badge = Panel(strip, "Tombol kontroler", primary ? Ink : Navy, 8, 8, keyWidth, 39);
            Label(badge, key, 20, FontStyle.Bold, primary ? Tint : White, 4, 0, keyWidth - 8, 39, TextAnchor.MiddleCenter);
            return Label(strip, title, 23, FontStyle.Bold, primary ? Ink : White, keyWidth + 20, 0,
                width - keyWidth - 28, 55, TextAnchor.MiddleLeft, 20);
        }

        void RefreshHeadset(PracticePhase state)
        {
            bool preparing = state == PracticePhase.Setup;
            bool presenting = state == PracticePhase.Presentation;
            bool reviewing = state == PracticePhase.Review;
            bool asking = !preparing && !presenting && !reviewing;
            canvas.transform.localPosition = asking ? new Vector3(0, -.22f, 0) : Vector3.zero;
            Show(xrSetup, preparing && !avatarPickerOpen);
            Show(xrAvatars, preparing && avatarPickerOpen);
            Show(xrPresentation, presenting);
            Show(xrConversation, asking);
            Show(xrReview, reviewing);
            if (preparing)
            {
                if (avatarPickerOpen)
                {
                    int index = app.audience.Selection(avatarSeat);
                    Set(xrRole, SeatNames[avatarSeat]);
                    Set(xrAvatar, app.audience.Profiles[index].label);
                    xrPortrait.uvRect = PortraitUv(index);
                    return;
                }
                Set(xrTopic, topic.text);
                Set(xrDeck, app.Deck.Status + "  ·  3 menit presentasi");
                Set(xrVoice, cloud ? "Suara live" : "Suara offline");
                Set(xrLanguage, english ? "English" : "Bahasa Indonesia");
                Set(xrMetrics, metrics ? "Catat waktu aktif" : "Catat waktu mati");
                Set(xrConsent, string.IsNullOrEmpty(app.Notice) ?
                    cloud ? "Mode live mengirim topik, audio, dan jawaban ke layanan AI." : "" : app.Notice);
                return;
            }
            if (presenting)
            {
                Set(xrClock, Remaining());
                Set(xrSlide, $"Slide {app.SlideIndex + 1} / {app.Deck.Slides.Count}");
                Set(xrPause, app.Session.Paused ? "Lanjut" : "Jeda");
                Set(xrMic, app.Recording ? "Mikrofon aktif" : app.Live ? "Grip kanan untuk bicara" : "");
                return;
            }
            if (reviewing)
            {
                Set(xrReport, SessionReport());
                Set(xrSuggestion, app.Session.Suggestion);
                return;
            }
            bool answering = state == PracticePhase.Question;
            Set(xrStatus, QuestionStatus(state));
            Set(xrQuestionNumber, QuestionNumber(state));
            Set(xrQuestion, QuestionText(state));
            Set(xrAction, state == PracticePhase.FloorRequest ? "A  Persilakan bertanya" :
                answering ? app.Live ? "Grip kanan  Tahan bicara" : "A  Sudah jawab" :
                state == PracticePhase.Asking ? "Dengarkan pertanyaan" : "Tunggu sebentar");
            Set(xrAnswers, answering ? app.CanReplay ? "Grip kiri putar ulang\nPelatuk kiri lewati" : "Pelatuk kiri lewati" : "");
            Set(xrNotice, string.IsNullOrEmpty(app.Notice) ? "Tahan X untuk mengakhiri latihan" : app.Notice);
        }
    }
}
