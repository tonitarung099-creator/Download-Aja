# Third-party notices

## aria2

Download Aja portable build menyertakan **aria2 1.37.0** sebagai executable terpisah pada `tools/aria2/aria2c.exe`.

- Project: https://github.com/aria2/aria2
- Release: release-1.37.0
- License: GPL-2.0-or-later
- License text: disertakan pada `tools/aria2/COPYING`
- Source code: tersedia dari repository dan release resmi aria2 di GitHub.

Download Aja berkomunikasi dengan aria2 melalui JSON-RPC lokal.


## FFmpeg / BtbN FFmpeg-Builds

Download Aja portable build menyertakan `ffmpeg.exe` terpisah untuk menangani stream HLS/DASH non-DRM.

- FFmpeg project: https://ffmpeg.org/
- Windows build source: https://github.com/BtbN/FFmpeg-Builds
- Pinned build: `autobuild-2026-09-21-13-55`, FFmpeg 8.1.3 win64 LGPL
- Build asset SHA-256: `ef320b236065afe9a8dfd5f2b5aeca0fd0f0196c6dd77958b735fcbb74c10f58`
- License: LGPL-compatible build; license file dari paket disertakan bila tersedia.
- FFmpeg source code tersedia dari project FFmpeg dan recipe/build scripts tersedia di repository BtbN.

FFmpeg dijalankan sebagai proses terpisah; Download Aja tidak menautkan library FFmpeg ke executable aplikasi.


## yt-dlp

Download Aja portable build menyertakan **yt-dlp 2026.08.19** sebagai executable terpisah pada `tools/yt-dlp/yt-dlp.exe`.

- Project: https://github.com/yt-dlp/yt-dlp
- Release: 2026.08.19
- SHA-256: `66674953fe251b89f4d08c5f0e35e0728679bd67ab3d7d05c0562af101dd3e7a`
- License: Unlicense / public domain dedication
- License text: disertakan pada `tools/yt-dlp/LICENSE.txt`

yt-dlp dipakai hanya sebagai proses terpisah untuk URL media yang didukung. Download Aja tidak dirancang untuk membypass DRM atau kontrol akses.

## Deno

Download Aja portable build menyertakan **Deno 2.9.7** pada `tools/deno/deno.exe` sebagai JavaScript runtime lokal yang direkomendasikan yt-dlp untuk dukungan YouTube.

- Project: https://github.com/denoland/deno
- Release: v2.9.7
- Archive SHA-256: `a0c3101b4158d1dfb7d6a78a7bf0f3de80c96bb423c152beec8beb22786f2238`
- License: MIT
- License text: disertakan pada `tools/deno/LICENSE.txt`

## yt-dlp-ejs

Executable resmi yt-dlp menggunakan komponen EJS untuk dukungan YouTube. Project EJS berlisensi Unlicense dan juga membundel komponen MIT/ISC; lihat repository upstream: https://github.com/yt-dlp/ejs
