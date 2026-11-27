using System;
using UnityEngine;
using UnityEngine.UI;

namespace Nara.Practice
{
    public sealed partial class PracticeView
    {
        RectTransform questSettings;
        Text questKey, questDeck, questTopic, questSummary, questNotice;
        TouchScreenKeyboard questKeyboard;
        Action<string> acceptQuestText;
        bool releaseQuestControls;
        internal bool QuestSettingsOpen { get; private set; }
        internal bool QuestTyping => questKeyboard != null;

        void BuildQuestSettings(RectTransform root)
        {
            questSettings = Card(root, "Pengaturan Quest", Dark, 67, 50, 826, 514);
            Label(questSettings, "Pengaturan Quest", 40, FontStyle.Bold, White, 30, 22, 766, 58);
            questKey = HeadsetAction(questSettings, "A", "Masukkan kunci AISUBS", 30, 97, 766);
            questDeck = HeadsetAction(questSettings, "B", "Pilih materi di headset", 30, 162, 766);
            questTopic = HeadsetAction(questSettings, "X", "Topik latihan", 30, 227, 766);
            questSummary = HeadsetAction(questSettings, "Pelatuk kanan", "Ringkasan materi", 30, 292, 766);
            questNotice = Label(questSettings, "", 22, FontStyle.Normal, Tint, 30, 365, 766, 68, TextAnchor.MiddleLeft, 19);
            HeadsetAction(questSettings, "Y", "Kembali", 30, 441, 372, true);
            HeadsetAction(questSettings, "Grip kiri", "Hapus kunci", 417, 441, 379);
        }

        void RefreshQuestSettings(bool preparing)
        {
            if (!preparing)
                QuestSettingsOpen = false;
            Show(questSettings, QuestSettingsOpen);
            if (!QuestSettingsOpen)
                return;
            Set(questKey, NaraQuestBridge.Configured ? "Ubah kunci AISUBS · tersimpan" : "Masukkan kunci AISUBS");
            Set(questDeck, app.Deck.HasImportedFile ? $"Ganti materi · {app.Deck.Count} slide" : "Pilih materi di headset");
            Set(questTopic, string.IsNullOrWhiteSpace(topic.text) ? "Isi topik latihan" : $"Topik · {Shorten(topic.text, 45)}");
            Set(questSummary, string.IsNullOrWhiteSpace(summary.text) ? "Isi ringkasan materi" : $"Ringkasan · {Shorten(summary.text, 41)}");
            Set(questNotice, !string.IsNullOrEmpty(app.Notice) ? app.Notice :
                !string.IsNullOrEmpty(app.DeckImportNotice) ? app.DeckImportNotice :
                "PPTX, PPT, PDF, JPG, PNG · paling besar 32 MB");
        }

        static string Shorten(string value, int limit) => value.Length > limit ? value[..(limit - 1)] + "…" : value;

        internal void QuestSetupControls(bool a, bool b, bool x, bool y, bool rightGrip, bool leftGrip, bool trigger)
        {
            if (rightGrip || y)
            {
                QuestSettingsOpen = !QuestSettingsOpen;
                return;
            }
            if (a)
            {
                OpenQuestKeyboard("", 190, true, text =>
                {
                    try
                    {
                        app.Notify(NaraQuestBridge.SaveKey(text) ?
                            "Kunci tersimpan. Kembali dan tekan B untuk mengaktifkan AI." :
                            "Kunci belum disimpan. Gunakan kunci AISUBS yang dimulai sk-.");
                    }
                    catch (AndroidJavaException) { app.Notify("Penyimpanan kunci belum tersedia. Buka ulang NARA."); }
                });
            }
            else if (b)
                app.PickDeck();
            else if (x)
                OpenQuestKeyboard(topic.text, 100, false, value => topic.text = value);
            else if (trigger)
                OpenQuestKeyboard(summary.text, 1500, false, value => summary.text = value, true);
            else if (leftGrip && NaraQuestBridge.Configured)
            {
                try
                {
                    if (NaraQuestBridge.ClearKey())
                    {
                        SavePreference(VoicePreference, cloud = false);
                        app.Notify("Kunci dihapus dari headset.");
                    }
                    else
                        app.Notify("Kunci belum terhapus. Coba lagi.");
                }
                catch (AndroidJavaException) { app.Notify("Penyimpanan kunci belum tersedia. Buka ulang NARA."); }
            }
        }

        internal void OpenQuestResponseKeyboard()
        {
            if (app.Session.Phase == PracticePhase.Presentation)
            {
                OpenQuestKeyboard(writtenPresentation.text, 7000, false, value =>
                {
                    if (app.Session.Phase == PracticePhase.Presentation)
                        writtenPresentation.text = value;
                }, true);
            }
            else if (app.Session.Phase == PracticePhase.Question && app.Live)
            {
                OpenQuestKeyboard(answer.text, 2000, false, value =>
                {
                    if (app.Session.Phase == PracticePhase.Question)
                    {
                        answer.text = value;
                        Submit();
                    }
                }, true);
            }
        }

        void OpenQuestKeyboard(string initial, int limit, bool secure, Action<string> accept, bool multiline = false)
        {
            if (questKeyboard != null)
                return;
            if (!TouchScreenKeyboard.isSupported)
            {
                app.Notify("Keyboard sistem belum tersedia di perangkat ini.");
                return;
            }
            questKeyboard = TouchScreenKeyboard.Open(initial ?? "", TouchScreenKeyboardType.Default, false, multiline, secure,
                false, "", limit);
            if (questKeyboard == null)
            {
                app.Notify("Keyboard sistem belum bisa dibuka.");
                return;
            }
            acceptQuestText = accept;
            releaseQuestControls = true;
        }

        internal bool UpdateQuestInputGate(bool anyHeld)
        {
            if (questKeyboard != null || app.PickingDocument)
            {
                releaseQuestControls = true;
                return true;
            }
            if (!releaseQuestControls)
                return false;
            releaseQuestControls = anyHeld;
            return true;
        }

        void Update()
        {
            if (questKeyboard == null)
                return;
            TouchScreenKeyboard.Status status = questKeyboard.status;
            if (status == TouchScreenKeyboard.Status.Visible)
                return;
            var keyboard = questKeyboard;
            var complete = acceptQuestText;
            questKeyboard = null;
            acceptQuestText = null;
            if (status == TouchScreenKeyboard.Status.Done)
                complete?.Invoke(keyboard.text.Trim());
            keyboard.active = false;
        }
    }
}
