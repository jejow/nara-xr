# Avatar audiens Microsoft Rocketbox

NARA mengisi 24 kursi dari 30 model dewasa **Microsoft Rocketbox Avatar Library**. Nama anggota audiens adalah fiktif. [Repositori sumber](https://github.com/microsoft/Microsoft-Rocketbox) · [Lisensi MIT repositori](LICENSE-Microsoft-Rocketbox.md)

FBX wajah, tekstur, dan potret berasal dari revisi `0943055db6ec570bcef9f2c8b41c9e5467c808f9`. Manifest mencatat jalur unduhan, nama hasil konversi, dan hash blob Git yang diverifikasi sebelum aset dipakai. [Manifest aset sumber](SourceAssets.json)

## Adaptasi

- Blender mempertahankan kerangka Rocketbox dan tujuh ekspresi ARKit pada setiap model. Ekspresi tersebut meliputi dua kedipan, rahang terbuka, bibir mengerucut, dua sisi senyum, dan alis terangkat.
- Tekstur kulit dan pakaian disiapkan dengan ukuran maksimum 1024 piksel. Shader potong alfa mempertahankan detail rambut dan aksesori pada material yang memakai opasitas. Potret menu menggunakan gambar pratinjau dari pustaka sumber.
- Blender mengekstrak lintasan pergelangan dan siku, arah telapak, serta sudut badan dari sepuluh rekaman duduk di kursi dan sepuluh rekaman duduk di meja pada revisi yang sama. Skrip mencatat nama setiap rekaman. Unity menyesuaikan lintasan itu dengan panjang lengan tiap avatar. [Rekaman sumber](https://github.com/microsoft/Microsoft-Rocketbox/tree/0943055db6ec570bcef9f2c8b41c9e5467c808f9/Assets/Animations/all_animations_max_motextr_static) · [Skrip Blender](../../../../../tools/nara_bake_seated_motion.py)
- Penanya mengangkat tangan sebelum diberi giliran. Pose memakai tulang bahu, siku, dan pergelangan model Rocketbox. Setelah mendapat izin, penanya mengangguk dan menurunkan tangan. Avatar lain melirik penanya saat suara berasal dari kursinya.
- Prefab dihasilkan dari FBX melalui `RocketboxAudienceBuilder`. Setiap prefab menyimpan ukuran badan, kepala, paha, dan lengan yang diukur dari kulit serta pakaian model. Saat latihan berjalan, tinggi meja diambil dari furnitur pada kursi masing-masing.

Pembuat pustaka juga mendokumentasikan rig dan penggunaannya di VR dalam artikel riset. [Gonzalez-Franco dkk.](https://doi.org/10.3389/frvir.2020.561558)
