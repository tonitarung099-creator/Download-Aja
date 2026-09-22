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
