# Roadmap Download Aja

## Tahap 1 — Fondasi
- [x] Struktur WPF .NET 8
- [x] UI utama bergaya download manager klasik
- [x] Dialog tambah URL
- [x] Adapter JSON-RPC aria2
- [x] Extension Chrome Manifest V3
- [x] Native Messaging bridge
- [x] Workflow portable Windows self-contained

## Tahap 2 — Download nyata
- [x] Bundel aria2c pada portable artifact
- [x] Start/stop aria2 otomatis bersama aplikasi
- [x] Download HTTP/HTTPS nyata
- [x] Pause/resume/cancel
- [x] Poll progress, speed, ETA
- [x] Persistence riwayat download
- [x] Resume file parsial setelah aplikasi dibuka ulang
- [x] Retry otomatis terbatas untuk koneksi putus
- [x] Queue download persisten
- [x] Batas download simultan saat queue berjalan
- [x] Scheduler satu kali untuk menjalankan queue

## Tahap 3 — Pengalaman download manager desktop
- [x] Single-instance + IPC untuk link dari browser
- [x] Kategori otomatis berdasarkan tipe file
- [x] Pencarian dan filter status/kategori
- [x] Pengaturan koneksi per download
- [x] Speed limiter untuk download baru
- [x] Pilihan folder tujuan per download
- [x] Drag/drop URL
- [x] Clipboard watcher opsional
- [x] Context menu file + double-click buka file selesai
- [x] Wizard integrasi Chrome dari aplikasi
- [x] Cookie/referrer/User-Agent opsional dari Chrome
- [x] Interception download Chrome yang dapat dinyalakan/dimatikan dari extension
- [x] Deteksi media langsung non-DRM di halaman
- [x] HLS/DASH non-DRM melalui FFmpeg
- [ ] Pause/resume khusus stream FFmpeg
- [ ] Dukungan browser Chromium lain (Edge/Brave/Vivaldi) dari wizard yang sama
- [ ] Aturan situs / pengecualian interception per domain

## Tahap 4 — Kualitas
- [x] Smoke test persistence dan klasifikasi
- [x] Validasi JavaScript extension
- [x] Verifikasi executable aria2/FFmpeg di CI
- [x] Smoke test HLS lokal melalui FFmpeg
- [ ] Test unit lebih lengkap
- [x] Test integrasi HTTP lokal untuk aria2
- [x] Crash logging lokal
- [x] Keyboard shortcuts dasar
- [ ] Audit accessibility lengkap
- [ ] Dark mode
- [ ] Update checker

## Tahap 5 — Rilis
- [x] Portable ZIP otomatis dari GitHub Actions
- [ ] Portable release candidate stabil
- [ ] Signed binaries bila sertifikat tersedia
- [ ] Installer Windows
- [ ] Registrasi extension/browser dari installer
- [ ] Uninstaller bersih
