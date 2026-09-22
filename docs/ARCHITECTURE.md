# Arsitektur Download Aja

## Komponen

1. **DownloadAja.Desktop** — UI Windows WPF.
2. **DownloadAja.Core** — model, kontrak, dan adapter engine download.
3. **aria2c** — proses lokal untuk HTTP/HTTPS segmented download dan resume.
4. **DownloadAja.BrowserBridge** — Native Messaging host untuk Chrome/Chromium.
5. **browser-extension/chrome** — extension Manifest V3.
6. **data/** — konfigurasi dan database lokal pada build portable.

## Aliran browser

Chrome extension -> Native Messaging -> BrowserBridge -> DownloadAja.exe -> aria2 RPC.

Extension menyediakan dua mode:
- klik kanan “Download dengan Download Aja”;
- intercept download otomatis (opsional).

## Prinsip portable

Build pengembangan bersifat self-contained dan multi-file. Tidak membutuhkan installer. Registrasi Native Messaging nantinya dilakukan per-user (HKCU), sehingga tidak membutuhkan hak administrator.

## Batas

Download Aja tidak dirancang untuk membypass DRM atau kontrol akses situs.
