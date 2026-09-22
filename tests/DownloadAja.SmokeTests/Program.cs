using DownloadAja.Core.Models;
using DownloadAja.Core.Services;

var failures = new List<string>();

void Check(bool condition, string message)
{
    if (!condition)
        failures.Add(message);
}

Check(new DownloadItem { Name = "film.mkv" }.Category == "Video", "Kategori MKV harus Video.");
Check(new DownloadItem { Name = "musik.flac" }.Category == "Audio", "Kategori FLAC harus Audio.");
Check(new DownloadItem { Name = "laporan.pdf" }.Category == "Dokumen", "Kategori PDF harus Dokumen.");
Check(new DownloadItem { Name = "setup.exe" }.Category == "Program", "Kategori EXE harus Program.");
Check(new DownloadItem { Name = "backup.7z" }.Category == "Arsip", "Kategori 7Z harus Arsip.");
Check(new DownloadItem { Name = "YouTube video", EngineKind = DownloadEngineKind.YtDlp }.Category == "Video",
    "Engine yt-dlp harus dikategorikan sebagai Video.");

Check(YouTubeUrlClassifier.IsVideoUrl("https://www.youtube.com/watch?v=abc123"), "URL watch YouTube harus dikenali sebagai video.");
Check(YouTubeUrlClassifier.IsVideoUrl("https://youtu.be/abc123"), "URL youtu.be harus dikenali sebagai video.");
Check(YouTubeUrlClassifier.IsVideoUrl("https://www.youtube.com/shorts/abc123"), "URL Shorts harus dikenali sebagai video.");
Check(YouTubeUrlClassifier.IsVideoUrl("https://www.youtube.com/live/abc123"), "URL live harus dikenali sebagai video.");
Check(YouTubeUrlClassifier.IsVideoUrl("https://www.youtube-nocookie.com/embed/abc123"), "URL embed youtube-nocookie harus dikenali.");
Check(!YouTubeUrlClassifier.IsVideoUrl("https://www.youtube.com/"), "Beranda YouTube tidak boleh dikenali sebagai video.");
Check(!YouTubeUrlClassifier.IsVideoUrl("https://www.youtube.com/@channel"), "Channel YouTube tidak boleh dikenali sebagai video.");
Check(!YouTubeUrlClassifier.IsVideoUrl("https://www.youtube.com/playlist?list=PL123"), "Playlist murni tidak boleh dikenali sebagai video.");
Check(!YouTubeUrlClassifier.IsYouTubeHost("https://notyoutube.com/watch?v=abc"), "Domain mirip YouTube tidak boleh dianggap YouTube.");

Check(YouTubeFormatProfiles.Normalize("1080P") == YouTubeFormatProfiles.P1080, "Profil kualitas harus dinormalisasi case-insensitive.");
Check(YouTubeFormatProfiles.Normalize("tidak-valid") == YouTubeFormatProfiles.Best, "Profil kualitas tidak valid harus kembali ke Best.");
Check(YouTubeFormatProfiles.GetFormatSelector(YouTubeFormatProfiles.P720).Contains("height<=720", StringComparison.Ordinal),
    "Selector 720p harus membatasi tinggi video.");

var settings = new DownloadSettings
{
    ConnectionsPerDownload = 99,
    SpeedLimitBytesPerSecond = -1,
    MaxSimultaneousDownloads = 99,
    SchedulerEnabled = false,
    ScheduledQueueStartAt = DateTimeOffset.Now.AddHours(1)
}.Normalize();

Check(settings.ConnectionsPerDownload == 16, "Koneksi harus dibatasi maksimum 16.");
Check(settings.SpeedLimitBytesPerSecond == 0, "Speed limit negatif harus menjadi 0.");
Check(settings.MaxSimultaneousDownloads == 20, "Download simultan harus dibatasi maksimum 20.");
Check(settings.ScheduledQueueStartAt is null, "Jadwal harus dibersihkan saat scheduler nonaktif.");

var tempDirectory = Path.Combine(Path.GetTempPath(), "DownloadAjaSmoke", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDirectory);

try
{
    var statePath = Path.Combine(tempDirectory, "downloads.json");
    var store = new DownloadHistoryStore(statePath);

    var source = new[]
    {
        new DownloadItem
        {
            Name = "selesai.zip",
            Url = "https://example.com/selesai.zip",
            DirectoryPath = tempDirectory,
            FilePath = Path.Combine(tempDirectory, "selesai.zip"),
            TotalBytes = 1000,
            CompletedBytes = 1000,
            Status = DownloadStatus.Selesai,
            EngineKind = DownloadEngineKind.Aria2,
            Gid = "old-finished-gid"
        },
        new DownloadItem
        {
            Name = "parsial.iso",
            Url = "https://example.com/parsial.iso",
            DirectoryPath = tempDirectory,
            FilePath = Path.Combine(tempDirectory, "parsial.iso"),
            TotalBytes = 2000,
            CompletedBytes = 750,
            Status = DownloadStatus.Mengunduh,
            EngineKind = DownloadEngineKind.Aria2,
            Gid = "old-active-gid"
        }
    };

    await store.SaveAsync(source);
    var restored = await store.LoadAsync(tempDirectory);

    Check(restored.Count == 2, "Riwayat harus memuat kembali dua item.");

    var finished = restored.SingleOrDefault(x => x.Name == "selesai.zip");
    Check(finished is not null, "Item selesai harus ada setelah reload.");
    Check(finished?.Status == DownloadStatus.Selesai, "Status selesai harus dipertahankan.");
    Check(finished?.Gid is null, "GID lama tidak boleh dipertahankan.");

    var partial = restored.SingleOrDefault(x => x.Name == "parsial.iso");
    Check(partial is not null, "Item parsial harus ada setelah reload.");
    Check(partial?.Status == DownloadStatus.Dijeda, "Download parsial harus dipulihkan sebagai Dijeda.");
    Check(partial?.CompletedBytes == 750, "Progress parsial harus dipertahankan.");
    Check(partial?.Gid is null, "GID sesi lama untuk download parsial harus dibuang.");

    var ffmpegSource = new[]
    {
        new DownloadItem
        {
            Name = "stream.mkv",
            Url = "https://example.com/master.m3u8",
            DirectoryPath = tempDirectory,
            FilePath = Path.Combine(tempDirectory, "stream.mkv"),
            CompletedBytes = 1234,
            Status = DownloadStatus.Mengunduh,
            EngineKind = DownloadEngineKind.Ffmpeg
        }
    };

    await store.SaveAsync(ffmpegSource);
    var ffmpegRestored = await store.LoadAsync(tempDirectory);
    Check(ffmpegRestored.Count == 1, "Riwayat stream FFmpeg harus dapat dimuat.");
    Check(ffmpegRestored[0].EngineKind == DownloadEngineKind.Ffmpeg, "Engine FFmpeg harus dipertahankan.");
    Check(ffmpegRestored[0].Status == DownloadStatus.Gagal, "Stream FFmpeg yang belum selesai harus dipulihkan sebagai Gagal.");

    var youtubeSource = new[]
    {
        new DownloadItem
        {
            Name = "Contoh YouTube",
            Url = "https://www.youtube.com/watch?v=example",
            DirectoryPath = tempDirectory,
            CompletedBytes = 4321,
            Status = DownloadStatus.Mengunduh,
            EngineKind = DownloadEngineKind.YtDlp,
            YouTubeFormatProfile = YouTubeFormatProfiles.P720
        }
    };

    await store.SaveAsync(youtubeSource);
    var youtubeRestored = await store.LoadAsync(tempDirectory);
    Check(youtubeRestored.Count == 1, "Riwayat yt-dlp harus dapat dimuat.");
    Check(youtubeRestored[0].EngineKind == DownloadEngineKind.YtDlp, "Engine yt-dlp harus dipertahankan.");
    Check(youtubeRestored[0].Status == DownloadStatus.Gagal, "yt-dlp yang terputus harus dipulihkan sebagai Gagal.");
    Check(youtubeRestored[0].YouTubeFormatProfile == YouTubeFormatProfiles.P720,
        "Profil kualitas YouTube harus dipertahankan setelah reload.");
    Check(youtubeRestored[0].ErrorMessage?.Contains(".part", StringComparison.OrdinalIgnoreCase) == true,
        "Riwayat yt-dlp terputus harus menjelaskan resume .part.");
}
finally
{
    try
    {
        Directory.Delete(tempDirectory, recursive: true);
    }
    catch
    {
    }
}

if (failures.Count == 0)
{
    Console.WriteLine("Download Aja smoke tests: PASS");
    return 0;
}

Console.Error.WriteLine($"Download Aja smoke tests: FAIL ({failures.Count})");
foreach (var failure in failures)
    Console.Error.WriteLine($"- {failure}");

return 1;
