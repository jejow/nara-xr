# Handoff proyek NARA

Snapshot sesi kerja **19 November 2026, WIB**. Dokumen ini menyatukan konteks yang diperlukan untuk meneruskan implementasi di chat AI lain. Jalur file di bawah relatif terhadap root repo.

Pemilih karakter kini mencakup 30 avatar dan audio Indonesia–Inggris per karakter. Laras dan Satria tampil sebagai pilihan utama. Panduan pemilih, aset suara, dan konfigurasi live yang berlaku ada di [Suara karakter NARA](docs/SUARA-KARAKTER.md). Bagian berikut mencatat konteks implementasi sebelum pembaruan ini.

## Instruksi singkat untuk chat penerus

> Lanjutkan proyek NARA dari repo `https://github.com/jejow/nara-xr`. Baca `README.md` dan `HANDOFF.md`, buka scene `NARA/Assets/FinalProject/Scenes/NaraSeminar.unity`, dan cek versi terbaru sebelum mengubahnya. Pertahankan alur presentasi tiga menit, pemberian giliran saat audiens angkat tangan, pilihan avatar per kursi, dan dukungan laptop serta XR. Ikuti batas pengujian yang tercatat di sini saat menjelaskan hasil.

## Tujuan dan keputusan pengguna

NARA melatih mahasiswa mempresentasikan usulan di depan panel. Pengguna menyiapkan topik, ringkasan, dan slide. Pada akhir presentasi, audiens mengangkat tangan dan menunggu izin. Jawaban dibuka setelah pertanyaan selesai. Mode 3D menambahkan latihan memperhatikan audiens dan monitor podium, sedangkan mode layar laptop menjalankan prosedur latihan yang sama dengan tampilan slide penuh.

Nama yang dipakai dalam build dan dokumentasi saat ini adalah **NARA**. Pengguna ingin kelas universitas yang meyakinkan, memakai meja serta kursi yang dibuat di Blender, dengan karakter dewasa yang beragam dan gerakan tanya jawab yang natural. Tulisan terapung **NARA · AI** pernah dihapus atas permintaan pengguna. Identitas penanya tampil pada cue Q&A.

Tampilan persiapan yang memenuhi layar mendapat masukan bahwa UI terasa terlalu padat. Versi yang dipertahankan sekarang menempatkan panel persiapan gelap beraksen ungu di kiri, dengan ruang terlihat sejak awal. Ringkasan dan pengelolaan slide dibuka lewat **Materi latihan**. Setelan bahasa, suara, dan metrik dibuka lewat **Pengaturan**. Saat latihan, kontrol dipadatkan pada bilah bawah. Headset memakai panel world-space dengan teks lebih besar dan label tombol kontroler. [Acuan Keynote](https://support.apple.com/guide/keynote/play-a-presentation-on-a-separate-display-tana4da2681/mac) · [Acuan panel visionOS](https://developer.apple.com/design/human-interface-guidelines/ornaments/)

## Ringkasan pekerjaan sesi

1. Pekerjaan awal mengeksplorasi foto, video, urutan gambar 360°, KIRI Engine, serta jalur mesh dan splat. Urutan sintetis hanya menyediakan sudut baru dari model perkiraan yang sama. Data pengukuran fisik tidak tercatat pada model tersebut. Untuk final project, pengguna memilih alur berbasis aset Blender dan ruang yang bisa direproduksi.
2. Kelas universitas tevy diadaptasi di Blender. Tim memasok model meja dan kursi dengan motif pelapis kursi dari gambar generatif. Tiga set membentuk panel depan dan tujuh set disusun di belakang dengan lorong tengah. Versi publik ini memakai podium Blender yang dibuat dari geometri baru dengan dudukan dan monitor yang sama posisinya. Podium generatif Hunyuan digunakan pada iterasi kelas sebelumnya dengan ketentuan distribusi tersendiri. [Sumber aset](docs/ASSETS.md) · [Lisensi Hunyuan3D 2.0](https://github.com/Tencent-Hunyuan/Hunyuan3D-2/blob/main/LICENSE)
3. Dua puluh empat model dewasa Rocketbox dipersiapkan menjadi prefab skinned dan katalog potret. Tiga tempat duduk aktif dapat diganti secara independen. Memilih avatar yang sedang menempati kursi lain menukar kedua posisi. Pilihan lokal disimpan lewat PlayerPrefs.
4. Pose dasar, kedipan, napas, arah pandang, anggukan, gestur lengan, dan gerakan mulut dari intensitas audio ditambahkan. Pose angkat tangan memakai tulang bahu, siku, dan pergelangan. Setelah diberi giliran, penanya menurunkan tangan, mengangguk, lalu berbicara. Suara diarahkan ke kursi penanya yang sedang aktif.
5. Layar slide dan monitor podium disinkronkan. Audit menemukan halangan proyeksi dari kursi tengah. Layar diatur menjadi 2,78 m dengan bagian bawah sekitar 1,27 m. Pemeriksaan ulang menguji enam titik dari tiga kursi dan dua tinggi mata.
6. UI laptop dan headset dipisah. Iterasi terakhir mengutamakan ruangan, memindahkan detail persiapan ke panel terpisah, dan menempatkan panel Q&A headset di bawah wajah penanya dari sudut awal. Versi publik berisi Unity project yang telah disiapkan, sumber Blender portabel, relay, aset runtime, dan panduan ini.

## Peta kode dan aset

| Bagian | Lokasi |
|---|---|
| Scene utama, posisi pembicara, proyektor, panel, dan pengaturan render | [NaraSeminar.unity](NARA/Assets/FinalProject/Scenes/NaraSeminar.unity) |
| Assembly runtime dan alur sesi | [Scripts](NARA/Assets/FinalProject/Scripts) |
| State dan hitungan sesi | [PracticeSession.cs](NARA/Assets/FinalProject/Scripts/PracticeSession.cs) |
| Audio, slide, alur API, mode offline, dan input jawaban | [NaraPractice.cs](NARA/Assets/FinalProject/Scripts/NaraPractice.cs) |
| Komponen antarmuka bersama dan fokus keyboard | [PracticeView.cs](NARA/Assets/FinalProject/Scripts/PracticeView.cs) |
| Persiapan, panel materi, bilah presentasi, dan Q&A desktop | [PracticeView.Desktop.cs](NARA/Assets/FinalProject/Scripts/PracticeView.Desktop.cs) |
| Panel world-space dan petunjuk kontroler | [PracticeView.Headset.cs](NARA/Assets/FinalProject/Scripts/PracticeView.Headset.cs) |
| Pemilih 24 avatar, posisi kursi, dan animasi | [PracticeAvatarPicker.cs](NARA/Assets/FinalProject/Scripts/PracticeAvatarPicker.cs), [NaraAudience.cs](NARA/Assets/FinalProject/Scripts/NaraAudience.cs), [NaraAvatarPuppet.cs](NARA/Assets/FinalProject/Scripts/NaraAvatarPuppet.cs) |
| Keyboard, pandangan desktop, input headset | [PracticeRig.cs](NARA/Assets/FinalProject/Scripts/PracticeRig.cs) |
| Generator scene, prefab, dan build | [Editor](NARA/Assets/FinalProject/Editor) |
| Contoh slide, WAV offline, potret, dan konfigurasi katalog | [Resources/Nara](NARA/Assets/FinalProject/Resources/Nara) |
| FBX lingkungan, atlas warna, dan tekstur kursi | [Environment](NARA/Assets/FinalProject/Environment) |
| Mesh karakter dan asal aset | [Rocketbox](NARA/Assets/FinalProject/Characters/Rocketbox) |
| Model edit, furnitur, dan GLB ruang dasar | [assets/blender](assets/blender) |
| Relay suara, pembuatan offline content, dan persiapan aset | [tools](tools) |

Alur utama `Setup → Presentation → Waiting → FloorRequest → Asking → Question → Review`. Pertanyaan utama kedua datang dari salah satu kursi samping. Mode live bisa meminta satu follow-up dari penanya yang sama berdasarkan jawaban pengguna. Teks kutipan follow-up diperiksa terhadap jawaban. Kontrol jawab menunggu fase `Question`; putar ulang mengembalikan fase ke `Asking` tanpa menambah jumlah pertanyaan.

## Jalankan dan periksa

1. Jalankan `git clone https://github.com/jejow/nara-xr.git`. Dari Unity Hub, buka **folder `nara-xr/NARA/`** dengan Unity **6000.3.28f1**. Proyek memakai Built-in Render Pipeline, uGUI, dan OpenXR. Unity Package Manager memulihkan dependensi pada impor awal.
2. Buka `Assets/FinalProject/Scenes/NaraSeminar.unity` dan jalankan Play Mode. Materi contoh langsung tersedia. Tekan **Mulai latihan** dan **Masuk Q&A** untuk memeriksa alur lebih cepat. Setelah tangan terangkat, pilih **Persilakan bertanya**, lalu tunggu **Giliranmu menjawab**.
3. Menu **NARA → Final Project → Create practice scene** membangun ulang scene dan prefab dari aset yang sudah ada. Menu **Build Windows** memakai scene tersebut. Output berada di `BuildFinalProject/Windows` relatif terhadap proyek Unity. Buka `NARA.exe` bersama file player lain di folder itu.
4. Untuk Quest, instal Android Build Support, SDK/NDK, dan OpenJDK pada Unity versi yang sama. Aktifkan platform Android dan gunakan **Build Quest APK**. Jalur builder mengatur ARM64, IL2CPP, Vulkan, OpenXR, Oculus Touch, dan target Quest 2/3.

| Kontrol | Aksi |
|---|---|
| Tab / Shift+Tab, Enter | Fokus dan aktivasi kontrol laptop |
| Panah kiri / kanan | Ganti slide atau pilihan avatar saat pemilih terbuka |
| C, Esc | Buka pemilih avatar. Esc menutup panel persiapan atau mengakhiri sesi aktif |
| Klik kanan dan seret, Home, V | Arah pandang laptop, kembali ke arah awal, atau lirik monitor |
| P, Spasi | Jeda presentasi, atau tahan Spasi untuk bicara saat mode live |
| Quest A | Mulai, masuk Q&A, beri giliran, konfirmasi jawaban offline, atau mulai latihan baru |
| Quest B / Y | Slide berikut atau sebelumnya. Saat persiapan, pilih mode suara atau bahasa |
| Quest grip kiri / kanan | Pilih avatar, jeda, atau putar ulang. Grip kanan untuk bicara saat live |
| Quest pelatuk kiri, tahan X | Catat waktu atau lewati pertanyaan. Tahan X untuk mengakhiri sesi |

Di pemilih avatar Quest, A berpindah kursi, B dan Y mengganti model, dan grip kiri kembali. Di desktop, **Buka folder slide** dari panel materi membuka lokasi `Application.persistentDataPath/Practice`. Salin `practice.example.json` ke `practice.json`, masukkan satu hingga tiga PNG/JPEG, isi daftar file, lalu pilih **Muat ulang**. Isi `language` dengan `id` atau `en`.

## Relay, data, dan uji otomatis

Mode offline berjalan dengan aset lokal. Untuk memeriksa jalur HTTP secara lokal, jalankan dari root repo:

```powershell
python tools/nara_voice_server.py --mock
```

NARA di Windows menggunakan endpoint default `http://127.0.0.1:8765`. Aktifkan **Suara live** di Pengaturan agar klien menghubungi relay. Balasan mock diberi label simulasi. Untuk provider live, relay memakai `OPENAI_API_KEY` di lingkungan proses. `NARA_ENDPOINT` dan `NARA_ACCESS_TOKEN` dapat diisi pada lingkungan pengujian lokal. Konfigurasi perangkat dapat disetel di `backend.json` pada folder Practice. Endpoint perangkat memakai HTTPS, dan kredensial provider berada pada server.

Mode live membutuhkan persetujuan pada Pengaturan. Audio yang direkam, topik, dan jawaban masuk ke relay untuk pemrosesan. Opsi catat waktu menyimpan metrik sesi lokal di folder Evaluation. Batas client untuk satu respons adalah delapan detik. Ketika koneksi gagal, NARA beralih ke pertanyaan tersimpan.

Contoh uji otomatis dengan Unity CLI dari root repo:

```powershell
$unity = 'C:/Program Files/Unity 6000.3.28f1/Editor/Unity.exe'
$project = (Resolve-Path ./NARA).Path
& $unity -batchmode -nographics -projectPath "$project" -executeMethod Nara.Practice.Editor.NaraPracticeValidation.Run -naraSmoke -logFile "$project/playmode.log"
```

Setelah relay `--mock` berjalan, tambahkan `-naraMock` pada perintah itu untuk menguji respons HTTP, follow-up, dan putus koneksi. Cari `NARA_SMOKE PASS` dalam log. Jalankan sesi Unity CLI dengan editor proyek tertutup agar lock proyek tidak saling bertabrakan. Metode `Nara.Practice.Editor.NaraPracticeBuild.RebuildWindows` dapat dipakai dengan `-batchmode -nographics -quit` untuk membangun scene dan player.

## Penyuntingan Blender dan aset

Model ruang lengkap dan teksturnya dipaketkan dalam [NARA-Kelas-Presentasi.blend](assets/blender/NARA-Kelas-Presentasi.blend). Model [Meja-Kursi.blend](assets/blender/Meja-Kursi.blend) menyimpan meja dan dua kursi pada skala desain. Dimensi meja 0,76 m dan dudukan 0,468 m berasal dari geometri model. Ukuran lapangan masih perlu diukur. Marker `NaraPresenter`, `NaraCoachSeat`, marker kursi samping, `NaraScreen`, `NaraScreenEdge`, dan marker monitor dibaca oleh generator Unity. Jaga nama marker dan material saat membuat ekspor.

Jika perlu membuat tata ruang dari GLB yang sudah didekode dan furnitur tim, jalankan Blender dengan [tools/nara_prepare_classroom.py](tools/nara_prepare_classroom.py). Argumen posisi adalah GLB dasar, FBX tujuan, BLEND tujuan, PNG pratinjau, dan satu gambar slide. Argumen `--furniture` memilih sumber BLEND. `--upholstery` memilih PNG tekstur kursi tujuan. Opsi `--skip-render` melewati render pratinjau. Sumber GLB yang siap diimpor tersedia di `assets/blender/tevy-classroom-unpacked.glb`.

[Manifest Rocketbox](NARA/Assets/FinalProject/Characters/Rocketbox/SourceAssets.json) mencatat revisi dan blob sumber yang dipakai. [nara_prepare_avatars.py](tools/nara_prepare_avatars.py) memproses sumber mentah jika diperlukan. [nara_make_content.py](tools/nara_make_content.py) menghasilkan slide dengan Pillow dan WAV dengan eSpeak NG. Build biasa menggunakan aset hasil yang ada di repo.

## Bukti dan status pekerjaan

- Build Windows x64 berhasil dari salinan publik. Validator menemukan 124.946 triangle di lingkungan, 10 set furnitur, tiga kursi panel, dan monitor podium. Repo memuat sumber untuk membuat player lewat menu build.
- Unity Play Mode dari salinan publik menghasilkan `NARA_SMOKE PASS`. Uji ini memeriksa 24 avatar, pemilih avatar desktop/XR, navigasi keyboard, panel materi yang menyimpan draf, sinkronisasi monitor dan layar, timer, izin bicara, dan putar ulang.
- Pengujian relay simulasi memeriksa dua pertanyaan, follow-up yang merujuk pada jawaban, serta fallback saat koneksi putus.
- Audit Blender untuk salinan publik memeriksa 10 set furnitur dan 36 garis pandang ke layar. Semua garis pandang bebas halangan pada titik yang diuji. Lorong tengah versi model sekitar 1,281 m. Tekstur terpaket berhasil dibaca dari kedua file Blender.
- Peluncuran player Windows, provider suara live, mikrofon fisik, dan headset fisik masih perlu diperiksa. Modul build Android belum terpasang di mesin pengujian.
- Render README adalah pratinjau dari tata letak Unity dan model ruang Blender. Ini dapat digunakan sebagai konteks visual, bersama aturan penempatan panel dan ruang di atas.

## Konteks pekerjaan akademik dan AI

Pengerjaan GA3 awal mencakup capture plan, prediksi, audit cacat dari beberapa sudut, komparasi mesh/splat, dan provenance aset. Folder ini membawa implementasi final project NARA. Bukti pengukuran langsung, evaluasi peserta, dan persetujuan tim perlu berasal dari pekerjaan yang benar-benar dilakukan oleh tim.

Pengguna memberi izin asisten untuk membantu penyusunan kata, gambar, Blender, dan skrip, dengan penggunaan AI dicatat. Kontribusi sesi meliputi C# dan Python, desain antarmuka, pencarian serta penyesuaian aset, pengolahan rig, render pratinjau, pembuatan audio sintetis, dan pengujian build. Untuk laporan, jelaskan kontribusi tersebut dan bedakan hasil simulasi dari pengujian manusia atau perangkat.
