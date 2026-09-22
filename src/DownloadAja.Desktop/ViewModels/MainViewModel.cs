using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using DownloadAja.Core.Models;
using DownloadAja.Core.Services;

namespace DownloadAja.Desktop.ViewModels;

public sealed class MainViewModel : IAsyncDisposable
{
    private readonly Aria2EngineHost _engine = new();

    public ObservableCollection<DownloadItem> Downloads { get; } = new();

    public string DownloadDirectory { get; } = GetDefaultDownloadDirectory();

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(DownloadDirectory);
        _ = await _engine.EnsureStartedAsync(ct);
    }

    public async Task<DownloadItem> AddAndStartAsync(string url, CancellationToken ct = default)
    {
        var item = new DownloadItem
        {
            Url = url,
            Name = TryGetFileName(url),
            SavePath = DownloadDirectory,
            Status = DownloadStatus.Menunggu
        };

        Downloads.Add(item);

        try
        {
            await StartAsync(item, ct);
            return item;
        }
        catch
        {
            item.Status = DownloadStatus.Gagal;
            throw;
        }
    }

    public async Task StartAsync(DownloadItem item, CancellationToken ct = default)
    {
        var client = await _engine.EnsureStartedAsync(ct);
        item.ErrorMessage = null;

        if (!string.IsNullOrWhiteSpace(item.Gid) && item.Status == DownloadStatus.Dijeda)
        {
            await client.UnpauseAsync(item.Gid, ct);
            item.Status = DownloadStatus.Mengunduh;
            return;
        }

        if (item.Status == DownloadStatus.Mengunduh)
            return;

        item.Gid = await client.AddUriAsync(item.Url, item.SavePath, connections: 8, ct);
        item.Status = DownloadStatus.Mengunduh;
    }

    public async Task PauseAsync(DownloadItem item, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(item.Gid) || item.Status != DownloadStatus.Mengunduh)
            return;

        var client = await _engine.EnsureStartedAsync(ct);
        await client.PauseAsync(item.Gid, ct);
        item.Status = DownloadStatus.Dijeda;
        item.SpeedBytesPerSecond = 0;
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
    }

    public long TotalSpeed => Downloads
        .Where(x => x.Status == DownloadStatus.Mengunduh)
        .Sum(x => x.SpeedBytesPerSecond);

    public int ActiveCount => Downloads.Count(x => x.Status == DownloadStatus.Mengunduh);

    public async ValueTask DisposeAsync()
        => await _engine.DisposeAsync();

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
                    item.SavePath = path;
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
