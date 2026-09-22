using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
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

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("DownloadAja", "0.4.0"));

            using var response = await http.GetAsync(
                "https://api.github.com/repos/tonitarung099-creator/Download-Aja/releases/latest");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                MessageBox.Show(
                    this,
                    "Belum ada GitHub Release resmi. Gunakan artifact GitHub Actions terbaru yang berstatus sukses.",
                    "Download Aja",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStreamAsync());

            var tag = document.RootElement.TryGetProperty("tag_name", out var tagElement)
                ? tagElement.GetString()
                : null;

            var htmlUrl = document.RootElement.TryGetProperty("html_url", out var urlElement)
                ? urlElement.GetString()
                : null;

            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            var latest = ParseReleaseVersion(tag);

            if (latest is null)
            {
                MessageBox.Show(
                    this,
                    $"Release terbaru ditemukan ({tag ?? "tanpa versi"}), tetapi versinya tidak dapat dibandingkan.",
                    "Download Aja",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (latest <= current)
            {
                MessageBox.Show(
                    this,
                    $"Versi kamu sudah terbaru.\n\nTerpasang: {current.ToString(3)}\nRelease: {latest}",
                    "Download Aja",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var answer = MessageBox.Show(
                this,
                $"Versi baru tersedia.\n\nTerpasang: {current.ToString(3)}\nTerbaru: {latest}\n\nBuka halaman release?",
                "Download Aja",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (answer == MessageBoxResult.Yes && Uri.TryCreate(htmlUrl, UriKind.Absolute, out var releaseUri))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = releaseUri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Tidak dapat memeriksa pembaruan.\n\n{ex.Message}",
                "Download Aja",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static Version? ParseReleaseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;

        var normalized = tag.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[1..];

        var dash = normalized.IndexOf('-');
        if (dash >= 0)
            normalized = normalized[..dash];

        return Version.TryParse(normalized, out var version)
            ? version
            : null;
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
