# Arsitektur Download Aja

## Komponen

1. **DownloadAja.Desktop** — UI Windows WPF dan single-instance host.
2. **DownloadAja.Core** — model, kontrak, adapter JSON-RPC, dan lifecycle aria2.
3. **aria2c** — proses lokal untuk HTTP/HTTPS segmented download dan resume.
4. **DownloadAja.BrowserBridge** — Native Messaging host untuk Chrome/Chromium.
5. **browser-extension/chrome** — extension Manifest V3.
6. **data/** — konfigurasi dan database lokal pada build portable.

## Aliran browser

Chrome extension -> Native Messaging -> BrowserBridge -> named pipe -> DownloadAja.exe -> aria2 RPC.

Jika Download Aja belum berjalan, BrowserBridge menjalankan DownloadAja.exe dengan `--add-url`. Aplikasi memakai mutex + named pipe sehingga hanya ada satu instance UI; instance kedua meneruskan URL ke instance utama lalu keluar.

## Engine download

- aria2 dijalankan otomatis oleh aplikasi.
- RPC hanya mendengar pada loopback/localhost.
- Port dipilih dinamis.
- Secret RPC dibuat acak setiap aplikasi dijalankan.
- Default segmented download: 8 koneksi, maksimum 16 per item.
- Download default disimpan ke folder Downloads milik pengguna.
- Pause/resume dilakukan melalui RPC; file parsial dapat dilanjutkan.

## Prinsip portable

Build pengembangan bersifat self-contained dan multi-file. Tidak membutuhkan installer. Registrasi Native Messaging dilakukan per-user (HKCU), sehingga tidak membutuhkan hak administrator.

## Batas

Download Aja tidak dirancang untuk membypass DRM atau kontrol akses situs.
