# Roadmap Download Aja

## Tahap 1 — Fondasi
- [x] Struktur WPF .NET 8
- [x] UI utama bergaya download manager klasik
- [x] Dialog tambah URL
- [x] Adapter JSON-RPC aria2
- [x] Extension Chrome Manifest V3
- [x] Native Messaging bridge dasar
- [x] Workflow portable Windows

## Tahap 2 — Download nyata
- [x] Bundel aria2c pada portable artifact
- [x] Start/stop aria2 otomatis bersama aplikasi
- [x] Download HTTP/HTTPS nyata
- [x] Pause/resume/cancel
- [x] Poll progress, speed, ETA
- [x] Persistence riwayat download
- [x] Resume file parsial setelah aplikasi dibuka ulang
- [ ] Retry otomatis dengan kebijakan backoff
- [ ] Queue download persisten

## Tahap 3 — Pengalaman setara download manager desktop
- [x] Single-instance + IPC untuk link dari browser
- [x] Kategori otomatis berdasarkan tipe file
- [x] Pencarian dan filter status/kategori
- [x] Pengaturan koneksi per download
- [x] Speed limiter untuk download baru
- [ ] Scheduler dan queue
- [ ] Site credentials/cookies yang aman
- [ ] Browser interception yang dapat dinyalakan/dimatikan dari aplikasi
- [ ] Deteksi media non-DRM
- [ ] HLS/DASH non-DRM melalui FFmpeg
- [ ] Drag/drop URL dan clipboard watcher
- [ ] Pilihan folder tujuan per download

## Tahap 4 — Kualitas
- [ ] Test unit core
- [ ] Test integrasi engine
- [ ] Crash logging lokal
- [ ] Accessibility dan keyboard shortcuts
- [ ] Dark mode
- [ ] Update checker

## Tahap 5 — Rilis
- [ ] Portable ZIP stabil
- [ ] Signed binaries bila sertifikat tersedia
- [ ] Installer Windows
- [ ] Registrasi extension/browser dari installer
- [ ] Uninstaller bersih
