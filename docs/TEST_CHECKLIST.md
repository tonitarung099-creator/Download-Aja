# Checklist Uji Manual Download Aja 0.5.0

Gunakan artifact portable dari GitHub Actions yang berstatus **success**. Extract seluruh folder sebelum menjalankan `DownloadAja.exe`.

## A. Startup dan portable

- [ ] Aplikasi terbuka tanpa installer.
- [ ] Tidak meminta hak Administrator.
- [ ] `Pengaturan → Tentang & Diagnostik` menunjukkan aria2 = OK.
- [ ] Diagnostik menunjukkan FFmpeg = OK.
- [ ] Diagnostik menunjukkan Browser bridge = OK.
- [ ] `VERSION.txt` dan `BUILD_INFO.txt` tersedia di root portable.

## B. Download HTTP/HTTPS

- [ ] Tambah satu URL file langsung.
- [ ] Progress, ukuran, kecepatan, dan ETA berubah saat download.
- [ ] Jeda menghentikan transfer.
- [ ] Mulai/Lanjut meneruskan download parsial.
- [ ] Tutup aplikasi saat download parsial, buka kembali, lalu lanjutkan.
- [ ] Hentikan lalu Mulai lagi tidak membuat file parsial baru yang salah.
- [ ] Download selesai dapat dibuka dengan double-click.
- [ ] Buka Folder menyorot file hasil.

## C. Nama file dan duplikat

- [ ] Isi nama file manual dan pastikan hasil memakai nama tersebut.
- [ ] Download URL yang sama dua kali ke folder yang sama.
- [ ] Download kedua tidak menimpa file pertama.
- [ ] Resume file parsial tetap memakai nama/path lama.

## D. Antrean

- [ ] Tambahkan beberapa URL dengan opsi mulai sekarang dimatikan.
- [ ] Semua item muncul sebagai Antrean.
- [ ] Atur batas download simultan di Pengaturan.
- [ ] Mulai Antrean tidak melewati batas simultan.
- [ ] Saat satu item selesai, item antrean berikutnya mulai otomatis.
- [ ] Hentikan Antrean tidak mematikan download yang sudah aktif.

## E. Scheduler

- [ ] Buat beberapa item Antrean.
- [ ] Atur scheduler beberapa menit ke depan.
- [ ] Saat waktu tercapai, antrean mulai.
- [ ] Setelah dijalankan, scheduler otomatis nonaktif.

## F. Clipboard dan drag/drop

- [ ] Aktifkan pemantauan clipboard.
- [ ] Salin URL HTTP/HTTPS dan pastikan dialog Tambah URL muncul.
- [ ] URL yang sudah ada di daftar tidak terus diprompt.
- [ ] Drag URL HTTP/HTTPS ke jendela aplikasi dan pastikan ditambahkan.

## G. Browser

- [ ] Buka menu Browser.
- [ ] Pilih browser yang digunakan.
- [ ] Load unpacked folder extension.
- [ ] Masukkan ID extension dan daftarkan integrasi.
- [ ] Klik kanan link → Download dengan Download Aja.
- [ ] Link masuk ke jendela Download Aja yang sudah terbuka, bukan membuka banyak instance.
- [ ] Aktifkan interception otomatis dan coba download file biasa.
- [ ] Tambahkan domain pengecualian dan pastikan domain tersebut tetap diunduh Chrome/browser.

## H. Media

- [ ] Buka halaman dengan file video/audio langsung.
- [ ] Popup extension menampilkan kandidat media.
- [ ] Kirim media langsung ke Download Aja.
- [ ] Coba stream HLS/DASH non-DRM.
- [ ] Hasil HLS/DASH tersimpan sebagai MKV.
- [ ] Hentikan stream menghentikan proses FFmpeg.

## I. Error dan diagnostik

- [ ] Periksa `data/logs` bila aplikasi mengalami error fatal.
- [ ] Salin Diagnostik menghasilkan teks tanpa cookie/password.
- [ ] Periksa Pembaruan tidak mengunduh atau menjalankan file otomatis.
- [ ] Bila belum ada GitHub Release, aplikasi menjelaskan bahwa release resmi belum tersedia.

## Yang perlu dilaporkan jika ada masalah

Kirim:
1. langkah yang dilakukan,
2. hasil yang diharapkan,
3. yang benar-benar terjadi,
4. screenshot bila ada,
5. isi **Tentang & Diagnostik**,
6. file log terbaru dari `data/logs` jika dibuat.

Jangan kirim cookie, password, token, atau API key.


## J. YouTube publik/non-DRM

- [ ] Salin URL video YouTube publik lalu Tambah URL.
- [ ] Pada tab YouTube, popup extension menampilkan tombol Download video halaman ini.
- [ ] Pemutar YouTube menampilkan tombol overlay Download Aja.
- [ ] Navigasi SPA ke video YouTube lain tidak membuat tombol overlay ganda.
- [ ] Tombol tersebut mengirim URL halaman ke aplikasi tanpa harus copy-paste.
- [ ] Item dikenali sebagai kategori Video.
- [ ] yt-dlp mulai mengunduh tanpa meminta instal Python/Node/Deno.
- [ ] Progress, kecepatan, dan ETA tampil saat ukuran dapat diketahui.
- [ ] Setelah selesai, video dan audio sudah tergabung lewat FFmpeg.
- [ ] Hentikan di tengah download lalu Mulai/Coba Lagi dan pastikan file .part dilanjutkan.
- [ ] URL playlist dengan parameter video hanya mengunduh satu video.
- [ ] Channel/beranda YouTube tidak dianggap sebagai video dan menampilkan pesan yang jelas.
- [ ] Video DRM/private/berkontrol akses tidak dicoba dibypass.
