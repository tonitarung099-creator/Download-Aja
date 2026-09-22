using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DownloadAja.Core.Models;

public sealed class DownloadItem : INotifyPropertyChanged
{
    private string _name = "";
    private string _url = "";
    private string _savePath = "";
    private string? _gid;
    private string? _errorMessage;
    private long _totalBytes;
    private long _completedBytes;
    private long _speedBytesPerSecond;
    private DownloadStatus _status = DownloadStatus.Menunggu;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Url { get => _url; set => Set(ref _url, value); }
    public string SavePath { get => _savePath; set => Set(ref _savePath, value); }
    public string? Gid { get => _gid; set => Set(ref _gid, value); }
    public string? ErrorMessage { get => _errorMessage; set => Set(ref _errorMessage, value); }

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
