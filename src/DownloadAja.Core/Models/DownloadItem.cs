using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DownloadAja.Core.Models;

public sealed class DownloadItem : INotifyPropertyChanged
{
    private string _name = "";
    private string _url = "";
    private string _savePath = "";
    private long _totalBytes;
    private long _completedBytes;
    private long _speedBytesPerSecond;
    private DownloadStatus _status = DownloadStatus.Menunggu;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Url { get => _url; set => Set(ref _url, value); }
    public string SavePath { get => _savePath; set => Set(ref _savePath, value); }
    public long TotalBytes { get => _totalBytes; set { if (Set(ref _totalBytes, value)) OnPropertyChanged(nameof(ProgressPercent)); } }
    public long CompletedBytes { get => _completedBytes; set { if (Set(ref _completedBytes, value)) OnPropertyChanged(nameof(ProgressPercent)); } }
    public long SpeedBytesPerSecond { get => _speedBytesPerSecond; set => Set(ref _speedBytesPerSecond, value); }
    public DownloadStatus Status { get => _status; set => Set(ref _status, value); }

    public double ProgressPercent => TotalBytes <= 0 ? 0 : Math.Clamp(CompletedBytes * 100d / TotalBytes, 0, 100);

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
