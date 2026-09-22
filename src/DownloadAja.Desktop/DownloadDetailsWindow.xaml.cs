using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using DownloadAja.Core.Models;

namespace DownloadAja.Desktop;

public partial class DownloadDetailsWindow : Window
{
    private readonly DownloadItem _item;

    public DownloadDetailsWindow(DownloadItem item)
    {
        _item = item;
        InitializeComponent();
        RefreshValues();
    }

    private void RefreshValues()
    {
        NameText.Text = _item.Name;
        StatusText.Text = _item.Status.ToString();
        EngineText.Text = _item.EngineKind switch
        {
            DownloadEngineKind.Aria2 => "aria2 — HTTP/HTTPS",
            DownloadEngineKind.Ffmpeg => "FFmpeg — HLS/DASH",
            DownloadEngineKind.YtDlp => "yt-dlp — YouTube",
            _ => _item.EngineKind.ToString()
        };
        QualityText.Text = _item.QualityText;
        SizeText.Text = $"{_item.SizeText}  •  {_item.ProgressText}";
        SpeedEtaText.Text = $"{_item.SpeedText}  •  ETA {_item.EtaText}";
        UrlBox.Text = _item.Url;
        DirectoryBox.Text = _item.DirectoryPath;
        FileBox.Text = string.IsNullOrWhiteSpace(_item.FilePath) ? "—" : _item.FilePath;
        ErrorBox.Text = string.IsNullOrWhiteSpace(_item.ErrorMessage) ? "—" : _item.ErrorMessage;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Nama: {_item.Name}");
        builder.AppendLine($"Status: {_item.Status}");
        builder.AppendLine($"Engine: {_item.EngineKind}");
        builder.AppendLine($"Kualitas: {_item.QualityText}");
        builder.AppendLine($"Ukuran: {_item.SizeText}");
        builder.AppendLine($"Progres: {_item.ProgressText}");
        builder.AppendLine($"Kecepatan: {_item.SpeedText}");
        builder.AppendLine($"ETA: {_item.EtaText}");
        builder.AppendLine($"URL: {_item.Url}");
        builder.AppendLine($"Folder: {_item.DirectoryPath}");
        builder.AppendLine($"File: {_item.FilePath ?? "—"}");
        builder.AppendLine($"Error: {_item.ErrorMessage ?? "—"}");

        try
        {
            Clipboard.SetText(builder.ToString());
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Download Aja", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenUrl_Click(object sender, RoutedEventArgs e)
    {
        if (!Uri.TryCreate(_item.Url, UriKind.Absolute, out var uri))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = uri.AbsoluteUri,
            UseShellExecute = true
        });
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var directory = !string.IsNullOrWhiteSpace(_item.FilePath)
            ? Path.GetDirectoryName(_item.FilePath)
            : _item.DirectoryPath;

        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true
        });
    }
}
