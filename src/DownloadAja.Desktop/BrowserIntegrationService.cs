using System.Text.Json;
using Microsoft.Win32;

namespace DownloadAja.Desktop;

public static class BrowserIntegrationService
{
    public const string HostName = "com.downloadaja.bridge";

    public static string ExtensionDirectory =>
        Path.Combine(AppContext.BaseDirectory, "browser-extension", "chrome");

    public static string BridgePath =>
        Path.Combine(AppContext.BaseDirectory, "browser-bridge", "DownloadAja.BrowserBridge.exe");

    public static string HostManifestPath =>
        Path.Combine(AppContext.BaseDirectory, "data", HostName + ".json");

    public static void RegisterChrome(string extensionId)
    {
        extensionId = ValidateExtensionId(extensionId);

        if (!Directory.Exists(ExtensionDirectory))
            throw new DirectoryNotFoundException($"Folder extension tidak ditemukan: {ExtensionDirectory}");

        if (!File.Exists(BridgePath))
            throw new FileNotFoundException("Browser bridge tidak ditemukan.", BridgePath);

        Directory.CreateDirectory(Path.GetDirectoryName(HostManifestPath)!);

        var manifest = new
        {
            name = HostName,
            description = "Download Aja Chrome Native Messaging Bridge",
            path = Path.GetFullPath(BridgePath),
            type = "stdio",
            allowed_origins = new[] { $"chrome-extension://{extensionId}/" }
        };

        File.WriteAllText(
            HostManifestPath,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        using var key = Registry.CurrentUser.CreateSubKey(
            $@"Software\Google\Chrome\NativeMessagingHosts\{HostName}",
            writable: true);

        if (key is null)
            throw new InvalidOperationException("Tidak dapat membuat registry Native Messaging untuk Chrome.");

        key.SetValue("", Path.GetFullPath(HostManifestPath), RegistryValueKind.String);
    }

    public static void UnregisterChrome()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                $@"Software\Google\Chrome\NativeMessagingHosts\{HostName}",
                throwOnMissingSubKey: false);
        }
        catch
        {
            // Pesan error nyata akan diberikan jika status dibaca kembali oleh UI.
            throw;
        }
    }

    public static bool IsChromeRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            $@"Software\Google\Chrome\NativeMessagingHosts\{HostName}");

        var value = key?.GetValue("") as string;
        return !string.IsNullOrWhiteSpace(value)
            && File.Exists(value)
            && File.Exists(BridgePath);
    }

    public static string? FindChromeExecutable()
    {
        string?[] candidates =
        [
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Google", "Chrome", "Application", "chrome.exe")
        ];

        return candidates.FirstOrDefault(path =>
            !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }

    private static string ValidateExtensionId(string extensionId)
    {
        var value = extensionId.Trim().ToLowerInvariant();

        if (value.Length != 32 || value.Any(ch => ch < 'a' || ch > 'p'))
            throw new ArgumentException(
                "ID extension Chrome harus 32 karakter (huruf a sampai p). Salin ID dari chrome://extensions.");

        return value;
    }
}
