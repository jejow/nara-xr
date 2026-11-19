# Suara karakter NARA

**Laras** dan **Satria** adalah pilihan utama perempuan dan laki-laki. Menu persiapan juga menerima karakter lain untuk ketiga kursi. Pilihan lama tetap terbaca lewat ID avatar. Karakter utama menyapa, membuka sesi tanya jawab, memberi pemberitahuan teknis, dan menyampaikan saran akhir. Kursi samping mendapat pertanyaan kedua dan dapat mengajukan pertanyaan lanjutan saat mode live. [Katalog karakter](../NARA/Assets/FinalProject/Resources/Nara/AvatarCatalog.json) · [Naskah Indonesia dan Inggris](../NARA/Assets/FinalProject/Resources/Nara/OfflineScript.json)

## Memilih dan mendengar suara

- Di laptop, buka **Karakter** saat persiapan. Pilih peran, gunakan filter **Perempuan** atau **Laki-laki**, lalu pilih potret. Klik **Dengar suara** untuk mendengar sapaan karakter dalam bahasa sesi.
- Di headset, buka **Karakter** dengan **Grip kiri**. **A** berpindah peran, **X** mengganti filter, **Y/B** mengganti karakter, dan **Grip kanan** memutar sapaan.

Pemilih memakai potret, nama, dan tanda centang untuk menunjukkan pilihan aktif. Kontras fokus, urutan navigasi, dan label kontroler mengikuti pola antarmuka game dan XR. [Navigasi antarmuka Xbox](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/112) · [Kontroler Meta Horizon](https://developers.meta.com/vr/design/controllers/) · [Aksesibilitas Meta Horizon](https://developers.meta.com/vr/design/accessibility/)

## Aset suara

Bank audio offline berisi tujuh baris dalam dua bahasa untuk setiap karakter. Sepuluh profil memakai bawaan Supertonic 3. Dua puluh profil lain memakai perpaduan tetap dari dua vektor gaya perempuan atau dua vektor gaya laki-laki. ID profil menentukan warna suara, tempo, dan WAV saat karakter berpindah kursi. Laras memakai F5 dan Satria memakai M2. Audio di Unity berupa dialog sintetis dari naskah proyek. Demo manusia di katalog audisi tersedia sebagai bahan casting. [Repositori model yang diarsipkan](https://github.com/supertone-oss-archive/supertonic) · [Model dan lisensi OpenRAIL-M](https://huggingface.co/supertone-oss-archive/supertonic-3) · [Katalog audisi](AUDISI-SUARA.md)

## Memperbarui naskah dan menjalankan relay

Unity memuat WAV yang tersedia dalam proyek saat sesi offline. Untuk membuat ulang audio setelah mengubah naskah, instal `supertonic==1.3.1` dan unduh model arsip sekali ke folder lokal. Untuk format WAV dan penamaan klip, gunakan generator NARA. [Sumber model](https://huggingface.co/supertone-oss-archive/supertonic-3/tree/aafc6e32416a594460b32413efc49d7fe4ce6d46) · [Generator](../tools/nara_cast_voices.py)

```shell
hf download supertone-oss-archive/supertonic-3 --revision aafc6e32416a594460b32413efc49d7fe4ce6d46 --local-dir /path/to/supertonic-3
python tools/nara_cast_voices.py /path/to/supertonic-3
```

Untuk **mode live**, jalankan relay dengan `OPENAI_API_KEY` di lingkungan proses dan `--voice-model` menunjuk ke folder model yang sama. Relay memakai ID penanya untuk sintesis lokal. Jika perangkat melewati batas waktu sesi atau relay gagal, Unity memberi pemberitahuan dan beralih ke dialog tersimpan dari karakter terkait. `--mock` menggunakan WAV proyek dan pertanyaan lanjutan berupa teks untuk memeriksa alur HTTP tanpa kunci API. [Relay suara](../tools/nara_voice_server.py)

```shell
python tools/nara_voice_server.py --voice-model /path/to/supertonic-3
python tools/nara_voice_server.py --mock
```
