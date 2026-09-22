using System.Collections.ObjectModel;
using DownloadAja.Core.Models;

namespace DownloadAja.Desktop.ViewModels;

public sealed class MainViewModel
{
    public ObservableCollection<DownloadItem> Downloads { get; } = new();

    public string StatusText => Downloads.Count == 0
        ? "Siap — belum ada download"
        : $"{Downloads.Count} item";

    public void AddPlaceholder(string url)
    {
        var name = TryGetFileName(url);
        Downloads.Add(new DownloadItem
        {
            Url = url,
            Name = name,
            Status = DownloadStatus.Menunggu
        });
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
}
