using System.Diagnostics;
using System.IO;
using System.Windows;

namespace DownloadAja.Desktop;

public partial class BrowserIntegrationWindow : Window
{
    public BrowserIntegrationWindow()
    {
        InitializeComponent();
        ExtensionPathBox.Text = BrowserIntegrationService.ExtensionDirectory;
        ExtensionIdBox.Text = BrowserIntegrationService.GetRegisteredExtensionId() ?? "";
        RefreshStatus();
    }

    private void OpenChromeExtensions_Click(object sender, RoutedEventArgs e)
    {
        var chrome = BrowserIntegrationService.FindChromeExecutable();
        if (chrome is null)
        {
            MessageBox.Show(this,
                "Google Chrome tidak ditemukan di lokasi standar Windows.",
                "Download Aja",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = chrome,
            Arguments = "chrome://extensions/",
            UseShellExecute = true
        });
    }

    private void OpenExtensionFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = BrowserIntegrationService.ExtensionDirectory;
        if (!Directory.Exists(folder))
        {
            MessageBox.Show(this,
                "Folder extension tidak ditemukan. Gunakan build portable terbaru.",
                "Download Aja",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    private void CopyExtensionPath_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(BrowserIntegrationService.ExtensionDirectory);
        StatusText.Text = "Path extension sudah disalin.";
    }

    private void Register_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BrowserIntegrationService.RegisterChrome(ExtensionIdBox.Text);
            StatusText.Text = "✓ Integrasi Chrome terdaftar. Jika Chrome sedang terbuka, restart Chrome lalu coba menu “Download dengan Download Aja”.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Gagal mendaftarkan integrasi: " + ex.Message;
        }
    }

    private void Unregister_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BrowserIntegrationService.UnregisterChrome();
            RefreshStatus();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Gagal menghapus integrasi: " + ex.Message;
        }
    }

    private void RefreshStatus()
    {
        var extensionId = BrowserIntegrationService.GetRegisteredExtensionId();
        StatusText.Text = BrowserIntegrationService.IsChromeRegistered()
            ? $"✓ Native Messaging Chrome terdaftar{(string.IsNullOrWhiteSpace(extensionId) ? "." : $" untuk extension {extensionId}.")}"
            : "Integrasi Native Messaging Chrome belum terdaftar.";
    }
}
