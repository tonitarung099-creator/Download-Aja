# Changelog

Semua perubahan penting Download Aja dicatat di file ini.

## 0.5.0 — YouTube & Chromium

### Fitur baru
- Download video YouTube publik/non-DRM melalui `yt-dlp`.
- Deno dibundel sebagai JavaScript runtime portable untuk dukungan YouTube.
- FFmpeg + ffprobe dipakai untuk merge/post-processing media.
- Tombol **Download video halaman ini** pada popup extension YouTube.
- Pilihan kualitas YouTube tersimpan: Best, 2160p, 1440p, 1080p, 720p, 480p, 360p.
- Tombol overlay pemutar menampilkan kualitas yang sedang dipilih.
- Menu klik kanan khusus halaman YouTube.
- Wizard browser mendukung Chrome, Edge, Brave, dan Vivaldi.
- Wizard menerima beberapa ID extension sekaligus.
- Halaman Tentang & Diagnostik menampilkan provenance build portable.
- Update checker berbasis GitHub Releases.

### Stabilitas
- Stop → Mulai/Coba Lagi pada YouTube memakai mekanisme `.part` yt-dlp.
- Cache yt-dlp dan Deno dipaksa ke folder `data/` portable.
- Config/plugin eksternal yt-dlp tidak boleh mengubah perilaku aplikasi.
- Cookie sesi YouTube tidak dikirim ke aplikasi pada mode publik.
- Error YouTube private/login/DRM dibuat lebih jelas.
- Penanganan nama file bentrok tanpa menimpa file lama atau merusak resume.

### CI / pengujian
- Smoke test aria2 HTTP lokal dengan verifikasi SHA-256.
- Regression test auto-rename download duplikat.
- Smoke test FFmpeg HLS remux.
- Smoke test yt-dlp download media lokal.
- Verifikasi output progress + marker hasil yt-dlp.
- Test portability dengan config yt-dlp eksternal yang sengaja salah.
- Test stop/resume yt-dlp melalui HTTP Range server lokal.
- Portable artifact menyertakan `VERSION.txt` dan `BUILD_INFO.txt`.

### Batasan 0.5.0
- YouTube hanya untuk media publik/non-DRM; tidak membypass login, member-only, paywall, atau kontrol akses.
- Playlist massal belum diaktifkan; satu URL video = satu pekerjaan.
- Pause langsung proses yt-dlp belum digunakan; alur yang didukung adalah Hentikan lalu Mulai/Coba Lagi.
- Stream HLS/DASH FFmpeg masih belum memiliki pause/resume khusus.

## 0.4.0 — Stabilization baseline

- Multi-connection HTTP/HTTPS melalui aria2.
- Pause/resume/cancel untuk download HTTP.
- Queue, batas simultan, scheduler, speed limiter.
- Persistence riwayat dan resume setelah aplikasi dibuka ulang.
- Clipboard watcher, drag/drop URL, pencarian, filter, kategori.
- Integrasi browser Chromium melalui Native Messaging.
- Deteksi media langsung dan HLS/DASH non-DRM melalui FFmpeg.
- Shortcut keyboard dasar.
- Halaman diagnostik lokal.
