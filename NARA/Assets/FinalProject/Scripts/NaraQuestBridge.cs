using System.IO;
using UnityEngine;

namespace Nara.Practice
{
    internal static class NaraQuestBridge
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        const string Service = "id.ac.its.nara.quest.NaraQuest";
        static VoiceSettings current;
        static bool started;
        static bool configured;
        internal static bool IsStandalone => true;
        internal static bool Configured => configured;

        internal static VoiceSettings Settings()
        {
            if (started)
                return current;
            started = true;
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var local = new AndroidJavaClass(Service);
                string avatarCatalog = Resources.Load<TextAsset>("Nara/AvatarCatalog").text;
                string prompt = Resources.Load<TextAsset>("Nara/DialoguePrompt").text;
                string settings = local.CallStatic<string>("start", activity, avatarCatalog, prompt);
                if (!string.IsNullOrWhiteSpace(settings))
                {
                    current = JsonUtility.FromJson<VoiceSettings>(settings);
                    configured = local.CallStatic<bool>("hasKey");
                }
            }
            catch (AndroidJavaException)
            {
                Debug.LogWarning("NARA could not initialize Quest services.");
            }
            return current ??= new VoiceSettings();
        }

        internal static bool SaveKey(string key)
        {
            using var local = new AndroidJavaClass(Service);
            if (!local.CallStatic<bool>("saveKey", key))
                return false;
            return configured = true;
        }

        internal static bool ClearKey()
        {
            using var local = new AndroidJavaClass(Service);
            if (!local.CallStatic<bool>("clearKey"))
                return false;
            configured = false;
            return true;
        }

        internal static bool PickDocument()
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var picker = new AndroidJavaClass("id.ac.its.nara.quest.NaraDocumentPicker");
            return picker.CallStatic<bool>("open", activity);
        }

        internal static string ReadDocument()
        {
            using var picker = new AndroidJavaClass("id.ac.its.nara.quest.NaraDocumentPicker");
            return picker.CallStatic<string>("takeResult");
        }

        internal static void DeleteSelection(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            string folder = Path.GetDirectoryName(path);
            if (!Path.GetFileName(folder).StartsWith("selection-", System.StringComparison.Ordinal))
                return;
            try
            {
                File.Delete(path);
                Directory.Delete(folder);
            }
            catch (System.Exception error) when (error is IOException or System.UnauthorizedAccessException) { }
        }
#else
        internal static bool IsStandalone => false;
        internal static bool Configured => false;
        internal static VoiceSettings Settings() => new();
        internal static bool SaveKey(string key) => false;
        internal static bool ClearKey() => false;
        internal static bool PickDocument() => false;
        internal static string ReadDocument() => "";
        internal static void DeleteSelection(string path) { }
#endif
    }
}
