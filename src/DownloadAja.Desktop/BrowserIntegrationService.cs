using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace DownloadAja.Desktop;

public enum ChromiumBrowserKind
{
    Chrome,
    Edge,
    Brave,
    Vivaldi
}

public static class BrowserIntegrationService
{
    public const string HostName = "com.downloadaja.bridge";

    private static readonly string[] NativeMessagingRegistryRoots =
    [
        @"Software\Google\Chrome\NativeMessagingHosts",
        @"Software\Microsoft\Edge\NativeMessagingHosts",
        @"Software\Chromium\NativeMessagingHosts"
    ];

    public static string ExtensionDirectory =>
        Path.Combine(AppContext.BaseDirectory, "browser-extension", "chrome");

    public static string BridgePath =>
        Path.Combine(AppContext.BaseDirectory, "browser-bridge", "DownloadAja.BrowserBridge.exe");

    public static string HostManifestPath =>
        Path.Combine(AppContext.BaseDirectory, "data", HostName + ".json");

    public static void RegisterChromiumBrowsers(string extensionIdsText)
    {
        var extensionIds = ParseExtensionIds(extensionIdsText);

        if (!Directory.Exists(ExtensionDirectory))
            throw new DirectoryNotFoundException($"Folder extension tidak ditemukan: {ExtensionDirectory}");

        if (!File.Exists(BridgePath))
            throw new FileNotFoundException("Browser bridge tidak ditemukan.", BridgePath);

        Directory.CreateDirectory(Path.GetDirectoryName(HostManifestPath)!);

        var manifest = new
        {
            name = HostName,
            description = "Download Aja Chromium Native Messaging Bridge",
            path = Path.GetFullPath(BridgePath),
            type = "stdio",
            allowed_origins = extensionIds
                .Select(id => $"chrome-extension://{id}/")
                .ToArray()
        };

        File.WriteAllText(
            HostManifestPath,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        var manifestPath = Path.GetFullPath(HostManifestPath);
        foreach (var root in NativeMessagingRegistryRoots)
        {
            using var key = Registry.CurrentUser.CreateSubKey(
                $@"{root}\{HostName}",
                writable: true);

            if (key is null)
                throw new InvalidOperationException($"Tidak dapat membuat registry Native Messaging: {root}");

            key.SetValue("", manifestPath, RegistryValueKind.String);
        }
    }

    public static void UnregisterChromiumBrowsers()
    {
        foreach (var root in NativeMessagingRegistryRoots)
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                $@"{root}\{HostName}",
                throwOnMissingSubKey: false);
        }

        try
        {
            if (File.Exists(HostManifestPath))
                File.Delete(HostManifestPath);
        }
        catch
        {
            // Registry sudah terhapus; manifest sisa tidak memblokir aplikasi.
        }
    }

    public static IReadOnlyList<string> GetRegisteredExtensionIds()
    {
        try
        {
            if (!File.Exists(HostManifestPath))
                return [];

            using var document = JsonDocument.Parse(File.ReadAllText(HostManifestPath));
            if (!document.RootElement.TryGetProperty("allowed_origins", out var origins) ||
                origins.ValueKind != JsonValueKind.Array)
                return [];

            const string prefix = "chrome-extension://";
            return origins
                .EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value) &&
                                value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(value => value![prefix.Length..].TrimEnd('/'))
                .Where(IsValidExtensionId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    public static string? GetRegisteredExtensionId()
        => GetRegisteredExtensionIds().FirstOrDefault();

    public static bool IsAnyChromiumBrowserRegistered()
        => NativeMessagingRegistryRoots.Any(IsRegisteredAtRoot)
            && File.Exists(BridgePath)
            && File.Exists(HostManifestPath);

    public static IReadOnlyDictionary<string, bool> GetRegistrationStatus()
        => new Dictionary<string, bool>
        {
            ["Chrome"] = IsRegisteredAtRoot(NativeMessagingRegistryRoots[0]),
            ["Edge"] = IsRegisteredAtRoot(NativeMessagingRegistryRoots[1]),
            ["Chromium fallback"] = IsRegisteredAtRoot(NativeMessagingRegistryRoots[2])
        };

    public static string? FindBrowserExecutable(ChromiumBrowserKind browser)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        string?[] candidates = browser switch
        {
            ChromiumBrowserKind.Chrome =>
            [
                Path.Combine(local, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe")
            ],
            ChromiumBrowserKind.Edge =>
            [
                Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(local, "Microsoft", "Edge", "Application", "msedge.exe")
            ],
            ChromiumBrowserKind.Brave =>
            [
                Path.Combine(local, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
                Path.Combine(programFiles, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
                Path.Combine(programFilesX86, "BraveSoftware", "Brave-Browser", "Application", "brave.exe")
            ],
            ChromiumBrowserKind.Vivaldi =>
            [
                Path.Combine(local, "Vivaldi", "Application", "vivaldi.exe"),
                Path.Combine(programFiles, "Vivaldi", "Application", "vivaldi.exe"),
                Path.Combine(programFilesX86, "Vivaldi", "Application", "vivaldi.exe")
            ],
            _ => []
        };

        return candidates.FirstOrDefault(path =>
            !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }

    public static string GetBrowserDisplayName(ChromiumBrowserKind browser)
        => browser switch
        {
            ChromiumBrowserKind.Chrome => "Google Chrome",
            ChromiumBrowserKind.Edge => "Microsoft Edge",
            ChromiumBrowserKind.Brave => "Brave",
            ChromiumBrowserKind.Vivaldi => "Vivaldi",
            _ => browser.ToString()
        };

    private static bool IsRegisteredAtRoot(string root)
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"{root}\{HostName}");
        var value = key?.GetValue("") as string;
        return !string.IsNullOrWhiteSpace(value)
            && File.Exists(value);
    }

    private static string[] ParseExtensionIds(string text)
    {
        var values = text
            .Split([',', ';', '\r', '\n', '\t', ' '],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (values.Length == 0)
            throw new ArgumentException("Masukkan minimal satu ID extension.");

        var invalid = values.FirstOrDefault(value => !IsValidExtensionId(value));
        if (invalid is not null)
            throw new ArgumentException(
                $"ID extension tidak valid: {invalid}. ID harus 32 karakter (huruf a sampai p).");

        return values;
    }

    private static bool IsValidExtensionId(string value)
        => value.Length == 32 && value.All(ch => ch >= 'a' && ch <= 'p');
}
