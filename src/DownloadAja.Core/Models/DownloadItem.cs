using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DownloadAja.Core.Models;

public sealed class DownloadItem : INotifyPropertyChanged
{
    private string _name = "";
    private string _url = "";
    private string _directoryPath = "";
    private string? _filePath;
    private string? _gid;
    private string? _errorMessage;
    private long _totalBytes;
    private long _completedBytes;
    private long _speedBytesPerSecond;
    private DownloadStatus _status = DownloadStatus.Menunggu;
    private DownloadEngineKind _engineKind = DownloadEngineKind.Aria2;
    private string _youtubeFormatProfile = "best";

    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => _name;
        set
        {
            if (!Set(ref _name, value)) return;
            OnPropertyChanged(nameof(Category));
        }
    }

    public string Url
    {
        get => _url;
        set
        {
            if (!Set(ref _url, value)) return;
            OnPropertyChanged(nameof(Category));
        }
    }

    public string DirectoryPath { get => _directoryPath; set => Set(ref _directoryPath, value); }
    public string? FilePath { get => _filePath; set => Set(ref _filePath, value); }
    public string? Gid { get => _gid; set => Set(ref _gid, value); }
    public string? ErrorMessage { get => _errorMessage; set => Set(ref _errorMessage, value); }
    public string YouTubeFormatProfile { get => _youtubeFormatProfile; set => Set(ref _youtubeFormatProfile, value); }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public long TotalBytes
    {
        get => _totalBytes;
        set
        {
            if (!Set(ref _totalBytes, value)) return;
            RaiseProgressProperties();
        }
    }

    public long CompletedBytes
    {
        get => _completedBytes;
        set
        {
            if (!Set(ref _completedBytes, value)) return;
            RaiseProgressProperties();
        }
    }

    public long SpeedBytesPerSecond
    {
        get => _speedBytesPerSecond;
        set
        {
            if (!Set(ref _speedBytesPerSecond, value)) return;
            OnPropertyChanged(nameof(SpeedText));
            OnPropertyChanged(nameof(EtaText));
        }
    }

    public DownloadStatus Status { get => _status; set => Set(ref _status, value); }

    public DownloadEngineKind EngineKind
    {
        get => _engineKind;
        set
        {
            if (!Set(ref _engineKind, value)) return;
            OnPropertyChanged(nameof(Category));
        }
    }

    public string Category => EngineKind == DownloadEngineKind.YtDlp
        ? "Video"
        : ClassifyCategory(Name, Url);
    public double ProgressPercent => TotalBytes <= 0 ? 0 : Math.Clamp(CompletedBytes * 100d / TotalBytes, 0, 100);
    public string ProgressText => $"{ProgressPercent:F0}%";
    public string SizeText => TotalBytes <= 0 ? "—" : $"{FormatBytes(CompletedBytes)} / {FormatBytes(TotalBytes)}";
    public string SpeedText => SpeedBytesPerSecond <= 0 ? "—" : $"{FormatBytes(SpeedBytesPerSecond)}/s";

    public string EtaText
    {
        get
        {
            if (SpeedBytesPerSecond <= 0 || TotalBytes <= CompletedBytes) return "—";
            var seconds = (TotalBytes - CompletedBytes) / (double)SpeedBytesPerSecond;
            var eta = TimeSpan.FromSeconds(Math.Min(seconds, TimeSpan.FromDays(99).TotalSeconds));
            return eta.TotalHours >= 1
                ? $"{(int)eta.TotalHours:00}:{eta.Minutes:00}:{eta.Seconds:00}"
                : $"{eta.Minutes:00}:{eta.Seconds:00}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void RaiseProgressProperties()
    {
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(EtaText));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static string ClassifyCategory(string name, string url)
    {
        var source = !string.IsNullOrWhiteSpace(name) ? name : url;
        var extension = Path.GetExtension(source.Split('?', '#')[0]).ToLowerInvariant();

        return extension switch
        {
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".webm" or ".m4v" or ".ts" => "Video",
            ".mp3" or ".wav" or ".flac" or ".aac" or ".m4a" or ".ogg" or ".opus" or ".wma" => "Audio",
            ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt" or ".csv" or ".rtf" or ".odt" => "Dokumen",
            ".exe" or ".msi" or ".msix" or ".appx" or ".apk" or ".deb" or ".rpm" => "Program",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" or ".xz" or ".iso" => "Arsip",
            _ => "Lainnya"
        };
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:F0} {units[unit]}" : $"{value:F1} {units[unit]}";
    }
}
