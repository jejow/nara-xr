using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Nara.Practice
{
    [Serializable]
    public sealed class DeckManifest
    {
        public string topic;
        public string summary;
        public string language;
        public string[] slides;
    }

    [Serializable]
    public sealed class VoiceSettings
    {
        public string endpoint;
        public string accessToken;

        public static VoiceSettings Load(string folder)
        {
            var result = new VoiceSettings();
            try
            {
                string file = Path.Combine(folder, "backend.json");
                if (File.Exists(file))
                    result = JsonUtility.FromJson<VoiceSettings>(File.ReadAllText(file)) ?? result;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                Debug.LogWarning("NARA could not read backend.json.");
            }
            result.endpoint = Environment.GetEnvironmentVariable("NARA_ENDPOINT") ?? result.endpoint;
            result.accessToken = Environment.GetEnvironmentVariable("NARA_ACCESS_TOKEN") ?? result.accessToken;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (string.IsNullOrWhiteSpace(result.endpoint))
                result.endpoint = "http://127.0.0.1:8765";
#endif
            string address = (result.endpoint ?? "").Trim().TrimEnd('/');
            result.endpoint = Uri.TryCreate(address, UriKind.Absolute, out Uri uri)
                && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
                && (uri.Scheme == "https" || (uri.Scheme == "http" && uri.IsLoopback))
                ? address : "";
            return result;
        }
    }

    public sealed class PracticeDeck : IDisposable
    {
        readonly List<Texture2D> imported = new(3);
        public readonly List<Texture2D> Slides = new(3);
        public string Folder => Path.Combine(Application.persistentDataPath, "Practice");
        public string Topic { get; private set; }
        public string Summary { get; private set; }
        public string Language { get; private set; }
        public string Status { get; private set; }

        public void Reload()
        {
            Dispose();
            Topic = "Ruang belajar kampus";
            Summary = "Contoh latihan tentang peminjaman ruang belajar. Jelaskan kebutuhan mahasiswa, alur peminjaman, dan cara menguji apakah ruang lebih mudah dipesan.";
            Language = "id";
            Status = "3 slide contoh";
            try
            {
                Directory.CreateDirectory(Folder);
                string path = Path.Combine(Folder, "practice.json");
                if (!File.Exists(path))
                {
                    string example = Path.Combine(Folder, "practice.example.json");
                    if (!File.Exists(example))
                        File.WriteAllText(example, JsonUtility.ToJson(new DeckManifest
                        {
                            topic = "Topik milikmu", summary = "Ringkasan singkat proposal", language = "id",
                            slides = new[] { "slide-1.png", "slide-2.png", "slide-3.png" }
                        }, true));
                }
                else
                {
                    var manifest = JsonUtility.FromJson<DeckManifest>(File.ReadAllText(path));
                    if (manifest == null || manifest.slides == null || manifest.slides.Length < 1 || manifest.slides.Length > 3)
                        throw new InvalidDataException();
                    foreach (string file in manifest.slides)
                        LoadImage(file);
                    Topic = string.IsNullOrWhiteSpace(manifest.topic) ? Topic : manifest.topic.Trim();
                    Summary = manifest.summary ?? "";
                    Language = manifest.language == "en" ? "en" : "id";
                    Slides.AddRange(imported);
                    Status = $"{Slides.Count} slide pengguna";
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                Dispose();
                Status = "Periksa practice.json dan gambar. Slide contoh ditampilkan.";
            }
            if (Slides.Count == 0)
            {
                for (int i = 1; i <= 3; i++)
                {
                    Texture2D image = Resources.Load<Texture2D>("Nara/Slides/slide_" + i);
                    if (image)
                        Slides.Add(image);
                }
            }
        }

        void LoadImage(string file)
        {
            if (string.IsNullOrWhiteSpace(file) || Path.GetFileName(file) != file)
                throw new InvalidDataException();
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                throw new InvalidDataException();
            var image = new FileInfo(Path.Combine(Folder, file));
            if (!image.Exists || image.Length > 6_000_000)
                throw new InvalidDataException();
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            imported.Add(texture);
            if (!texture.LoadImage(File.ReadAllBytes(image.FullName), true) || texture.width > 4096 || texture.height > 4096)
                throw new InvalidDataException();
            texture.wrapMode = TextureWrapMode.Clamp;
        }

        public void Dispose()
        {
            foreach (Texture2D texture in imported)
                UnityEngine.Object.Destroy(texture);
            imported.Clear();
            Slides.Clear();
        }
    }
}
