using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Data;
using DownloadAja.Core.Models;
using DownloadAja.Core.Services;

namespace DownloadAja.Desktop.ViewModels;

public sealed class MainViewModel : IAsyncDisposable
{
    private readonly Aria2EngineHost _engine = new();
    private readonly FfmpegMediaDownloader _ffmpeg = new();
    private readonly Dictionary<string, FfmpegMediaSession> _ffmpegSessions = new();
    private readonly YtDlpDownloader _ytDlp = new();
    private readonly Dictionary<string, YtDlpSession> _ytDlpSessions = new();
    private readonly DownloadHistoryStore _historyStore;
    private readonly DownloadSettingsStore _settingsStore;
    private DateTimeOffset _lastAutoSave = DateTimeOffset.MinValue;
    private string _searchText = "";
    private string _filterKey = "Semua";
    private DownloadSettings _settings = new();
    private bool _queueAutoRun;

    public ObservableCollection<DownloadItem> Downloads { get; } = new();
    public ICollectionView DownloadsView { get; }
    public string DownloadDirectory { get; } = GetDefaultDownloadDirectory();

    public int ConnectionsPerDownload => _settings.ConnectionsPerDownload;
    public long SpeedLimitBytesPerSecond => _settings.SpeedLimitBytesPerSecond;
    public int MaxSimultaneousDownloads => _settings.MaxSimultaneousDownloads;
    public bool ClipboardMonitoringEnabled => _settings.ClipboardMonitoringEnabled;
    public bool QueueRunning => _queueAutoRun;
    public bool SchedulerEnabled => _settings.SchedulerEnabled;
    public DateTimeOffset? ScheduledQueueStartAt => _settings.ScheduledQueueStartAt;
    public int QueuedCount => Downloads.Count(x => x.Status == DownloadStatus.Menunggu && string.IsNullOrWhiteSpace(x.Gid));

    public MainViewModel()
    {
        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
        _historyStore = new DownloadHistoryStore(Path.Combine(dataDirectory, "downloads.json"));
        _settingsStore = new DownloadSettingsStore(Path.Combine(dataDirectory, "settings.json"));

        DownloadsView = CollectionViewSource.GetDefaultView(Downloads);
        DownloadsView.Filter = MatchesFilter;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(DownloadDirectory);

        _settings = await _settingsStore.LoadAsync(ct);

        var restored = await _historyStore.LoadAsync(DownloadDirectory, ct);
        foreach (var item in restored)
            Downloads.Add(item);

        DownloadsView.Refresh();
        _ = await _engine.EnsureStartedAsync(ct);
    }

    public void SetSearchText(string? value)
    {
        _searchText = value?.Trim() ?? "";
        DownloadsView.Refresh();
    }

    public void SetFilter(string? filterKey)
    {
        _filterKey = string.IsNullOrWhiteSpace(filterKey) ? "Semua" : filterKey;
        DownloadsView.Refresh();
    }

    public async Task UpdateSettingsAsync(
        int connectionsPerDownload,
        long speedLimitBytesPerSecond,
        int maxSimultaneousDownloads,
        bool clipboardMonitoringEnabled,
        CancellationToken ct = default)
    {
        _settings.ConnectionsPerDownload = connectionsPerDownload;
        _settings.SpeedLimitBytesPerSecond = speedLimitBytesPerSecond;
        _settings.MaxSimultaneousDownloads = maxSimultaneousDownloads;
        _settings.ClipboardMonitoringEnabled = clipboardMonitoringEnabled;
        _settings.Normalize();

        await _settingsStore.SaveAsync(_settings, ct);
    }

    public async Task ConfigureSchedulerAsync(DateTimeOffset? scheduledAt, CancellationToken ct = default)
    {
        _settings.SchedulerEnabled = scheduledAt.HasValue;
        _settings.ScheduledQueueStartAt = scheduledAt;
        _settings.Normalize();
        await _settingsStore.SaveAsync(_settings, ct);
    }

    public Task<DownloadItem> AddAndStartAsync(
        string url,
        DownloadRequestContext? requestContext = null,
        CancellationToken ct = default)
        => AddAsync(
            url,
            DownloadDirectory,
            startImmediately: true,
            outputFileName: null,
            requestContext: requestContext,
            ct: ct);

    public async Task<DownloadItem> AddAsync(
        string url,
        string directoryPath,
        bool startImmediately,
        string? outputFileName = null,
        string? youtubeFormatProfile = null,
        DownloadRequestContext? requestContext = null,
        CancellationToken ct = default)
    {
        var isStream = IsStreamRequest(requestContext);
        var isYouTubeHost = !isStream && YouTubeUrlClassifier.IsYouTubeHost(url);

        if (isYouTubeHost && !YouTubeUrlClassifier.IsVideoUrl(url))
            throw new InvalidOperationException(
                "URL YouTube bukan URL video. Gunakan URL watch, shorts, live, embed, clip, atau youtu.be.");

        var isYouTube = isYouTubeHost && YouTubeUrlClassifier.IsVideoUrl(url);
        var normalizedYouTubeProfile = isYouTube
            ? YouTubeFormatProfiles.Normalize(requestContext?.FormatProfile ?? youtubeFormatProfile)
            : YouTubeFormatProfiles.Best;
        var targetDirectory = string.IsNullOrWhiteSpace(directoryPath) ? DownloadDirectory : directoryPath;
        var normalizedOutputName = isStream || isYouTube ? null : NormalizeOutputFileName(outputFileName);
        var youtubeOutputName = isYouTube ? NormalizeYouTubeOutputName(outputFileName) : null;
        var customOutputPath = normalizedOutputName is null
            ? null
            : GetUniquePath(targetDirectory, normalizedOutputName);

        var item = new DownloadItem
        {
            Url = url,
            Name = isStream
                ? BuildStreamFileName(requestContext?.SuggestedName)
                : isYouTube
                    ? youtubeOutputName ?? "YouTube video"
                    : customOutputPath is null
                        ? TryGetFileName(url)
                        : Path.GetFileName(customOutputPath),
            DirectoryPath = targetDirectory,
            FilePath = isYouTube ? null : customOutputPath,
            Status = DownloadStatus.Menunggu,
            EngineKind = isStream
                ? DownloadEngineKind.Ffmpeg
                : isYouTube
                    ? DownloadEngineKind.YtDlp
                    : DownloadEngineKind.Aria2,
            YouTubeFormatProfile = normalizedYouTubeProfile
        };

        Downloads.Insert(0, item);

        try
        {
            if (startImmediately)
                await StartAsync(item, requestContext, ct);
            else
                await SaveStateAsync(ct);

            DownloadsView.Refresh();
            return item;
        }
        catch
        {
            item.Status = DownloadStatus.Gagal;
            await SaveStateAsync(ct);
            throw;
        }
    }

    public Task StartAsync(DownloadItem item, CancellationToken ct = default)
        => StartAsync(item, requestContext: null, ct);

    public async Task StartAsync(
        DownloadItem item,
        DownloadRequestContext? requestContext,
        CancellationToken ct = default)
    {
        if (item.Status == DownloadStatus.Selesai)
            return;

        item.ErrorMessage = null;

        if (item.EngineKind == DownloadEngineKind.Ffmpeg)
        {
            StartFfmpeg(item, requestContext);
            DownloadsView.Refresh();
            await SaveStateAsync(ct);
            return;
        }

        if (item.EngineKind == DownloadEngineKind.YtDlp)
        {
            StartYtDlp(item, requestContext);
            DownloadsView.Refresh();
            await SaveStateAsync(ct);
            return;
        }

        var client = await _engine.EnsureStartedAsync(ct);

        if (!string.IsNullOrWhiteSpace(item.Gid))
        {
            if (item.Status == DownloadStatus.Dijeda)
            {
                await client.UnpauseAsync(item.Gid, ct);
                item.Status = DownloadStatus.Mengunduh;
                DownloadsView.Refresh();
                await SaveStateAsync(ct);
                return;
            }

            if (item.Status is DownloadStatus.Mengunduh or DownloadStatus.Menunggu)
                return;

            // GID dari hasil error/removed tidak dipakai untuk sesi baru.
            item.Gid = null;
        }

        var directory = string.IsNullOrWhiteSpace(item.DirectoryPath)
            ? DownloadDirectory
            : item.DirectoryPath;

        Directory.CreateDirectory(directory);

        var existingFileName = string.IsNullOrWhiteSpace(item.FilePath)
            ? null
            : Path.GetFileName(item.FilePath);

        item.Gid = await client.AddUriAsync(
            item.Url,
            directory,
            connections: _settings.ConnectionsPerDownload,
            outputFileName: existingFileName,
            speedLimitBytesPerSecond: _settings.SpeedLimitBytesPerSecond,
            referer: requestContext?.Referer,
            userAgent: requestContext?.UserAgent,
            cookieHeader: requestContext?.CookieHeader,
            autoFileRenaming: existingFileName is null,
            ct: ct);

        item.Status = DownloadStatus.Mengunduh;
        DownloadsView.Refresh();
        await SaveStateAsync(ct);
    }

    public async Task<int> StartQueuedAsync(CancellationToken ct = default)
    {
        _queueAutoRun = true;
        var started = await FillQueueSlotsAsync(ct);

        if (QueuedCount == 0 && ActiveCount == 0)
            _queueAutoRun = false;

        DownloadsView.Refresh();
        await SaveStateAsync(ct);
        return started;
    }

    public void StopQueue()
    {
        _queueAutoRun = false;
    }

    private async Task<int> FillQueueSlotsAsync(CancellationToken ct)
    {
        var availableSlots = Math.Max(0, _settings.MaxSimultaneousDownloads - ActiveCount);
        if (availableSlots == 0)
            return 0;

        var queued = Downloads
            .Where(x => x.Status == DownloadStatus.Menunggu && string.IsNullOrWhiteSpace(x.Gid))
            .OrderBy(x => x.CreatedAt)
            .Take(availableSlots)
            .ToArray();

        var started = 0;
        foreach (var item in queued)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await StartAsync(item, ct);
                started++;
            }
            catch (Exception ex)
            {
                item.Status = DownloadStatus.Gagal;
                item.ErrorMessage = ex.Message;
            }
        }

        if (QueuedCount == 0 && ActiveCount == 0)
            _queueAutoRun = false;

        return started;
    }

    public async Task<int?> TryRunScheduledQueueAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        if (!_settings.SchedulerEnabled || !_settings.ScheduledQueueStartAt.HasValue)
            return null;

        if (now < _settings.ScheduledQueueStartAt.Value)
            return null;

        _settings.SchedulerEnabled = false;
        _settings.ScheduledQueueStartAt = null;
        await _settingsStore.SaveAsync(_settings, ct);

        return await StartQueuedAsync(ct);
    }

    public async Task PauseAsync(DownloadItem item, CancellationToken ct = default)
    {
        if (item.EngineKind == DownloadEngineKind.Ffmpeg)
            throw new InvalidOperationException("Stream HLS/DASH belum mendukung jeda. Gunakan Hentikan lalu kirim ulang stream dari browser.");

        if (item.EngineKind == DownloadEngineKind.YtDlp)
            throw new InvalidOperationException("Unduhan YouTube belum mendukung jeda langsung. Gunakan Hentikan lalu Mulai/Coba Lagi; file .part akan dilanjutkan bila tersedia.");

        if (string.IsNullOrWhiteSpace(item.Gid) || item.Status != DownloadStatus.Mengunduh)
            return;

        var client = await _engine.EnsureStartedAsync(ct);
        await client.PauseAsync(item.Gid, ct);
        item.Status = DownloadStatus.Dijeda;
        item.SpeedBytesPerSecond = 0;
        DownloadsView.Refresh();
        await SaveStateAsync(ct);
    }

    public async Task StopAsync(DownloadItem item, CancellationToken ct = default)
    {
        if (item.EngineKind == DownloadEngineKind.Ffmpeg)
        {
            if (_ffmpegSessions.Remove(item.Id, out var mediaSession))
                await mediaSession.DisposeAsync();

            item.SpeedBytesPerSecond = 0;
            item.Status = DownloadStatus.Dibatalkan;
            DownloadsView.Refresh();
            await SaveStateAsync(ct);
            return;
        }

        if (item.EngineKind == DownloadEngineKind.YtDlp)
        {
            if (_ytDlpSessions.Remove(item.Id, out var youtubeSession))
                await youtubeSession.DisposeAsync();

            item.SpeedBytesPerSecond = 0;
            item.Status = DownloadStatus.Dibatalkan;
            item.ErrorMessage = "Dihentikan. Klik Mulai/Coba Lagi untuk melanjutkan file .part bila tersedia.";
            DownloadsView.Refresh();
            await SaveStateAsync(ct);
            return;
        }

        if (!string.IsNullOrWhiteSpace(item.Gid))
        {
            var client = await _engine.EnsureStartedAsync(ct);
            try
            {
                await client.ForceRemoveAsync(item.Gid, ct);
            }
            catch
            {
                // Item mungkin sudah selesai/hilang dari engine.
            }
        }

        item.Gid = null;
        item.SpeedBytesPerSecond = 0;
        item.Status = DownloadStatus.Dibatalkan;
        DownloadsView.Refresh();
        await SaveStateAsync(ct);
    }

    public async Task RemoveAsync(DownloadItem item, CancellationToken ct = default)
    {
        if (_ffmpegSessions.Remove(item.Id, out var mediaSession))
            await mediaSession.DisposeAsync();

        if (_ytDlpSessions.Remove(item.Id, out var youtubeSession))
            await youtubeSession.DisposeAsync();

        if (!string.IsNullOrWhiteSpace(item.Gid) &&
            item.Status is DownloadStatus.Mengunduh or DownloadStatus.Dijeda)
        {
            var client = await _engine.EnsureStartedAsync(ct);
            try
            {
                await client.ForceRemoveAsync(item.Gid, ct);
            }
            catch
            {
                // Hapus dari daftar tetap dilanjutkan.
            }
        }

        Downloads.Remove(item);
        DownloadsView.Refresh();
        await SaveStateAsync(ct);
    }

    public async Task RemoveAndDeleteFileAsync(DownloadItem item, CancellationToken ct = default)
    {
        var filePath = item.FilePath;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            throw new FileNotFoundException(
                "File hasil download tidak ditemukan. Gunakan Hapus untuk menghapus item dari daftar saja.",
                filePath);

        if (item.Status is DownloadStatus.Mengunduh or DownloadStatus.Dijeda)
            await StopAsync(item, ct);

        try
        {
            File.Delete(filePath);

            if (item.EngineKind == DownloadEngineKind.Aria2)
            {
                var ariaControl = filePath + ".aria2";
                if (File.Exists(ariaControl))
                    File.Delete(ariaControl);
            }
        }
        catch (Exception ex)
        {
            throw new IOException($"File tidak dapat dihapus: {ex.Message}", ex);
        }

        Downloads.Remove(item);
        DownloadsView.Refresh();
        await SaveStateAsync(ct);
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        if (_engine.IsRunning && _engine.Client is not null)
        foreach (var item in Downloads.ToArray())
        {
            if (string.IsNullOrWhiteSpace(item.Gid) ||
                item.Status is DownloadStatus.Selesai or DownloadStatus.Dibatalkan)
                continue;

            try
            {
                var status = await _engine.Client!.TellStatusAsync(item.Gid, ct);
                ApplyStatus(item, status);
            }
            catch (Exception ex)
            {
                item.ErrorMessage = ex.Message;
                if (item.Status == DownloadStatus.Mengunduh)
                    item.Status = DownloadStatus.Gagal;
                item.SpeedBytesPerSecond = 0;
            }
        }

        foreach (var pair in _ffmpegSessions.ToArray())
        {
            var item = Downloads.FirstOrDefault(x => x.Id == pair.Key);
            var session = pair.Value;

            if (item is null)
            {
                _ffmpegSessions.Remove(pair.Key);
                await session.DisposeAsync();
                continue;
            }

            item.CompletedBytes = session.BytesWritten;
            item.SpeedBytesPerSecond = session.SpeedBytesPerSecond;

            if (!session.HasExited)
                continue;

            item.SpeedBytesPerSecond = 0;

            if (session.ExitCode == 0 && File.Exists(session.OutputPath))
            {
                item.FilePath = session.OutputPath;
                item.Name = Path.GetFileName(session.OutputPath);
                item.TotalBytes = item.CompletedBytes;
                item.Status = DownloadStatus.Selesai;
                item.ErrorMessage = null;
            }
            else if (item.Status != DownloadStatus.Dibatalkan)
            {
                item.Status = DownloadStatus.Gagal;
                item.ErrorMessage = string.IsNullOrWhiteSpace(session.ErrorText)
                    ? "FFmpeg gagal menyelesaikan stream."
                    : session.ErrorText;
            }

            _ffmpegSessions.Remove(pair.Key);
            await session.DisposeAsync();
        }

        foreach (var pair in _ytDlpSessions.ToArray())
        {
            var item = Downloads.FirstOrDefault(x => x.Id == pair.Key);
            var session = pair.Value;

            if (item is null)
            {
                _ytDlpSessions.Remove(pair.Key);
                await session.DisposeAsync();
                continue;
            }

            if (!string.IsNullOrWhiteSpace(session.Title) &&
                string.Equals(item.Name, "YouTube video", StringComparison.OrdinalIgnoreCase))
            {
                item.Name = session.Title;
            }

            item.TotalBytes = Math.Max(item.TotalBytes, session.TotalBytes);
            item.CompletedBytes = Math.Max(item.CompletedBytes, session.DownloadedBytes);
            item.SpeedBytesPerSecond = session.SpeedBytesPerSecond;

            if (!session.HasExited)
                continue;

            item.SpeedBytesPerSecond = 0;
            var finalPath = session.FinalPath;

            if (session.ExitCode == 0 &&
                !string.IsNullOrWhiteSpace(finalPath) &&
                File.Exists(finalPath))
            {
                var length = new FileInfo(finalPath).Length;
                item.FilePath = finalPath;
                item.DirectoryPath = Path.GetDirectoryName(finalPath) ?? item.DirectoryPath;
                item.Name = Path.GetFileName(finalPath);
                item.TotalBytes = length;
                item.CompletedBytes = length;
                item.Status = DownloadStatus.Selesai;
                item.ErrorMessage = null;
            }
            else if (item.Status != DownloadStatus.Dibatalkan)
            {
                item.Status = DownloadStatus.Gagal;
                item.ErrorMessage = BuildYtDlpErrorMessage(session.ErrorText);
            }

            _ytDlpSessions.Remove(pair.Key);
            await session.DisposeAsync();
        }

        if (_queueAutoRun)
            await FillQueueSlotsAsync(ct);

        DownloadsView.Refresh();

        if (DateTimeOffset.Now - _lastAutoSave >= TimeSpan.FromSeconds(5))
            await SaveStateAsync(ct);
    }

    public long TotalSpeed => Downloads
        .Where(x => x.Status == DownloadStatus.Mengunduh)
        .Sum(x => x.SpeedBytesPerSecond);

    public int ActiveCount => Downloads.Count(x => x.Status == DownloadStatus.Mengunduh);

    public async Task SaveStateAsync(CancellationToken ct = default)
    {
        await _historyStore.SaveAsync(Downloads, ct);
        _lastAutoSave = DateTimeOffset.Now;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await SaveStateAsync();
        }
        finally
        {
            foreach (var session in _ffmpegSessions.Values.ToArray())
                await session.DisposeAsync();

            foreach (var session in _ytDlpSessions.Values.ToArray())
                await session.DisposeAsync();

            _ffmpegSessions.Clear();
            _ytDlpSessions.Clear();
            await _engine.DisposeAsync();
        }
    }

    private bool MatchesFilter(object value)
    {
        if (value is not DownloadItem item)
            return false;

        var filterMatch = _filterKey switch
        {
            "Mengunduh" => item.Status == DownloadStatus.Mengunduh,
            "Selesai" => item.Status == DownloadStatus.Selesai,
            "Belum selesai" => item.Status != DownloadStatus.Selesai,
            "Antrean" => item.Status == DownloadStatus.Menunggu,
            "Video" or "Audio" or "Dokumen" or "Program" or "Arsip" or "Lainnya" => item.Category == _filterKey,
            _ => true
        };

        if (!filterMatch)
            return false;

        if (string.IsNullOrWhiteSpace(_searchText))
            return true;

        return item.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || item.Url.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || item.Category.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyStatus(DownloadItem item, JsonElement status)
    {
        item.TotalBytes = ReadLong(status, "totalLength");
        item.CompletedBytes = ReadLong(status, "completedLength");
        item.SpeedBytesPerSecond = ReadLong(status, "downloadSpeed");

        if (status.TryGetProperty("files", out var files) &&
            files.ValueKind == JsonValueKind.Array &&
            files.GetArrayLength() > 0)
        {
            var first = files[0];
            if (first.TryGetProperty("path", out var pathElement))
            {
                var path = pathElement.GetString();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    item.FilePath = path;
                    var directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrWhiteSpace(directory))
                        item.DirectoryPath = directory;
                    item.Name = Path.GetFileName(path);
                }
            }
        }

        var ariaStatus = status.TryGetProperty("status", out var s) ? s.GetString() : null;
        item.Status = ariaStatus switch
        {
            "active" => DownloadStatus.Mengunduh,
            "waiting" => DownloadStatus.Menunggu,
            "paused" => DownloadStatus.Dijeda,
            "complete" => DownloadStatus.Selesai,
            "error" => DownloadStatus.Gagal,
            "removed" => DownloadStatus.Dibatalkan,
            _ => item.Status
        };

        if (item.Status == DownloadStatus.Selesai)
        {
            item.CompletedBytes = Math.Max(item.CompletedBytes, item.TotalBytes);
            item.SpeedBytesPerSecond = 0;
        }

        if (status.TryGetProperty("errorMessage", out var errorMessage))
            item.ErrorMessage = errorMessage.GetString();
    }

    private static long ReadLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)) return 0;
        return long.TryParse(value.GetString(), out var parsed) ? parsed : 0;
    }

    private void StartFfmpeg(DownloadItem item, DownloadRequestContext? requestContext)
    {
        if (_ffmpegSessions.ContainsKey(item.Id))
            return;

        var directory = string.IsNullOrWhiteSpace(item.DirectoryPath)
            ? DownloadDirectory
            : item.DirectoryPath;

        Directory.CreateDirectory(directory);

        var fileName = BuildStreamFileName(
            requestContext?.SuggestedName ?? item.Name);

        var outputPath = GetUniquePath(directory, fileName);

        item.EngineKind = DownloadEngineKind.Ffmpeg;
        item.FilePath = outputPath;
        item.Name = Path.GetFileName(outputPath);
        item.TotalBytes = 0;
        item.CompletedBytes = 0;
        item.SpeedBytesPerSecond = 0;
        item.Status = DownloadStatus.Mengunduh;

        try
        {
            _ffmpegSessions[item.Id] = _ffmpeg.Start(item.Url, outputPath, requestContext);
        }
        catch
        {
            item.Status = DownloadStatus.Gagal;
            throw;
        }
    }

    private static string BuildYtDlpErrorMessage(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "yt-dlp gagal menyelesaikan unduhan YouTube.";

        if (raw.Contains("Private video", StringComparison.OrdinalIgnoreCase))
            return "Video YouTube ini private dan tidak dapat diunduh tanpa akses yang sah.";

        if (raw.Contains("Sign in", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("login", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("cookies", StringComparison.OrdinalIgnoreCase))
            return "YouTube meminta login/verifikasi untuk video ini. Download Aja 0.5.0 hanya menangani video publik yang dapat diakses tanpa login.";

        if (raw.Contains("members-only", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("members only", StringComparison.OrdinalIgnoreCase))
            return "Video ini khusus member dan tidak didukung oleh mode YouTube publik.";

        if (raw.Contains("DRM", StringComparison.OrdinalIgnoreCase))
            return "Media ini terdeteksi memakai DRM dan tidak didukung.";

        if (raw.Contains("not available", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("Video unavailable", StringComparison.OrdinalIgnoreCase))
            return "Video YouTube tidak tersedia untuk URL/region ini.";

        var lines = raw
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .TakeLast(5);

        return string.Join(Environment.NewLine, lines);
    }

    private void StartYtDlp(DownloadItem item, DownloadRequestContext? requestContext)
    {
        if (_ytDlpSessions.ContainsKey(item.Id))
            return;

        var directory = string.IsNullOrWhiteSpace(item.DirectoryPath)
            ? DownloadDirectory
            : item.DirectoryPath;

        Directory.CreateDirectory(directory);

        item.EngineKind = DownloadEngineKind.YtDlp;
        item.FilePath = null;
        item.TotalBytes = 0;
        item.CompletedBytes = 0;
        item.SpeedBytesPerSecond = 0;
        item.Status = DownloadStatus.Mengunduh;
        item.ErrorMessage = null;

        var suggestedName = string.Equals(item.Name, "YouTube video", StringComparison.OrdinalIgnoreCase)
            ? null
            : item.Name;

        try
        {
            _ytDlpSessions[item.Id] = _ytDlp.Start(
                item.Url,
                directory,
                suggestedName,
                item.YouTubeFormatProfile,
                _settings.SpeedLimitBytesPerSecond,
                requestContext);
        }
        catch
        {
            item.Status = DownloadStatus.Gagal;
            throw;
        }
    }

    private static bool IsStreamRequest(DownloadRequestContext? context)
        => string.Equals(context?.MediaKind, "hls", StringComparison.OrdinalIgnoreCase)
            || string.Equals(context?.MediaKind, "dash", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeYouTubeOutputName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var baseName = Path.GetFileNameWithoutExtension(value.Trim());
        foreach (var invalid in Path.GetInvalidFileNameChars())
            baseName = baseName.Replace(invalid, '_');

        baseName = baseName.Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(baseName))
            return null;

        return baseName.Length > 120 ? baseName[..120] : baseName;
    }

    private static string BuildStreamFileName(string? suggestedName)
    {
        var baseName = Path.GetFileNameWithoutExtension(suggestedName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(baseName) ||
            baseName.Equals("master", StringComparison.OrdinalIgnoreCase) ||
            baseName.Equals("index", StringComparison.OrdinalIgnoreCase) ||
            baseName.Equals("playlist", StringComparison.OrdinalIgnoreCase))
        {
            baseName = $"media-{DateTime.Now:yyyyMMdd-HHmmss}";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
            baseName = baseName.Replace(invalid, '_');

        baseName = baseName.Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = $"media-{DateTime.Now:yyyyMMdd-HHmmss}";

        if (baseName.Length > 120)
            baseName = baseName[..120];

        return baseName + ".mkv";
    }

    private static string GetUniquePath(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
            return path;

        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);

        for (var i = 1; i < 10000; i++)
        {
            path = Path.Combine(directory, $"{name} ({i}){ext}");
            if (!File.Exists(path))
                return path;
        }

        return Path.Combine(directory, $"{name}-{Guid.NewGuid():N}{ext}");
    }

    private static string? NormalizeOutputFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var fileName = Path.GetFileName(value.Trim());
        if (string.IsNullOrWhiteSpace(fileName) || fileName is "." or "..")
            return null;

        foreach (var invalid in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(invalid, '_');

        fileName = fileName.Trim(' ', '.');
        return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
    }

    private static string TryGetFileName(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var name = Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath));
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }

        return "download";
    }

    private static string GetDefaultDownloadDirectory()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
            return Path.Combine(profile, "Downloads");

        return Path.Combine(AppContext.BaseDirectory, "downloads");
    }
}
