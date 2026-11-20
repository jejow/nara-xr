using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
    public sealed class DeckPage
    {
        public int page;
        public string text;
    }

    [Serializable]
    sealed class ImportedDeck
    {
        public int version;
        public string topic;
        public DeckPage[] pages;
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
        internal const long MaxArchiveBytes = 32_000_000;
        const int MaxPages = 60;
        const int MaxJpegBytes = 1_500_000;
        readonly List<Texture2D> imported = new(3);
        readonly List<Texture2D> slides = new(3);
        ImportedDeck archive;
        Texture2D pageImage;
        int cachedPage = -1;
        string smokeFolder;
        public string Folder => smokeFolder ?? Path.Combine(Application.persistentDataPath, "Practice");
        string ArchivePath => Path.Combine(Folder, "imported-deck.zip");
        public int Count => archive?.pages.Length ?? slides.Count;
        public DeckPage[] Pages => archive?.pages ?? Array.Empty<DeckPage>();
        public bool HasImportedFile => archive != null;
        public string Topic { get; private set; }
        public string Summary { get; private set; }
        public string Language { get; private set; }
        public string Status { get; private set; }

        internal void UseSmokeFolder(string path)
        {
            if (Application.isBatchMode)
                smokeFolder = path;
        }

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
                if (File.Exists(ArchivePath))
                {
                    archive = ReadArchive(ArchivePath);
                    if (!string.IsNullOrWhiteSpace(archive.topic))
                        Topic = archive.topic.Trim();
                    else
                        Topic = "";
                    Summary = "";
                    int withText = Array.FindAll(archive.pages, page => !string.IsNullOrWhiteSpace(page.text)).Length;
                    Status = withText == 0 ? $"{Count} slide · tambahkan ringkasan atau penjelasan" :
                        $"{Count} slide · teks terbaca pada {withText} slide";
                    return;
                }
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
                    slides.AddRange(imported);
                    Status = $"{slides.Count} slide pengguna";
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException ||
                ex is InvalidDataException)
            {
                Dispose();
                Status = "Materi tersimpan tidak terbaca. Slide contoh ditampilkan.";
            }
            if (slides.Count == 0)
            {
                for (int i = 1; i <= 3; i++)
                {
                    Texture2D image = Resources.Load<Texture2D>("Nara/Slides/slide_" + i);
                    if (image)
                        slides.Add(image);
                }
            }
        }

        public Texture2D GetSlide(int index)
        {
            if (index < 0 || index >= Count)
                return null;
            if (archive == null)
                return slides[index];
            if (cachedPage == index)
                return pageImage;

            if (pageImage)
                UnityEngine.Object.Destroy(pageImage);
            cachedPage = index;
            pageImage = null;
            try
            {
                using var zip = ZipFile.OpenRead(ArchivePath);
                pageImage = ReadPage(zip, index + 1);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException ||
                ex is InvalidDataException)
            {
                Debug.LogWarning("NARA could not display the selected page.");
            }
            return pageImage;
        }

        internal void InstallArchive(string incoming)
        {
            ReadArchive(incoming, checkPreview: true);
            if (File.Exists(ArchivePath))
                File.Replace(incoming, ArchivePath, null);
            else
                File.Move(incoming, ArchivePath);
        }

        internal void DiscardArchive() => File.Delete(ArchivePath);

        static ImportedDeck ReadArchive(string path, bool checkPreview = false)
        {
            if (new FileInfo(path).Length > MaxArchiveBytes)
                throw new InvalidDataException();
            using var zip = ZipFile.OpenRead(path);
            ZipArchiveEntry entry = zip.GetEntry("deck.json");
            if (entry == null || entry.Length is <= 0 or > 1_000_000)
                throw new InvalidDataException();
            using var reader = new StreamReader(entry.Open());
            var manifest = JsonUtility.FromJson<ImportedDeck>(reader.ReadToEnd());
            if (manifest?.version != 1 || manifest.pages == null || manifest.pages.Length is < 1 or > MaxPages)
                throw new InvalidDataException();
            if (manifest.topic?.Length > 100 || zip.Entries.Count != manifest.pages.Length + 1)
                throw new InvalidDataException();
            for (int i = 0; i < manifest.pages.Length; i++)
            {
                DeckPage page = manifest.pages[i];
                if (page?.page != i + 1 || page.text?.Length > 2500)
                    throw new InvalidDataException();
                PageEntry(zip, page.page);
            }
            if (checkPreview)
                UnityEngine.Object.Destroy(ReadPage(zip, 1));
            return manifest;
        }

        static ZipArchiveEntry PageEntry(ZipArchive zip, int page)
        {
            ZipArchiveEntry entry = zip.GetEntry($"slide-{page:000}.jpg");
            if (entry == null || entry.Length is <= 0 or > MaxJpegBytes)
                throw new InvalidDataException();
            return entry;
        }

        static Texture2D ReadPage(ZipArchive zip, int page)
        {
            using var stream = PageEntry(zip, page).Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!texture.LoadImage(buffer.ToArray(), true) || Mathf.Max(texture.width, texture.height) > 1600)
            {
                UnityEngine.Object.Destroy(texture);
                throw new InvalidDataException();
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
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
            if (pageImage)
                UnityEngine.Object.Destroy(pageImage);
            archive = null;
            pageImage = null;
            cachedPage = -1;
            foreach (Texture2D texture in imported)
                UnityEngine.Object.Destroy(texture);
            imported.Clear();
            slides.Clear();
        }
    }
}
