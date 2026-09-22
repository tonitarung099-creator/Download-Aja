# Download Aja

Download manager untuk Windows dengan pengalaman penggunaan yang familier bagi pengguna IDM, tetapi dengan identitas dan kode sendiri.

## Arah proyek

- Desktop Windows: C# / .NET 8 / WPF
- Mesin download: aria2 melalui JSON-RPC
- Integrasi Chrome/Chromium: Manifest V3 + Native Messaging
- Development build: portable folder (ZIP)
- Final release: installer Windows setelah aplikasi stabil
- Bahasa antarmuka utama: Indonesia

## Target fitur

- Tambah URL, mulai, jeda, lanjutkan, hentikan, hapus
- Segmented / multi-connection download
- Resume setelah koneksi terputus atau aplikasi dibuka ulang
- Queue, scheduler, speed limiter
- Kategori Otomatis: Semua, Mengunduh, Selesai, Video, Audio, Dokumen, Program, Arsip
- Deteksi link/video dari browser
- Context menu "Download dengan Download Aja"
- Integrasi clipboard
- Riwayat dan pencarian
- Portable self-contained build
- Installer Windows pada tahap rilis final

## Prinsip UI

Workflow dibuat familier seperti download manager klasik: toolbar aksi utama, daftar download, kategori di sisi kiri, status/progress di tengah, dan panel detail. Aset, logo, ikon, serta tata letak tidak menyalin aplikasi proprietary secara 1:1.

## Struktur awal

```
src/DownloadAja.Desktop/      aplikasi Windows
src/DownloadAja.Core/         model dan kontrak inti
browser-extension/chrome/     extension Chrome/Chromium
docs/                         arsitektur dan roadmap
.github/workflows/            build otomatis
```

## Status

Fondasi proyek sedang dibangun.