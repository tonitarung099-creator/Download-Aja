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
- [ ] Bundel aria2c pada portable artifact
- [ ] Start/stop aria2 otomatis bersama aplikasi
- [ ] Download HTTP/HTTPS nyata
- [ ] Pause/resume/cancel
- [ ] Poll progress, speed, ETA
- [ ] Persistence riwayat dan queue
- [ ] Penanganan error dan retry

## Tahap 3 — Pengalaman setara download manager desktop
- [ ] Single-instance + IPC untuk link dari browser
- [ ] Kategori otomatis berdasarkan tipe file
- [ ] Scheduler dan queue
- [ ] Speed limiter
- [ ] Site credentials/cookies yang aman
- [ ] Browser interception yang dapat dinyalakan/dimatikan
- [ ] Deteksi media non-DRM
- [ ] HLS/DASH non-DRM melalui FFmpeg
- [ ] Drag/drop URL dan clipboard watcher

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
