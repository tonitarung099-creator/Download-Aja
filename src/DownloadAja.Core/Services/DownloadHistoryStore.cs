using System.Text.Json;
using DownloadAja.Core.Models;

namespace DownloadAja.Core.Services;

public sealed class DownloadHistoryStore
{
    private readonly string _statePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public DownloadHistoryStore(string statePath)
    {
        _statePath = statePath;
    }

    public async Task<IReadOnlyList<DownloadItem>> LoadAsync(string defaultDirectory, CancellationToken ct = default)
    {
        if (!File.Exists(_statePath))
            return [];

        try
        {
            await using var stream = File.OpenRead(_statePath);
            var records = await JsonSerializer.DeserializeAsync<List<DownloadRecord>>(stream, _jsonOptions, ct) ?? [];

            return records
                .Where(x => !string.IsNullOrWhiteSpace(x.Url))
                .Select(x => ToItem(x, defaultDirectory))
                .OrderByDescending(x => x.CreatedAt)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            TryBackupCorruptState();
            return [];
        }
    }

    public async Task SaveAsync(IEnumerable<DownloadItem> items, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(_statePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var records = items.Select(DownloadRecord.FromItem).ToArray();
        var tempPath = _statePath + ".tmp";

        await using (var stream = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            4096,
            useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, records, _jsonOptions, ct);
            await stream.FlushAsync(ct);
        }

        File.Move(tempPath, _statePath, overwrite: true);
    }

    private static DownloadItem ToItem(DownloadRecord record, string defaultDirectory)
    {
        var processEngineInterrupted =
            record.EngineKind is DownloadEngineKind.Ffmpeg or DownloadEngineKind.YtDlp
            && record.Status != DownloadStatus.Selesai
            && record.Status != DownloadStatus.Dibatalkan;

        var status = processEngineInterrupted
            ? DownloadStatus.Gagal
            : record.Status switch
            {
                DownloadStatus.Selesai => DownloadStatus.Selesai,
                DownloadStatus.Dibatalkan => DownloadStatus.Dibatalkan,
                _ when record.CompletedBytes > 0 => DownloadStatus.Dijeda,
                _ => DownloadStatus.Menunggu
            };

        return new DownloadItem
        {
            Id = string.IsNullOrWhiteSpace(record.Id) ? Guid.NewGuid().ToString("N") : record.Id,
            Url = record.Url,
            Name = string.IsNullOrWhiteSpace(record.Name) ? "download" : record.Name,
            DirectoryPath = string.IsNullOrWhiteSpace(record.DirectoryPath) ? defaultDirectory : record.DirectoryPath,
            FilePath = record.FilePath,
            TotalBytes = Math.Max(0, record.TotalBytes),
            CompletedBytes = Math.Max(0, record.CompletedBytes),
            SpeedBytesPerSecond = 0,
            Status = status,
            ErrorMessage = status == DownloadStatus.Gagal
                ? record.EngineKind switch
                {
                    DownloadEngineKind.Ffmpeg => "Stream belum selesai. Kirim ulang media dari browser untuk memulai ulang.",
                    DownloadEngineKind.YtDlp => "Unduhan YouTube terputus. Klik Mulai/Coba Lagi; yt-dlp akan melanjutkan file .part bila tersedia.",
                    _ => record.ErrorMessage
                }
                : record.ErrorMessage,
            CreatedAt = record.CreatedAt == default ? DateTimeOffset.Now : record.CreatedAt,
            EngineKind = record.EngineKind,
            Gid = null
        };
    }

    private void TryBackupCorruptState()
    {
        try
        {
            if (!File.Exists(_statePath)) return;
            var backup = _statePath + $".corrupt-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
            File.Move(_statePath, backup, overwrite: true);
        }
        catch
        {
            // History rusak tidak boleh mencegah aplikasi terbuka.
        }
    }

    private sealed class DownloadRecord
    {
        public string Id { get; init; } = "";
        public string Url { get; init; } = "";
        public string Name { get; init; } = "";
        public string DirectoryPath { get; init; } = "";
        public string? FilePath { get; init; }
        public long TotalBytes { get; init; }
        public long CompletedBytes { get; init; }
        public DownloadStatus Status { get; init; }
        public DownloadEngineKind EngineKind { get; init; } = DownloadEngineKind.Aria2;
        public string? ErrorMessage { get; init; }
        public DateTimeOffset CreatedAt { get; init; }

        public static DownloadRecord FromItem(DownloadItem item) => new()
        {
            Id = item.Id,
            Url = item.Url,
            Name = item.Name,
            DirectoryPath = item.DirectoryPath,
            FilePath = item.FilePath,
            TotalBytes = item.TotalBytes,
            CompletedBytes = item.CompletedBytes,
            Status = item.Status,
            EngineKind = item.EngineKind,
            ErrorMessage = item.ErrorMessage,
            CreatedAt = item.CreatedAt
        };
    }
}
