using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;

namespace DownloadAja.Desktop;

public partial class DiagnosticsWindow : Window
{
    public DiagnosticsWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "tidak diketahui";
        VersionText.Text = $"Versi {version}";
        RefreshDiagnostics();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
        => RefreshDiagnostics();

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(DiagnosticsBox.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Download Aja", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "data", "logs");
        Directory.CreateDirectory(folder);

        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    private void RefreshDiagnostics()
    {
        var baseDir = AppContext.BaseDirectory;
        var aria = Path.Combine(baseDir, "tools", "aria2", "aria2c.exe");
        var ffmpeg = Path.Combine(baseDir, "tools", "ffmpeg", "ffmpeg.exe");
        var bridge = BrowserIntegrationService.BridgePath;
        var extension = BrowserIntegrationService.ExtensionDirectory;
        var dataDir = Path.Combine(baseDir, "data");
        var logsDir = Path.Combine(dataDir, "logs");

        var status = BrowserIntegrationService.GetRegistrationStatus();
        var extensionId = BrowserIntegrationService.GetRegisteredExtensionId();

        var builder = new StringBuilder();
        builder.AppendLine($"Versi: {Assembly.GetExecutingAssembly().GetName().Version}");
        builder.AppendLine($"Waktu lokal: {DateTimeOffset.Now:O}");
        builder.AppendLine($"OS: {Environment.OSVersion}");
        builder.AppendLine($".NET: {Environment.Version}");
        builder.AppendLine($"64-bit process: {Environment.Is64BitProcess}");
        builder.AppendLine();
        builder.AppendLine($"Portable root: {baseDir}");
        builder.AppendLine($"Data: {dataDir}");
        builder.AppendLine($"Logs: {logsDir}");
        builder.AppendLine();
        builder.AppendLine($"aria2: {(File.Exists(aria) ? "OK" : "TIDAK DITEMUKAN")} — {aria}");
        builder.AppendLine($"FFmpeg: {(File.Exists(ffmpeg) ? "OK" : "TIDAK DITEMUKAN")} — {ffmpeg}");
        builder.AppendLine($"Browser bridge: {(File.Exists(bridge) ? "OK" : "TIDAK DITEMUKAN")} — {bridge}");
        builder.AppendLine($"Extension folder: {(Directory.Exists(extension) ? "OK" : "TIDAK DITEMUKAN")} — {extension}");
        builder.AppendLine();
        builder.AppendLine($"Extension ID: {extensionId ?? "belum terdaftar"}");

        foreach (var pair in status)
            builder.AppendLine($"Native Messaging {pair.Key}: {(pair.Value ? "terdaftar" : "belum")}");

        DiagnosticsBox.Text = builder.ToString();
    }
}
