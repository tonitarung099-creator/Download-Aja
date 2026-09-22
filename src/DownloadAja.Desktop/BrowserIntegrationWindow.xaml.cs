using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace DownloadAja.Desktop;

public partial class BrowserIntegrationWindow : Window
{
    public BrowserIntegrationWindow()
    {
        InitializeComponent();
        ExtensionPathBox.Text = BrowserIntegrationService.ExtensionDirectory;
        ExtensionIdBox.Text = string.Join(Environment.NewLine, BrowserIntegrationService.GetRegisteredExtensionIds());
        RefreshStatus();
    }

    private void OpenBrowserExtensions_Click(object sender, RoutedEventArgs e)
    {
        var browser = GetSelectedBrowser();
        var executable = BrowserIntegrationService.FindBrowserExecutable(browser);
        var displayName = BrowserIntegrationService.GetBrowserDisplayName(browser);

        if (executable is null)
        {
            MessageBox.Show(this,
                $"{displayName} tidak ditemukan di lokasi standar Windows.",
                "Download Aja",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = executable,
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
            BrowserIntegrationService.RegisterChromiumBrowsers(ExtensionIdBox.Text);
            StatusText.Text = "✓ Native Messaging didaftarkan untuk Chrome, Edge, dan Chromium fallback. Restart browser yang sedang terbuka lalu coba extension.";
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
            BrowserIntegrationService.UnregisterChromiumBrowsers();
            RefreshStatus();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Gagal menghapus integrasi: " + ex.Message;
        }
    }

    private ChromiumBrowserKind GetSelectedBrowser()
    {
        if (BrowserBox.SelectedItem is ComboBoxItem selected &&
            Enum.TryParse<ChromiumBrowserKind>(selected.Tag?.ToString(), out var browser))
            return browser;

        return ChromiumBrowserKind.Chrome;
    }

    private void RefreshStatus()
    {
        var extensionIds = BrowserIntegrationService.GetRegisteredExtensionIds();
        var status = BrowserIntegrationService.GetRegistrationStatus();

        var details = string.Join(" • ", status.Select(pair =>
            $"{pair.Key}: {(pair.Value ? "terdaftar" : "belum")}"));

        var idText = extensionIds.Count == 0
            ? "tanpa ID tersimpan"
            : $"{extensionIds.Count} ID extension";

        StatusText.Text = BrowserIntegrationService.IsAnyChromiumBrowserRegistered()
            ? $"✓ Integrasi aktif untuk {idText}.\n{details}"
            : $"Integrasi Native Messaging belum terdaftar.\n{details}";
    }
}
