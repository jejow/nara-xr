# Handoff proyek NARA

Snapshot sesi kerja **20 November 2026, WIB**. Dokumen ini menyatukan konteks yang diperlukan untuk meneruskan implementasi di chat AI lain. Jalur file di bawah relatif terhadap root repo.

Salinan kerja ini berangkat dari commit `7eb3134`. Ruang memakai 24 kursi aktif yang diisi dari 30 profil bersuara Indonesia dan Inggris. Penghuni dan penanya diacak. Menu pengaturan disimpan lokal. [Suara karakter NARA](docs/SUARA-KARAKTER.md)

## Instruksi singkat untuk chat penerus

> Lanjutkan dari salinan kerja NARA yang disertakan dengan dokumen ini. Baca `README.md` dan buka `NARA/Assets/FinalProject/Scenes/NaraSeminar.unity`. Menu persiapan menyediakan kelas universitas dan IF 110. Pertahankan presentasi tiga menit, empat baris berisi 24 NPC, dua penanya acak, izin bertanya, dan kontrol laptop serta headset.

## Tujuan dan keputusan pengguna

NARA melatih mahasiswa presentasi di depan kelas. Pengguna menyiapkan topik, ringkasan, dan slide. Pada akhir presentasi, NPC yang terpilih mengangkat tangan dan menunggu izin. Dalam mode live, kolom jawaban dibuka saat teks pertanyaan tersedia setelah penanya dipersilakan. Mode 3D menampilkan audiens dan monitor podium. Mode layar laptop memakai tampilan slide penuh.

Nama yang dipakai dalam build dan dokumentasi saat ini adalah **NARA**. Pengguna ingin kelas universitas yang meyakinkan, memakai meja serta kursi yang dibuat di Blender, dengan karakter dewasa yang beragam dan gerakan tanya jawab yang natural. Tulisan terapung **NARA · AI** pernah dihapus atas permintaan pengguna. Identitas penanya tampil pada cue Q&A.

Tampilan persiapan yang memenuhi layar mendapat masukan bahwa UI terasa terlalu padat. Versi yang dipertahankan sekarang menempatkan panel persiapan gelap beraksen ungu di kiri, dengan ruang terlihat sejak awal. Ringkasan dan pengelolaan slide dibuka lewat **Materi latihan**. Setelan bahasa, suara, dan metrik dibuka lewat **Pengaturan**. Saat latihan, kontrol dipadatkan pada bilah bawah. Headset memakai panel world-space dengan teks lebih besar dan label tombol kontroler. [Acuan Keynote](https://support.apple.com/guide/keynote/play-a-presentation-on-a-separate-display-tana4da2681/mac) · [Acuan panel visionOS](https://developer.apple.com/design/human-interface-guidelines/ornaments/)

## Ringkasan pekerjaan sesi

1. Pekerjaan awal mengeksplorasi foto, video, urutan gambar 360°, KIRI Engine, serta jalur mesh dan splat. Urutan sintetis hanya menyediakan sudut baru dari model perkiraan yang sama. Data pengukuran fisik tidak tercatat pada model tersebut. Untuk final project, pengguna memilih alur berbasis aset Blender dan ruang yang bisa direproduksi.
2. Kelas universitas memakai meja trapesium, kursi kain tim, dan podium oak dengan monitor serta mikrofon. IF 110 memakai adaptasi ruang referensi IF 101 dengan 24 kursi kayu dan layar PPT di depan. Kedua ruang memakai material PBR dan refleksi interior yang dipanggang. [Sumber aset](docs/ASSETS.md)
3. Dua puluh empat NPC diambil tanpa duplikasi dari 30 profil Rocketbox. Kursi diacak saat ruangan dibuka dan saat kembali ke persiapan sesudah latihan. Pilihan kelas, mode latihan, bahasa, suara, dan pencatatan waktu tersimpan di PlayerPrefs. Topik dan ringkasan dipertahankan saat pengguna mengganti kelas.
4. Dari posisi tangan di paha, tiap NPC bergantian menyimak, menyilangkan lengan, merapikan manset, atau bertumpu ke satu sisi. Waktu dan urutan gerak berbeda antarorang. Sebagian menoleh ke layar saat slide berganti. Setelah mendapat giliran, penanya menurunkan tangan dan memakai lima variasi gestur. Mulut mengikuti audio. Nama dan ucapan mengikuti pembicara yang terlihat, dengan teks bersambung untuk kalimat panjang.
5. Slide tampil di layar fisik pada kedua kelas serta monitor podium di kelas universitas. IF 110 menyediakan **Lihat PPT** di desktop. Pemeriksaan Blender menguji enam titik layar dari dua tinggi mata pada 24 kursi.
6. UI laptop dan headset dipisah. Iterasi terakhir mengutamakan ruangan, memindahkan detail persiapan ke panel terpisah, dan menempatkan panel Q&A headset di bawah wajah penanya dari sudut awal. Versi publik berisi Unity project yang telah disiapkan, sumber Blender portabel, relay, aset runtime, dan panduan ini.

## Peta kode dan aset

| Bagian | Lokasi |
|---|---|
| Scene kelas, posisi pembicara, proyektor, dan pengaturan render | [Kelas universitas](NARA/Assets/FinalProject/Scenes/NaraSeminar.unity), [IF 110](NARA/Assets/FinalProject/Scenes/NaraIF101.unity) |
| Assembly runtime dan alur sesi | [Scripts](NARA/Assets/FinalProject/Scripts) |
| State dan hitungan sesi | [PracticeSession.cs](NARA/Assets/FinalProject/Scripts/PracticeSession.cs) |
| Audio, slide, alur API, mode offline, dan input jawaban | [NaraPractice.cs](NARA/Assets/FinalProject/Scripts/NaraPractice.cs) |
| Komponen antarmuka bersama dan fokus keyboard | [PracticeView.cs](NARA/Assets/FinalProject/Scripts/PracticeView.cs) |
| Persiapan, panel materi, bilah presentasi, dan Q&A desktop | [PracticeView.Desktop.cs](NARA/Assets/FinalProject/Scripts/PracticeView.Desktop.cs) |
| Panel world-space dan petunjuk kontroler | [PracticeView.Headset.cs](NARA/Assets/FinalProject/Scripts/PracticeView.Headset.cs) |
| Posisi balon dialog, halaman ucapan, dan pemeriksaan garis pandang | [PracticeView.Speech.cs](NARA/Assets/FinalProject/Scripts/PracticeView.Speech.cs) |
| Pengacakan kursi, perhatian ke slide atau penanya, dan gerakan tubuh | [NaraAudience.cs](NARA/Assets/FinalProject/Scripts/NaraAudience.cs), [NaraAvatarPuppet.cs](NARA/Assets/FinalProject/Scripts/NaraAvatarPuppet.cs) |
| Rekaman duduk, gestur per kalimat, dan arah jari | [Pemilihan gestur](NARA/Assets/FinalProject/Scripts/NaraAvatarPuppet.Motion.cs) · [Pemutar rekaman](NARA/Assets/FinalProject/Scripts/SeatedMotionBank.cs) |
| Angkat, tahan, dan turunkan tangan serta klip tambahan Overte dan HY-Motion | [Urutan izin bertanya](NARA/Assets/FinalProject/Scripts/NaraAvatarPuppet.Floor.cs) · [Pemutar klip tubuh atas](NARA/Assets/FinalProject/Scripts/OverteMotion.cs) |
| Penempatan siku dan tangan pada tubuh serta meja | [Pemecah IK](NARA/Assets/FinalProject/Scripts/NaraAvatarPuppet.Contacts.cs) · [Pengukuran karakter](NARA/Assets/FinalProject/Scripts/NaraAvatarPuppet.ContactFit.cs) · [Uji geometri](NARA/Assets/FinalProject/Editor/NaraSeatedMotionAudit.cs) |
| Keyboard, pandangan desktop, input headset | [PracticeRig.cs](NARA/Assets/FinalProject/Scripts/PracticeRig.cs) |
| Generator scene, prefab, dan build | [Editor](NARA/Assets/FinalProject/Editor) |
| Contoh slide, WAV offline, potret, dan konfigurasi katalog | [Resources/Nara](NARA/Assets/FinalProject/Resources/Nara) |
| FBX lingkungan, atlas warna, dan tekstur kursi | [Environment](NARA/Assets/FinalProject/Environment) |
| Mesh karakter dan asal aset | [Rocketbox](NARA/Assets/FinalProject/Characters/Rocketbox) |
| Model edit, furnitur, dan GLB ruang dasar | [assets/blender](assets/blender) |
| Relay suara, pembuatan offline content, dan persiapan aset | [tools](tools) |

Alur utama `Setup → Presentation → Waiting → FloorRequest → Asking → Question → Review`. Dua kursi berbeda diundi dari seluruh ruangan. Mode live bisa meminta satu follow-up dari penanya kedua berdasarkan jawaban pengguna. Putar ulang mengembalikan fase ke `Asking` tanpa menambah jumlah pertanyaan.

## Jalankan dan periksa

1. Dari Unity Hub, buka **folder `NARA/`** dari salinan kerja dengan Unity **6000.3.28f1**. Proyek memakai Built-in Render Pipeline, uGUI, dan OpenXR. Unity Package Manager memulihkan dependensi pada impor awal. [Repo versi dasar](https://github.com/jejow/nara-xr)
2. Buka `Assets/FinalProject/Scenes/NaraSeminar.unity` dan jalankan Play Mode. Materi contoh langsung tersedia. Tekan **Mulai latihan** dan **Masuk Q&A** untuk memeriksa alur lebih cepat. Setelah tangan terangkat, pilih **Persilakan bertanya**, lalu tunggu **Giliranmu menjawab**.
3. Menu **NARA → Final Project → Create practice scenes** membangun ulang kedua scene dari aset yang sudah ada. Menu **Build Windows** memasukkan keduanya ke player. Output berada di `BuildFinalProject/Windows` relatif terhadap proyek Unity. Buka `NARA.exe` bersama file player lain di folder itu.
4. Untuk Quest, instal Android Build Support, SDK/NDK, dan OpenJDK pada Unity versi yang sama. Aktifkan platform Android dan gunakan **Build Quest APK**. Jalur builder mengatur ARM64, IL2CPP, Vulkan, OpenXR, Oculus Touch, dan target Quest 2/3.

| Kontrol | Aksi |
|---|---|
| Tab / Shift+Tab, Enter | Fokus dan aktivasi kontrol laptop |
| Panah kiri / kanan | Ganti slide |
| Esc | Tutup panel persiapan atau akhiri sesi aktif |
| Klik kanan dan seret, Home, V | Arah pandang laptop, kembali ke arah awal, atau lihat PPT di IF 110. Tahan V untuk melihat monitor kelas universitas |
| P, Spasi | Jeda presentasi, atau tahan Spasi untuk bicara saat mode live |
| Quest A | Mulai, masuk Q&A, beri giliran, konfirmasi jawaban offline, atau mulai latihan baru |
| Quest B / Y | Slide berikut atau sebelumnya. Saat persiapan, pilih mode suara atau bahasa |
| Quest grip kiri / kanan | Grip kiri mengganti kelas saat persiapan, menjeda saat presentasi, atau memutar ulang audio. Grip kanan untuk bicara saat live |
| Quest pelatuk kiri, tahan X | Catat waktu atau lewati pertanyaan. Tahan X untuk mengakhiri sesi |

Di Windows, **Materi latihan → Impor berkas** menerima PPTX, PPT, PDF, PNG, dan JPEG lewat relay lokal. Pratinjau dan teks disimpan sebagai `Practice/imported-deck.zip`. Pemain lama dapat membuka **Folder slide**, menyalin `practice.example.json` ke `practice.json`, mendaftarkan satu hingga tiga PNG/JPEG, lalu memilih **Muat ulang**. Isi `language` dengan `id` atau `en` untuk bahasa awal.

## Relay, data, dan uji otomatis

Mode offline berjalan dengan aset lokal. Untuk memeriksa jalur HTTP secara lokal, jalankan dari root repo:

```powershell
python tools/nara_voice_server.py --mock
```

Di Windows, [NARA Live.cmd](NARA%20Live.cmd) menjalankan relay dan membuka player dengan token akses lokal. Port bawaan peluncur 8767 dapat diatur melalui `port` di `runtime.json`. Relay meneruskan dialog ke `gpt-6.1-sol` melalui AISUBS. Kunci AISUBS dan Cartesia disimpan dengan Windows DPAPI di folder pengguna. Pilih **Suara live** di Pengaturan untuk mengizinkan pemrosesan sesi. Balasan mock diberi label simulasi. [Panduan relay live](docs/SUARA-KARAKTER.md#pertanyaan-live-di-windows)

PowerPoint dan PDFium merender slide secara lokal. Tesseract membaca teks gambar jika diperlukan. Faster Whisper mentranskripsi per bagian selama presentasi. AISUBS menerima teks slide, topik, ringkasan, penjelasan tertulis atau transkrip, serta jawaban. Relay API 5 mengirim teks dialog lebih dulu dan audio PCM lewat `/speech-stream`. Unity mulai memutar setelah tersedia buffer singkat dan menyimpan audio lengkap untuk putar ulang. Pilihan suara di relay berurutan dari Cartesia saat dikonfigurasi, Piper jika model tersedia, lalu Supertonic. Batas klien adalah 42 detik untuk giliran teks, 60 detik untuk jawaban rekaman, dan hingga 138 detik untuk transkripsi cadangan rekaman presentasi. Kegagalan koneksi atau respons yang tidak dapat dipakai memicu dialog tersimpan. Opsi catat waktu menyimpan metrik sesi lokal di folder Evaluation.

Untuk lingkungan pengujian, klien membaca `NARA_ENDPOINT` dan `NARA_ACCESS_TOKEN`. Perangkat XR dapat memakai `backend.json` di folder Practice dengan relay HTTPS yang dapat dijangkau perangkat. Kredensial provider tetap berada pada server itu.

Contoh uji otomatis dengan Unity CLI dari root repo:

```powershell
$unity = 'C:/Program Files/Unity 6000.3.28f1/Editor/Unity.exe'
$project = (Resolve-Path ./NARA).Path
& $unity -batchmode -nographics -projectPath "$project" -executeMethod Nara.Practice.Editor.NaraPracticeValidation.Run -naraSmoke -logFile "$project/playmode.log"
```

Setelah relay `--mock` berjalan, tambahkan `-naraMock` pada perintah itu untuk menguji respons HTTP, follow-up, dan putus koneksi. Cari `NARA_SMOKE PASS` dalam log. Jalankan sesi Unity CLI dengan editor proyek tertutup agar lock proyek tidak saling bertabrakan. Metode `Nara.Practice.Editor.NaraPracticeBuild.RebuildWindows` dapat dipakai dengan `-batchmode -quit` untuk membangun kedua scene dan player sekaligus memanggang ulang refleksi. Jalankan player dengan `-naraSmoke -naraSmokeSwitch` untuk menguji perpindahan kelas melalui tombol persiapan dan pemulihan isian.

Untuk uji langsung dengan relay live, jalankan `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/nara_start_live.ps1 -SmokeTest` dari root repo. Uji ini mengirim contoh materi peminjaman sepeda dan satu jawaban tertulis. Periksa giliran kedua penanya, WAV yang dibuat untuk mereka, dan respons penutup dari pemandu dengan mencari `NARA_SMOKE PASS` di `%USERPROFILE%\.nara\relay\windows-smoke.log`.

Uji impor menambahkan variabel `NARA_SMOKE_PRESENTATION` untuk berkas contoh empat slide dan `NARA_SMOKE_DIR` untuk direktori uji terpisah. Tambahkan `NARA_SMOKE_PRESENTATION_PCM` berisi jalur rekaman contoh PCM mono 16-bit 16 kHz untuk menguji transkripsi saat presentasi dan pengiriman jawaban rekaman. Log `NARA_CAPTION_TIME` mencatat waktu menuju teks dan `NARA_VOICE_START` mencatat waktu dari izin bertanya hingga pemutaran dimulai. Untuk menguji potongan audio yang dikirim bertahap oleh relay uji, tambahkan `NARA_SMOKE_REQUIRE_STREAM=1`. Untuk membangun ke direktori Windows lain, isi `NARA_WINDOWS_BUILD_PATH` dengan jalur lengkap `NARA.exe` saat menjalankan batch Unity. Peluncur dapat membaca jalur pemain dari `playerPath` pada `runtime.json` di direktori relay pengguna.

## Penyuntingan Blender dan aset

Model ruang dan teksturnya dipaketkan dalam [NARA-Kelas-Presentasi.blend](assets/blender/NARA-Kelas-Presentasi.blend) dan [NARA-IF-101.blend](assets/blender/NARA-IF-101.blend). Model [Meja-Kursi.blend](assets/blender/Meja-Kursi.blend) menyimpan furnitur sumber. Marker `NaraPresenter`, `NaraAudienceSeat_R##_C##`, `NaraScreen`, `NaraScreenEdge`, dan marker monitor dibaca oleh generator Unity. Jaga nama marker dan material saat membuat ekspor.

Jika perlu membuat tata ruang dari GLB yang sudah didekode dan furnitur tim, jalankan Blender dengan [tools/nara_prepare_classroom.py](tools/nara_prepare_classroom.py). Argumen posisi adalah GLB dasar, FBX tujuan, BLEND tujuan, PNG pratinjau, dan satu gambar slide. Argumen `--furniture` memilih sumber BLEND. `--upholstery` memilih PNG tekstur kursi tujuan. Opsi `--skip-render` melewati render pratinjau. Sumber GLB yang siap diimpor tersedia di `assets/blender/tevy-classroom-unpacked.glb`.

Jalankan [nara_prepare_if101.py](tools/nara_prepare_if101.py) dengan Blender untuk membuat ulang IF 110 dan tekstur dasarnya. Nama file model, scene, serta kunci preferensi memakai penamaan IF 101 dari proyek awal. [nara_prepare_pbr.py](tools/nara_prepare_pbr.py) mengekstrak peta permukaan dari GLB serta membuat peta normal dan kilap ubin IF.

[Manifest Rocketbox](NARA/Assets/FinalProject/Characters/Rocketbox/SourceAssets.json) mencatat revisi dan blob sumber yang dipakai. [nara_prepare_avatars.py](tools/nara_prepare_avatars.py) memproses sumber mentah jika diperlukan. [nara_make_content.py](tools/nara_make_content.py) menghasilkan slide dengan Pillow dan WAV dengan eSpeak NG. Build biasa menggunakan aset hasil yang ada di repo.

Untuk memanggang ulang gerak duduk, jalankan Blender dari akar repo dengan `blender --background --factory-startup --python tools/nara_bake_seated_motion.py -- "<folder cache FBX>" "NARA/Assets/FinalProject/Resources/Nara/SeatedMotion.bytes"`. Hasilnya menyimpan 20 rekaman pada 15 sampel per detik. Unity melakukan interpolasi per frame. Setelah mengubah bentuk atau pakaian avatar, jalankan **Create practice scenes** untuk memperbarui ukuran kontak pada prefab.

Untuk mengekspor ulang klip Overte, jalankan `blender --background --factory-startup --python tools/nara_bake_overte_motion.py -- "<folder cache FBX>" "NARA/Assets/FinalProject/Resources/Nara/OverteMotion.bytes"`. Skrip mengambil revisi tetap dan memanggang 18 klip pada 24 sampel per detik. Klip 01 dan 02 mengangkat tangan kanan, sedangkan 03 memakai tangan kiri. Berkas atribusi dan lisensi Overte ikut disertakan dalam folder **StreamingAssets/ThirdParty** pada build. [Sumber aset](docs/ASSETS.md)

Ekspor adaptasi HY-Motion dengan `blender --background --factory-startup --python tools/nara_bake_hy_raises.py -- "NARA/Assets/FinalProject/Resources/Nara/OverteMotion.bytes" "NARA/Assets/FinalProject/Resources/Nara/LocalAudienceRaises.bytes" "<FBX angkat tangan HY-Motion>"`. Pengekspor menerima hingga empat FBX dan menghasilkan dua tempo per masukan. Unity dapat memilih masing-masing tempo untuk tangan kanan atau kiri. Data HY-Motion tersimpan lokal. Klip Overte tersedia saat berkas lokal kosong, hilang, atau tidak valid. [Lisensi Tencent](https://huggingface.co/tencent/HY-Motion-1.0/blob/main/LICENSE.txt)

Menu **NARA → Final Project → Validate seated motion** memeriksa semua profil di kedua ruang. Untuk pengujian batch, gunakan metode `Nara.Practice.Editor.NaraSeatedMotionAudit.Run` dan cari `NARA_CONTACT_RESULT` di log. Variabel lingkungan opsional `NARA_CONTACT_IMAGES` memilih folder foto pose dari depan dan samping.

## Bukti dan status pekerjaan

- Build Windows x64 memuat kedua scene. Validator menemukan 259.256 triangle di kelas universitas dan 235.475 di IF 110, termasuk 24 NPC di setiap kelas. Keduanya berada dalam anggaran 285.000 triangle.
- Uji langsung pada player Windows memeriksa perpindahan kelas dua arah, pemulihan pilihan setelah aplikasi dibuka ulang, dan pemeliharaan topik serta ringkasan. Di kedua ruang, uji sesi mencakup 24 NPC unik, undian seluruh kursi, dua bahasa, animasi angkat tangan, dan izin bicara.
- Uji dialog memutar kamera hingga pembicara berada di empat tepi layar, lalu memindahkannya ke balik tembok kelas. Simulasi XR juga menempatkan penghalang di sudut balon. Uji teks memeriksa setiap kata pada halaman audio dan navigasi halaman dari panel jawaban. Uji rig mengamati posisi kedua pergelangan sepanjang sepuluh gerakan menyimak dan lima gestur bicara.
- Audit gerak menguji 30 profil di dua kelas pada 30 frame per detik. Dari 192.660 frame, audit memeriksa ruas lengan dan kelima jari terhadap segitiga tubuh serta lengan satunya pada 2.475 pose. Uji mencatat nol persilangan dan 0,0 mm penetrasi meja pada sampel mesh. Tumpang tindih terbesar antarvolume pendekatan adalah 5,5 mm. Panggul dan ujung kaki tetap di tempat pada 60 kombinasi karakter dan ruang.
- Koreksi lengan sekarang terbawa ke frame berikutnya dan dilepas lebih pelan saat duduk. Di 20 kombinasi karakter dan kelas yang dibandingkan langsung, gerak balik pergelangan terbesar turun dari 36,1 mm ke 9,1 mm, dan siku dari 33,7 mm ke 7,7 mm.
- Audit menjalankan tujuh variasi angkat tangan, empat klip gestur bicara, dan sebelas reaksi menyimak. Empat variasi angkat tangan berasal dari adaptasi lokal satu klip HY-Motion. Permintaan biasa, pembatalan saat tangan masih naik, serta penahanan 15 detik yang melewati batas perulangan semua lolos.
- Build Windows dengan klip tambahan menjalani uji tanya jawab offline di IF 110 sampai selesai. Uji mencakup permintaan giliran, suara dari kursi penanya, tanya jawab, dan saran pemandu.
- Player Windows meloloskan uji tanya jawab di IF 110 dan kelas universitas. Tambahkan `-naraMotionTiming` pada `-naraSmoke` untuk mencatat waktu CPU animasi seluruh audiens selama 180 frame. Pengukuran ini hanya mencakup pemutakhiran rig. [Rekaman kedua kelas](docs/media/NARA-gerak-audiens.mp4) · [Foto pose dari depan dan samping](docs/media/NARA-pose-audiens.jpg)
- Pada laptop AMD Ryzen 5 PRO 2500U, pemutakhiran rig 24 NPC pada build Overte menghasilkan median 3,64 ms dan persentil ke-95 sebesar 5,12 ms di kelas universitas. IF 110 menghasilkan median 4,06 ms dan persentil ke-95 sebesar 10,88 ms pada satu susunan audiens acak. Waktu rig bergantung pada jumlah tangan yang sedang dekat dengan badan atau furnitur.
- Relay simulasi meloloskan 60 kombinasi penanya dan jenis pertanyaan serta menjaga identitas penanya untuk follow-up.
- Build Windows di IF 110 meloloskan sesi live dengan GPT-6.1 Sol. Uji mencakup impor empat slide PowerPoint, penjelasan tertulis, dua NPC penanya dengan suara lokal, jawaban ketik, dan saran lisan pemandu. Jalur rekaman diuji terpisah memakai suara sintetis dan teks slide.
- Dengan respons provider simulasi, relay memeriksa pertanyaan kedua dari NPC yang berbeda, pertanyaan lanjutan yang merujuk pada transkrip rekaman contoh, dan audio saran dari pemandu.
- Audit Blender memeriksa 288 garis pandang ke layar IF 110. Lorong tengah terukur sekitar 1,16 m di kelas universitas dan 1,54 m di IF 110.
- Headset dan mikrofon fisik masih perlu diperiksa.

## Konteks pekerjaan akademik dan AI

Pengerjaan GA3 awal mencakup capture plan, prediksi, audit cacat dari beberapa sudut, komparasi mesh/splat, dan provenance aset. Folder ini membawa implementasi final project NARA. Bukti pengukuran langsung, evaluasi peserta, dan persetujuan tim perlu berasal dari pekerjaan yang benar-benar dilakukan oleh tim.

Pengguna memberi izin asisten untuk membantu penyusunan kata, gambar, Blender, dan skrip, dengan penggunaan AI dicatat. Kontribusi sesi meliputi C# dan Python, desain antarmuka, pencarian serta penyesuaian aset, pengolahan rig, render pratinjau, pembuatan audio sintetis, dan pengujian build. Untuk laporan, jelaskan kontribusi tersebut dan bedakan hasil simulasi dari pengujian manusia atau perangkat.

Untuk gerakan duduk, AI membantu menulis skrip ekspor Blender, penyesuaian gerak di Unity, dan pengujian kontak. Klip sumber berasal dari Microsoft Rocketbox dan Overte. Adaptasi lokal untuk angkat tangan memakai satu hasil generasi HY-Motion 1.0. Foto pose dan video tanya jawab berasal dari render proyek.

AI membantu menghubungkan relay AISUBS dan menguji alur Windows dengan materi contoh. Saat mode live digunakan, GPT-6.1 Sol menyusun pertanyaan serta saran berdasarkan materi, penjelasan, dan jawaban latihan.
