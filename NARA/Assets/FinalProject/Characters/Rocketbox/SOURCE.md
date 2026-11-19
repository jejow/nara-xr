# Avatar audiens Microsoft Rocketbox

NARA menyertakan 24 model dewasa yang berbeda dari **Microsoft Rocketbox Avatar Library** untuk penanya dan dua anggota audiens di ruang presentasi. Nama di menu adalah label fiktif untuk memudahkan pemilihan. [Repositori sumber](https://github.com/microsoft/Microsoft-Rocketbox), [lisensi MIT](LICENSE-Microsoft-Rocketbox.md)

FBX wajah, tekstur, dan potret berasal dari revisi `0943055db6ec570bcef9f2c8b41c9e5467c808f9`. Manifest mencatat jalur unduhan, nama hasil konversi, dan hash blob Git yang diverifikasi sebelum aset dipakai. [Manifest aset sumber](SourceAssets.json)

## Adaptasi

- Blender mempertahankan kerangka Rocketbox dan tujuh ekspresi ARKit pada setiap model. Ekspresi tersebut meliputi dua kedipan, rahang terbuka, bibir mengerucut, dua sisi senyum, dan alis terangkat.
- Tekstur kulit dan pakaian disiapkan dengan ukuran maksimum 1024 piksel. Shader potong alfa mempertahankan detail rambut dan aksesori pada material yang memakai opasitas. Potret menu menggunakan gambar pratinjau dari pustaka sumber.
- Unity menyesuaikan pose duduk pada kursi baris depan. Gerakan runtime mengatur napas, anggukan, kedipan, dan gestur lengan ringan. Intensitas audio penanya menggerakkan bentuk rahang dan bibir.
- Penanya mengangkat tangan sebelum diberi giliran. Pose memakai tulang bahu, siku, dan pergelangan model Rocketbox. Setelah mendapat izin, penanya mengangguk dan menurunkan tangan. Avatar lain melirik penanya saat suara berasal dari kursinya.
- Prefab dihasilkan dari FBX melalui `RocketboxAudienceBuilder`. Menu memuat model yang dipilih untuk tiga kursi dan menyimpan pilihan profil secara lokal. Pilihan avatar yang sudah menempati kursi lain menukar kedua posisi.

Pembuat pustaka juga mendokumentasikan rig dan penggunaannya di VR dalam artikel riset. [Gonzalez-Franco dkk.](https://doi.org/10.3389/frvir.2020.561558)
