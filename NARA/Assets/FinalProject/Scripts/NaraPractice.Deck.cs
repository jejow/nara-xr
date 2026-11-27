using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Nara.Practice
{
    public sealed partial class NaraPractice
    {
        UnityWebRequest deckRequest;
        string pendingDeck;
        string pickedDocument;
        bool pickingDocument;
        public bool ImportingDeck => deckRequest != null;
        internal bool PickingDocument => pickingDocument;
        public bool CanImportDeck => HasEndpoint && new Uri(settings.endpoint).IsLoopback &&
            !string.IsNullOrEmpty(settings.accessToken);
        public string DeckImportNotice { get; private set; } = "";

        public void PreviewSlide(int delta)
        {
            if (Session.Phase != PracticePhase.Setup || ImportingDeck)
                return;
            SlideIndex = Mathf.Clamp(SlideIndex + delta, 0, Mathf.Max(0, Deck.Count - 1));
            ApplySlide();
        }

        public void PickDeck()
        {
            if (Session.Phase != PracticePhase.Setup || ImportingDeck || pickingDocument)
                return;
            if (!CanImportDeck)
            {
                DeckImportNotice = NaraQuestBridge.IsStandalone ? "Impor berkas belum siap. Buka ulang NARA." :
                    "Buka NARA lewat NARA Live.cmd untuk mengimpor berkas.";
                return;
            }
            if (NaraQuestBridge.IsStandalone)
            {
                pickingDocument = NaraQuestBridge.PickDocument();
                DeckImportNotice = pickingDocument ? "Pilih materi dari penyimpanan headset." : "Pemilih berkas masih terbuka.";
                return;
            }
            string selected = PracticeFilePicker.SelectPresentation();
            if (!string.IsNullOrEmpty(selected))
                ImportDeck(selected);
        }

        void ReceiveQuestDocument()
        {
            if (!pickingDocument)
                return;
            string result = NaraQuestBridge.ReadDocument();
            if (string.IsNullOrEmpty(result))
                return;
            pickingDocument = false;
            if (result[0] == '!')
            {
                DeckImportNotice = result switch
                {
                    "!cancel" => "",
                    "!format" => "Pilih PPTX, PPT, PDF, PNG, atau JPEG.",
                    "!picker" => "Pemilih berkas Android belum tersedia di perangkat ini.",
                    "!size" => "Ukuran materi paling besar 32 MB.",
                    _ => "Berkas belum bisa dibaca. Pilih berkas yang sudah tersimpan di headset."
                };
                return;
            }
            pickedDocument = result;
            ImportDeck(result);
            if (!ImportingDeck)
                DeletePickedDocument();
        }

        internal void ImportDeck(string source)
        {
            if (Session.Phase != PracticePhase.Setup || ImportingDeck || !CanImportDeck)
                return;
            try
            {
                var file = new FileInfo(source);
                string format = file.Extension.ToLowerInvariant();
                if (format is not (".pptx" or ".ppt" or ".pdf" or ".png" or ".jpg" or ".jpeg"))
                {
                    DeckImportNotice = "Pilih PPTX, PPT, PDF, PNG, atau JPEG.";
                    return;
                }
                if (!file.Exists || file.Length == 0 || file.Length > 32_000_000)
                {
                    DeckImportNotice = "Berkas harus bisa dibaca dan berukuran paling besar 32 MB.";
                    return;
                }
                Directory.CreateDirectory(Deck.Folder);
                pendingDeck = Path.Combine(Deck.Folder, $"import-{Guid.NewGuid():N}.tmp");
                StartCoroutine(ConvertDeck(file.FullName, format));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                DeckImportNotice = "Berkas belum bisa dibuka. Coba salin ke folder lokal.";
            }
        }

        IEnumerator ConvertDeck(string source, string format)
        {
            DeckImportNotice = "Mengimpor slide dan membaca teks…";
            UnityWebRequest request;
            UnityWebRequestAsyncOperation conversion;
            try
            {
                request = new UnityWebRequest(settings.endpoint + "/import-deck", "POST");
                deckRequest = request;
                request.uploadHandler = new UploadHandlerFile(source);
                request.downloadHandler = new DownloadHandlerFile(pendingDeck) { removeFileOnAbort = true };
                request.timeout = 145;
                request.redirectLimit = 0;
                request.SetRequestHeader("Content-Type", "application/octet-stream");
                request.SetRequestHeader("X-NARA-Deck-Format", format);
                if (NaraQuestBridge.IsStandalone)
                    request.SetRequestHeader("X-NARA-Deck-Title", Uri.EscapeDataString(Path.GetFileName(source)));
                request.SetRequestHeader("Authorization", "Bearer " + settings.accessToken);
                conversion = request.SendWebRequest();
            }
            catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException)
            {
                DeckImportNotice = "Berkas belum bisa dibuka atau relay lokal belum berjalan.";
                CancelDeckImport();
                yield break;
            }
            yield return conversion;
            bool converted = request.result == UnityWebRequest.Result.Success;
            long status = request.responseCode;
            request.Dispose();
            deckRequest = null;
            try
            {
                if (converted)
                {
                    Deck.InstallArchive(pendingDeck);
                    DeckImportNotice = $"Materi siap · {Path.GetFileName(source)}";
                    ReloadDeck();
                }
                else
                {
                    DeckImportNotice = status switch
                    {
                        413 => "Batas impor 32 MB dan 60 slide. Coba pecah materinya.",
                        422 => "Materi tidak bisa dibaca. Jika PowerPoint gagal, ekspor ke PDF.",
                        501 => "Komponen impor belum tersedia. Cek panduan NARA.",
                        503 => "Impor sebelumnya masih berjalan. Coba lagi sebentar.",
                        504 => "Impor terlalu lama. Coba berkas dengan slide lebih sedikit.",
                        _ => NaraQuestBridge.IsStandalone ? "Impor lokal belum siap. Coba buka ulang NARA." :
                            "Relay lokal belum siap. Buka NARA lewat NARA Live.cmd."
                    };
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
            {
                DeckImportNotice = "Materi baru belum bisa disimpan. Materi sebelumnya tetap tersedia.";
            }
            finally
            {
                DeletePendingDeck();
                DeletePickedDocument();
            }
        }

        public void UseExampleDeck()
        {
            if (Session.Phase != PracticePhase.Setup || ImportingDeck)
                return;
            try
            {
                Deck.DiscardArchive();
                DeckImportNotice = "";
                ReloadDeck();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                DeckImportNotice = "Materi tersimpan belum bisa ditutup.";
            }
        }

        void CancelDeckImport()
        {
            deckRequest?.Abort();
            deckRequest?.Dispose();
            deckRequest = null;
            DeletePendingDeck();
            DeletePickedDocument();
        }

        void DeletePickedDocument()
        {
            NaraQuestBridge.DeleteSelection(pickedDocument);
            pickedDocument = null;
        }

        void DeletePendingDeck()
        {
            if (string.IsNullOrEmpty(pendingDeck))
                return;
            try { File.Delete(pendingDeck); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            pendingDeck = null;
        }
    }
}
