using System.Collections.ObjectModel;
using System.IO;
using DownloadAja.Core.Models;

namespace DownloadAja.Desktop.ViewModels;

public sealed class MainViewModel
{
    public ObservableCollection<DownloadItem> Downloads { get; } = new();

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
