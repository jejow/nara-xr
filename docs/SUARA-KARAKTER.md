# Suara karakter NARA

NARA mengisi 24 kursi secara acak dari 30 profil. Satu NPC menjadi pemandu untuk sapaan, pemberitahuan teknis, dan saran akhir. Dua kursi berbeda dipilih secara acak untuk pertanyaan utama. Pertanyaan lanjutan mode live memakai NPC yang mengajukan pertanyaan kedua. [Katalog karakter](../NARA/Assets/FinalProject/Resources/Nara/AvatarCatalog.json) · [Naskah Indonesia dan Inggris](../NARA/Assets/FinalProject/Resources/Nara/OfflineScript.json)

## Pengaturan suara

Di laptop, buka **Pengaturan** untuk memilih bahasa dan suara offline atau live. Di headset, tekan **Y** untuk mengganti bahasa dan **B** untuk mengganti mode suara saat persiapan. Setelan disimpan otomatis di perangkat.

## Aset suara

Bank audio offline berisi tujuh baris dalam dua bahasa untuk setiap karakter. Sepuluh profil memakai bawaan Supertonic 3. Dua puluh profil lain memakai perpaduan tetap dari dua vektor gaya perempuan atau dua vektor gaya laki-laki. ID profil menentukan warna suara, tempo, dan WAV saat karakter berpindah kursi. Laras memakai F5 dan Satria memakai M2. Audio di Unity berupa dialog sintetis dari naskah proyek. Demo manusia di katalog audisi tersedia sebagai bahan casting. [Repositori model yang diarsipkan](https://github.com/supertone-oss-archive/supertonic) · [Model dan lisensi OpenRAIL-M](https://huggingface.co/supertone-oss-archive/supertonic-3) · [Katalog audisi](AUDISI-SUARA.md)

## Memperbarui naskah dan menjalankan relay

Unity memuat WAV yang tersedia dalam proyek saat sesi offline. Untuk membuat ulang audio setelah mengubah naskah, instal `supertonic==1.3.1` dan unduh model arsip sekali ke folder lokal. Untuk format WAV dan penamaan klip, gunakan generator NARA. [Sumber model](https://huggingface.co/supertone-oss-archive/supertonic-3/tree/aafc6e32416a594460b32413efc49d7fe4ce6d46) · [Generator](../tools/nara_cast_voices.py)

```shell
hf download supertone-oss-archive/supertonic-3 --revision aafc6e32416a594460b32413efc49d7fe4ce6d46 --local-dir /path/to/supertonic-3
python tools/nara_cast_voices.py /path/to/supertonic-3
```

## Pertanyaan live di Windows

Buka [NARA Live.cmd](../NARA%20Live.cmd) dari root repo setelah membangun player. Di **Materi latihan → Impor berkas**, pilih PPTX, PPT, PDF, PNG, atau JPEG. Batasnya 32 MB dan 60 slide. PowerPoint dirender menjadi slide statis dan memerlukan Microsoft PowerPoint yang bisa dibuka di Windows. PDF dan gambar diproses oleh relay lokal. Hasil impor tersedia saat membuka NARA lagi. **Tutup berkas** menutup materi di NARA.

Aktifkan **Suara live** di **Pengaturan**. Saat presentasi, pilih **Mulai mic** untuk merekam penjelasan atau **Tulis penjelasan** untuk mengetik. Setelah **Masuk Q&A**, dua NPC dari kursi berbeda meminta giliran. Klik **Persilakan bertanya**, lalu jawab lewat **Mulai mic → Kirim rekaman** atau ketik dan pilih **Kirim**. Penanya kedua bisa mengajukan satu pertanyaan lanjutan berdasarkan jawabanmu. Pemandu memberi saran penutup.

Relay memakai `gpt-6.1-sol` dari AISUBS untuk menyusun dialog. Topik, ringkasan, teks slide, halaman yang dibuka, penjelasan, dan jawaban dipakai sebagai konteks. Tesseract membantu membaca halaman gambar. Tambahkan penjelasan untuk grafik atau gambar yang teksnya sulit dibaca. [Dokumentasi AISUBS](https://aisubs.id/apisubs/docs) · [Relay](../tools/nara_voice_server.py)

Faster Whisper mulai mentranskripsi penjelasan secara lokal selama presentasi. Saat pertanyaan siap, klik **Persilakan bertanya** untuk langsung membaca dan menjawab. Supertonic menyiapkan suara karakter di latar belakang. Setelah audio siap, kamu juga bisa memutar ulang pertanyaan. Jika relay atau provider gagal memberi dialog valid, NARA beralih ke pertanyaan tersimpan.

Pada komputer baru, jalankan perintah berikut di PowerShell dengan Python 3.12. Model diunduh ke folder pengguna dan dipakai kembali setiap kali relay dimulai.

```powershell
$relayFolder = Join-Path $env:USERPROFILE '.nara\relay'
python -m venv "$relayFolder\venv"
& "$relayFolder\venv\Scripts\python.exe" -m pip install supertonic==1.3.1 faster-whisper==1.2.1 pypdfium2==5.15.2 Pillow==12.3.0 pywin32==312 tesserocr==2.13.0
& "$relayFolder\venv\Scripts\hf.exe" download supertone-oss-archive/supertonic-3 `
    --revision aafc6e32416a594460b32413efc49d7fe4ce6d46 `
    --include 'onnx/*' 'voice_styles/*' '*.json' --local-dir "$relayFolder\models\supertonic-3"
& "$relayFolder\venv\Scripts\hf.exe" download Systran/faster-whisper-tiny `
    --revision d90ca5fe260221311c53c58e660288d3deb8d356 `
    --include config.json model.bin tokenizer.json vocabulary.txt --local-dir "$relayFolder\models\whisper-tiny"
$ocrFolder = Join-Path $relayFolder 'models\tessdata'
New-Item -ItemType Directory -Path $ocrFolder -Force | Out-Null
foreach ($code in 'ind', 'eng') {
    Invoke-WebRequest "https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/4.1.0/$code.traineddata" `
        -OutFile (Join-Path $ocrFolder "$code.traineddata")
}
```

Peluncur meminta kunci AISUBS secara tersamar saat pertama dipakai. Kunci simpanan dienkripsi dengan Windows DPAPI untuk akun yang menjalankan relay. Unity menerima token untuk mengakses relay lokal. Periksa status startup di `%USERPROFILE%\.nara\relay`. Pada port 8765, log-nya `relay.log` dan `relay-error.log`. Jika `runtime.json` menentukan port lain, nama kedua log memuat nomor port.

Setelan standar memakai `gpt-6.1-sol` dan mencoba ulang model yang sama sekali jika AISUBS mengembalikan HTTP 502, 503, atau 504 dan waktu masih cukup. Kredensial tiap pengguna menentukan model yang tersedia. [Model dan tarif AISUBS](https://aisubs.id/apisubs?tab=models)

Untuk pengujian, relay simulasi memakai WAV proyek dan pertanyaan lanjutan berbentuk teks. Konfigurasi relay OpenAI yang sudah ada masih dapat memilih provider eksplisit dengan kredensialnya sendiri.

```powershell
python tools/nara_voice_server.py --mock
python tools/nara_voice_server.py --provider openai --voice-model "$relayFolder\models\supertonic-3"
```
