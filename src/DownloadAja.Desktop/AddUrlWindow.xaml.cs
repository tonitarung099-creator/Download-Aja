using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using DownloadAja.Core.Services;

namespace DownloadAja.Desktop;

public partial class AddUrlWindow : Window
{
    public string DownloadUrl => UrlBox.Text.Trim();
    public string DirectoryPath => DirectoryBox.Text.Trim();
    public string OutputFileName => FileNameBox.Text.Trim();
    public string YouTubeFormatProfile =>
        (YouTubeQualityBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? YouTubeFormatProfiles.Best;
    public bool StartImmediately => StartNowCheckBox.IsChecked == true;

    public AddUrlWindow(string defaultDirectory, string? initialUrl = null)
    {
        InitializeComponent();
        DirectoryBox.Text = defaultDirectory;

        Loaded += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(initialUrl))
            {
                UrlBox.Text = initialUrl.Trim();
            }
            else if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText().Trim();
                if (Uri.TryCreate(text, UriKind.Absolute, out _))
                    UrlBox.Text = text;
            }

            UrlBox.Focus();
            UrlBox.SelectAll();
        };
    }

    private void UrlBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        YouTubeInfo.Visibility = YouTubeUrlClassifier.IsVideoUrl(UrlBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Pilih folder penyimpanan",
            InitialDirectory = Directory.Exists(DirectoryPath) ? DirectoryPath : null
        };

        if (dialog.ShowDialog(this) == true)
            DirectoryBox.Text = dialog.FolderName;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!Uri.TryCreate(DownloadUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(this, "Masukkan URL HTTP/HTTPS yang valid.", "Download Aja",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (YouTubeUrlClassifier.IsYouTubeHost(DownloadUrl) &&
            !YouTubeUrlClassifier.IsVideoUrl(DownloadUrl))
        {
            MessageBox.Show(
                this,
                "URL YouTube ini bukan URL video. Buka videonya lalu gunakan URL watch, shorts, live, clip, atau youtu.be.",
                "Download Aja",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(DirectoryPath))
        {
            MessageBox.Show(this, "Pilih folder penyimpanan.", "Download Aja",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!string.IsNullOrWhiteSpace(OutputFileName))
        {
            var fileName = Path.GetFileName(OutputFileName);
            if (string.IsNullOrWhiteSpace(fileName) ||
                fileName is "." or ".." ||
                fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                ValidationText.Text = "Nama file mengandung karakter yang tidak valid.";
                return;
            }
        }

        try
        {
            Directory.CreateDirectory(DirectoryPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Folder tidak dapat digunakan: {ex.Message}", "Download Aja",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
