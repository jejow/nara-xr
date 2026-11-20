# Model ruang presentasi universitas

NARA memakai **Classroom v2** dari **tevy** sebagai lingkungan latihan. Model sumber menyediakan ruang tertutup sekitar 8,15 × 8,20 × 3,60 m dengan meja dan kursi berbaris, papan tulis, meja pengajar, pintu, jendela, lampu langit-langit, dan lemari. Rilis v2 bertanggal 6 November 2026. Model LOD0 memiliki 37.320 triangle menurut halaman pembuat. [Halaman aset](https://tevy.dev/models/classroom), [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)

Unduhan v2 LOD0 berukuran 2.789.056 byte dengan SHA-256 `77cffe22885c9786b5f2a9f86a8945b7e77038c150fca02919a015cddaab9bec`. Atribut `asset.copyright` di GLB menyebut CC0 1.0 dan tevy. [GLB asli](https://tevy.dev/models/classroom/v2/lod0.glb)

## Adaptasi untuk NARA

1. Tekstur KTX2 ditranskode ke PNG dengan Basis Universal. Mesh Meshopt diimpor menggunakan Blender 5.2.2. [Basis Universal](https://github.com/BinomialLLC/basis_universal)
2. Empat baris masing-masing berisi enam meja dan kursi yang menghadap proyektor. Kelas universitas mengimpor objek `Table` dari model Blender tim. Meja memiliki lebar 0,923 m, kedalaman 0,518 m, dan tinggi asli 0,760 m. Mesh `Near chair` disederhanakan dengan bantalan setinggi sekitar 0,468 m. [Skrip persiapan](../../../../tools/nara_prepare_classroom.py) · [Model furnitur](../../../../assets/blender/Meja-Kursi.blend)
3. `UniversityClassroom.fbx` menyimpan lingkungan, perabot, podium, proyektor, dan marker `NaraAudienceSeat_R##_C##`. Unity memakai peta warna, normal, metallic, kekasaran, dan oklusi dari GLB, serta tekstur kain tim dan serat kayu podium. [Persiapan peta PBR](../../../../tools/nara_prepare_pbr.py)
4. Podium memakai panel oak, permukaan baca miring, monitor slide dan mikrofon model. Unity menampilkan slide yang sama pada monitor dan layar proyeksi selebar 2,78 m. Audit menguji enam titik layar dari dua tinggi mata di setiap kursi.

Model Blender memuat geometri kelas, kamera pembicara dan ruangan, pencahayaan render, serta slide contoh pada layar dan monitor. Avatar, sesi suara, dan pergantian slide berjalan di scene Unity.

## IF 110

`IF101Classroom.fbx` memakai model yang disusun dari referensi foto dan video IF 101. Menu NARA menyebut adaptasi latihan ini IF 110. Ruang memakai 24 kursi kayu dan layar PPT selebar 2,42 m di antara dua papan tulis. Lorong tengah sekitar 1,54 m. Skala ubin, portal pintu oranye, tirai, dan platform pengajar mengikuti model referensi. Ubin memakai peta normal nat dan kilap terpisah. Kedua ruang memakai refleksi interior yang dipanggang saat scene dibangun. [Skrip pembuat ruang](../../../../tools/nara_prepare_if101.py) · [Model ruangan](../../../../assets/blender/NARA-IF-101.blend)
