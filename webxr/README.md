# NARA WebXR

NARA berjalan di browser sebagai ruang latihan presentasi 3D dengan model Blender dan aset audiens dari proyek NARA.

## Laptop

Buka `Start-WebXR.cmd` atau jalankan dari folder ini dengan Node.js 22.12 atau lebih baru.

```powershell
npm ci
npm run dev
```

`npm run build` membuat halaman dan aset di `dist/client` serta handler di `dist/server/index.js`. `npm run preview` menjalankan build di localhost, termasuk rute tanya jawab AISUBS.

## Headset

Buka URL HTTPS di browser headset, pilih kelas dan materi, lalu tekan **Masuk VR**. Arahkan controller ke panel dan tekan trigger untuk memilih. A memilih aksi utama, B atau Y maju slide, X mundur. Grip menyalakan atau menghentikan mikrofon. Tekan thumbstick untuk memusatkan panel.

Pilih berkas materi sebelum masuk VR. Panel di headset menyediakan mikrofon, papan ketik, kontrol suara, dan tombol untuk memusatkan panel.

## Materi dan suara

- Unggah PPTX, PPT, PDF, atau gambar maksimal 35 MB, sampai 60 slide. PDF mempertahankan desain. Pratinjau PPTX mendukung teks, gambar, bentuk dasar, dan tabel. PPT lama mengambil isi teks per slide. Aktifkan OCR di pengaturan sebelum mengimpor gambar jika diperlukan.
- Mode otomatis memakai `gpt-6.1-sol` lewat AISUBS ketika server siap. Mode offline memakai rekaman dan naskah dari build. Opsi kunci pribadi menyimpannya hanya di browser pemilik. Mode AI mengirim konteks teks ke AISUBS melalui server aplikasi.
- Model suara dan mikrofon dimuat otomatis di latar belakang. Browser mentranskrip rekaman di perangkat. Tinjau atau edit hasilnya sebelum dikirim. Dialog AI memakai suara lokal yang tersedia atau Piper. Pengunduhan awal model suara memerlukan internet.
- Materi, ruang, bahasa, durasi, topik, pilihan kamera, dan pilihan suara tersimpan di browser yang sama.

## Aset

Kelas universitas memakai meja dan kursi Blender tim, podium, proyektor, dan monitor presenter. IF 110 memakai kursi kayu dan layar. Di setiap ruang, 24 anggota audiens diambil acak dari 30 profil Rocketbox. Gerakan memakai data duduk, bicara, dan angkat tangan dari NARA dengan penyesuaian kerangka di browser. [Daftar aset dan lisensi](public/media/credits/ASSETS.md)

Aset WebXR untuk build publik tersedia di `public/media/`. Untuk mengganti aset tersebut, ekspor ulang dari repo NARA dengan Blender 5.2, Pillow, dan soundfile.

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' -b --python-exit-code 20 -P scripts/export-blender.py -- rooms avatars
python scripts/export-static.py
```

Saat deploy Worker, binding `ASSETS` menunjuk ke `dist/client`. Handler dan aset perlu berada pada origin HTTPS yang sama untuk dipakai dari headset.

## Hosting di Netlify

Jalankan dari folder `webxr`. Netlify menyajikan `dist/client` dan menjalankan tanya jawab AISUBS di `/api/turn`.

```powershell
npm run build:netlify
npx netlify-cli@27.20.1 dev --offline
```

Untuk deploy dari Git, pilih `webxr` sebagai direktori dasar. Netlify CLI juga dapat mengunggah hasil build dan fungsi dari folder ini.

Situs produksi dapat dibuka tanpa login. Kunci bawaan dibaca oleh fungsi Netlify dari penyimpanan privat proyek. Kuota bersama mencakup maksimal 120 permintaan AI per hari WIB. Saat kuota habis, latihan bisa dilanjutkan dengan naskah bawaan atau memakai kunci pribadi. Saat menunggu AI, pilih **Pakai naskah bawaan** untuk langsung melanjutkan sesi. [NARA WebXR di Netlify](https://nara-studio-webxr.netlify.app)
