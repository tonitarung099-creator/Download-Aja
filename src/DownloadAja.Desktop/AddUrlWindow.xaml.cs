using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace DownloadAja.Desktop;

public partial class AddUrlWindow : Window
{
    public string DownloadUrl => UrlBox.Text.Trim();
    public string DirectoryPath => DirectoryBox.Text.Trim();
    public bool StartImmediately => StartNowCheckBox.IsChecked == true;

    public AddUrlWindow(string defaultDirectory)
    {
        InitializeComponent();
        DirectoryBox.Text = defaultDirectory;

        Loaded += (_, _) =>
        {
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText().Trim();
                if (Uri.TryCreate(text, UriKind.Absolute, out _))
                    UrlBox.Text = text;
            }

            UrlBox.Focus();
            UrlBox.SelectAll();
        };
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

        if (string.IsNullOrWhiteSpace(DirectoryPath))
        {
            MessageBox.Show(this, "Pilih folder penyimpanan.", "Download Aja",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
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
