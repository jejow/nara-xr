# Model ruang presentasi universitas

NARA memakai **Classroom v2** dari **tevy** sebagai lingkungan latihan. Model sumber menyediakan ruang tertutup sekitar 8,15 × 8,20 × 3,60 m dengan meja dan kursi berbaris, papan tulis, meja pengajar, pintu, jendela, lampu langit-langit, dan lemari. Rilis v2 bertanggal 6 November 2026. Model LOD0 memiliki 37.320 triangle menurut halaman pembuat. [Halaman aset](https://tevy.dev/models/classroom), [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)

Unduhan v2 LOD0 berukuran 2.789.056 byte dengan SHA-256 `77cffe22885c9786b5f2a9f86a8945b7e77038c150fca02919a015cddaab9bec`. Atribut `asset.copyright` di GLB menyebut CC0 1.0 dan tevy. [GLB asli](https://tevy.dev/models/classroom/v2/lod0.glb)

## Adaptasi untuk NARA

1. Tekstur KTX2 ditranskode ke PNG dengan Basis Universal. Mesh Meshopt diimpor menggunakan Blender 5.2.2. [Basis Universal](https://github.com/BinomialLLC/basis_universal)
2. Sepuluh meja dan kursi memakai model `Table`, `Near chair`, dan `Far chair` dari model Blender tim. Tiga set membentuk panel depan. Tujuh set lainnya disusun dalam dua baris dengan lorong tengah dan ruang dekat pintu. Meja setinggi 0,760 m dan bantalan setinggi 0,468 m mengikuti ukuran model Blender. Dudukan avatar dikalibrasi dari bantalan. [Skrip persiapan](../../../../tools/nara_prepare_classroom.py) · [Model furnitur](../../../../assets/blender/Meja-Kursi.blend)
3. `UniversityClassroom.fbx` menyimpan lingkungan, furnitur tim, podium Blender, perangkat proyeksi, dan marker. Unity memakai `ClassroomAtlas.png` dan tekstur kursi `PanelUpholstery.png` dari gambar terpaket di model Blender tim. Tekstur kursi berukuran sumber 1254 × 1254. [Catatan aset](../../../../docs/ASSETS.md)
4. Unity menampilkan slide yang sama di layar dinding dan monitor podium. Marker Blender menentukan lebar layar 2,78 m dan tepi bawah sekitar 1,27 m untuk menjaga garis pandang dari panel. Gambar dengan rasio berbeda ditampilkan dengan ruang kosong di sekelilingnya. Scan KIRI dikeluarkan dari build FP.

Model Blender tata ruang berisi geometri kelas dan panel, kamera pembicara dan ruangan, pencahayaan render, serta slide contoh pada layar dan monitor. Avatar, sesi suara, dan pergantian slide berjalan di scene Unity.
