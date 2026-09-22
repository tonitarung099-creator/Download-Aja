using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace DownloadAja.BrowserBridge;

internal static class Program
{
    private const string PipeName = "DownloadAja.UrlPipe.v1";

    private static async Task Main()
    {
        using var input = Console.OpenStandardInput();

        while (true)
        {
            var message = await ReadMessageAsync(input);
            if (message is null) break;

            if (TryCreateDesktopPayload(message.RootElement, out var payload))
            {
                var delivered = await TrySendToRunningDesktopAsync(payload, timeoutMs: 700);

                if (!delivered)
                {
                    LaunchDesktop();
                    delivered = await RetrySendAsync(payload);
                }

                await WriteMessageAsync(delivered
                    ? new { ok = true }
                    : new { ok = false, error = "Download Aja tidak dapat dihubungi." });
                continue;
            }

            await WriteMessageAsync(new { ok = false, error = "Pesan tidak valid." });
        }
    }

    private static bool TryCreateDesktopPayload(JsonElement root, out string payload)
    {
        payload = "";

        if (!root.TryGetProperty("type", out var type) ||
            type.GetString() != "addDownload" ||
            !root.TryGetProperty("url", out var urlElement))
            return false;

        var url = urlElement.GetString();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return false;

        payload = JsonSerializer.Serialize(new
        {
            type = "addDownload",
            url = uri.AbsoluteUri,
            referrer = ReadCleanString(root, "referrer", 4096),
            userAgent = ReadCleanString(root, "userAgent", 4096),
            cookieHeader = ReadCleanString(root, "cookieHeader", 262144),
            mediaKind = ReadCleanString(root, "mediaKind", 32),
            suggestedName = ReadCleanString(root, "suggestedName", 512),
            formatProfile = ReadCleanString(root, "formatProfile", 32)
        });

        return true;
    }

    private static string? ReadCleanString(JsonElement root, string propertyName, int maxLength)
    {
        if (!root.TryGetProperty(propertyName, out var element) ||
            element.ValueKind != JsonValueKind.String)
            return null;

        var value = element.GetString();
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var clean = value.Replace("\r", "").Replace("\n", "");
        return clean.Length <= maxLength ? clean : clean[..maxLength];
    }

    private static async Task<bool> RetrySendAsync(string payload)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(100);
            if (await TrySendToRunningDesktopAsync(payload, timeoutMs: 400))
                return true;
        }

        return false;
    }

    private static async Task<bool> TrySendToRunningDesktopAsync(string message, int timeoutMs)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);

            using var timeout = new CancellationTokenSource(timeoutMs);
            await pipe.ConnectAsync(timeout.Token);

            await using var writer = new StreamWriter(pipe, new UTF8Encoding(false))
            {
                AutoFlush = true
            };

            await writer.WriteLineAsync(message);
            return true;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or TimeoutException)
        {
            return false;
        }
    }

    private static void LaunchDesktop()
    {
        var baseDir = AppContext.BaseDirectory;
        var exe = Path.GetFullPath(Path.Combine(baseDir, "..", "DownloadAja.exe"));

        if (!File.Exists(exe))
            exe = Path.Combine(baseDir, "DownloadAja.exe");

        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = true
        });
    }

    private static async Task<JsonDocument?> ReadMessageAsync(Stream input)
    {
        var lengthBytes = new byte[4];
        var read = await input.ReadAsync(lengthBytes);
        if (read == 0) return null;
        if (read != 4) throw new EndOfStreamException();

        var length = BitConverter.ToInt32(lengthBytes, 0);
        if (length <= 0 || length > 1024 * 1024)
            throw new InvalidDataException("Ukuran pesan tidak valid.");

        var buffer = new byte[length];
        var offset = 0;

        while (offset < length)
        {
            var n = await input.ReadAsync(buffer.AsMemory(offset, length - offset));
            if (n == 0) throw new EndOfStreamException();
            offset += n;
        }

        return JsonDocument.Parse(buffer);
    }

    private static async Task WriteMessageAsync(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        var output = Console.OpenStandardOutput();

        await output.WriteAsync(BitConverter.GetBytes(bytes.Length));
        await output.WriteAsync(bytes);
        await output.FlushAsync();
    }
}
