# Download Aja

**Download Aja** adalah download manager Windows berbahasa Indonesia dengan workflow yang familier bagi pengguna download manager desktop klasik. Aplikasi dibuat dengan identitas dan kode sendiri.

## Teknologi

- Desktop: C# / .NET 8 / WPF
- Download HTTP/HTTPS: aria2 1.37.0 melalui JSON-RPC lokal
- HLS/DASH non-DRM: FFmpeg 8.1.3 yang dibundel di portable
- YouTube publik/non-DRM: yt-dlp 2026.08.19 + Deno 2.9.7 + FFmpeg
- Integrasi Chrome: Manifest V3 + Native Messaging
- Distribusi development: portable folder multi-file dalam ZIP
- Tidak membutuhkan installer atau hak Administrator untuk menjalankan aplikasi

## Fitur yang sudah bekerja

- Tambah URL HTTP/HTTPS
- Multi-connection download
- Pause, resume, stop, retry
- Riwayat persisten dan resume file parsial
- Progress, speed, ETA, kategori, pencarian, filter
- Pilih folder tujuan per download
- Queue dengan batas download simultan
- Scheduler satu kali untuk menjalankan queue
- Speed limiter
- Drag-and-drop URL
- Clipboard watcher opsional
- Double-click file selesai untuk membuka file
- Context menu: buka file/folder, salin URL, mulai/coba lagi, jeda
- Chrome context menu “Download dengan Download Aja”
- Interception download Chrome opsional
- Cookie/referrer/User-Agent opsional untuk link yang membutuhkan sesi login
- Deteksi media langsung di tab Chrome
- Download HLS/DASH non-DRM melalui FFmpeg
- Download URL video YouTube publik/non-DRM melalui yt-dlp (playlist dimatikan)

## Menjalankan portable

1. Unduh artifact **Download-Aja-Windows-x64-Portable** dari GitHub Actions terbaru yang hijau.
2. Extract seluruh folder.
3. Jalankan `DownloadAja.exe`.
4. Jangan memindahkan hanya file EXE; folder `tools`, `browser-bridge`, dan resource lain harus tetap bersama aplikasi.

## Menghubungkan Chrome

1. Di aplikasi klik **Browser**.
2. Klik **Buka chrome://extensions**.
3. Aktifkan **Mode developer**.
4. Klik **Load unpacked / Muat yang belum dikemas** dan pilih folder extension yang ditampilkan aplikasi.
5. Salin ID extension 32 karakter dari Chrome.
6. Tempel ID tersebut ke wizard Download Aja lalu klik **Daftarkan Integrasi**.
7. Jika perlu, restart Chrome.

Pendaftaran Native Messaging memakai registry pengguna Windows (HKCU), jadi tidak membutuhkan hak Administrator.

## Struktur repository

```
src/DownloadAja.Desktop/      aplikasi Windows
src/DownloadAja.Core/         engine/model inti
src/DownloadAja.BrowserBridge/ native messaging bridge
browser-extension/chrome/     extension Chrome
tests/                        smoke tests
docs/                         arsitektur dan roadmap
.github/workflows/            build portable otomatis
```

## Catatan media

Deteksi media ditujukan untuk media langsung, stream HLS/DASH **non-DRM**, dan URL YouTube publik/non-DRM. Download Aja tidak dirancang untuk melewati DRM, paywall, login/access control, atau pembatasan hak akses layanan.


## Uji manual

Checklist pengujian Windows tersedia di `docs/TEST_CHECKLIST.md`. Gunakan checklist tersebut saat menguji artifact portable agar bug dapat direproduksi dengan jelas.
