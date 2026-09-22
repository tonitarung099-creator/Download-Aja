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
    private readonly DownloadHistoryStore _historyStore;
    private readonly DownloadSettingsStore _settingsStore;
    private DateTimeOffset _lastAutoSave = DateTimeOffset.MinValue;
    private string _searchText = "";
    private string _filterKey = "Semua";
    private DownloadSettings _settings = new();

    public ObservableCollection<DownloadItem> Downloads { get; } = new();
    public ICollectionView DownloadsView { get; }
    public string DownloadDirectory { get; } = GetDefaultDownloadDirectory();

    public int ConnectionsPerDownload => _settings.ConnectionsPerDownload;
    public long SpeedLimitBytesPerSecond => _settings.SpeedLimitBytesPerSecond;

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

    public async Task UpdateSettingsAsync(int connectionsPerDownload, long speedLimitBytesPerSecond, CancellationToken ct = default)
    {
        _settings = new DownloadSettings
        {
            ConnectionsPerDownload = connectionsPerDownload,
            SpeedLimitBytesPerSecond = speedLimitBytesPerSecond
        }.Normalize();

        await _settingsStore.SaveAsync(_settings, ct);
    }

    public Task<DownloadItem> AddAndStartAsync(string url, CancellationToken ct = default)
        => AddAsync(url, DownloadDirectory, startImmediately: true, ct);

    public async Task<DownloadItem> AddAsync(
        string url,
        string directoryPath,
        bool startImmediately,
        CancellationToken ct = default)
    {
        var item = new DownloadItem
        {
            Url = url,
            Name = TryGetFileName(url),
            DirectoryPath = string.IsNullOrWhiteSpace(directoryPath) ? DownloadDirectory : directoryPath,
            Status = DownloadStatus.Menunggu
        };

        Downloads.Insert(0, item);

        try
        {
            if (startImmediately)
                await StartAsync(item, ct);
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

    public async Task StartAsync(DownloadItem item, CancellationToken ct = default)
    {
        if (item.Status == DownloadStatus.Selesai)
            return;

        var client = await _engine.EnsureStartedAsync(ct);
        item.ErrorMessage = null;

        if (!string.IsNullOrWhiteSpace(item.Gid) && item.Status == DownloadStatus.Dijeda)
        {
            await client.UnpauseAsync(item.Gid, ct);
            item.Status = DownloadStatus.Mengunduh;
            DownloadsView.Refresh();
            await SaveStateAsync(ct);
            return;
        }

        if (item.Status == DownloadStatus.Mengunduh)
            return;

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
            ct: ct);

        item.Status = DownloadStatus.Mengunduh;
        DownloadsView.Refresh();
        await SaveStateAsync(ct);
    }

    public async Task PauseAsync(DownloadItem item, CancellationToken ct = default)
    {
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

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        if (!_engine.IsRunning || _engine.Client is null) return;

        foreach (var item in Downloads.ToArray())
        {
            if (string.IsNullOrWhiteSpace(item.Gid) ||
                item.Status is DownloadStatus.Selesai or DownloadStatus.Dibatalkan)
                continue;

            try
            {
                var status = await _engine.Client.TellStatusAsync(item.Gid, ct);
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
