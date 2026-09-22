using System.Diagnostics;
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

            if (message.RootElement.TryGetProperty("type", out var type) &&
                type.GetString() == "addDownload" &&
                message.RootElement.TryGetProperty("url", out var urlElement))
            {
                var url = urlElement.GetString();
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    var delivered = await TrySendToRunningDesktopAsync(uri.AbsoluteUri);
                    if (!delivered)
                    {
                        LaunchDesktop(uri.AbsoluteUri);
                        delivered = true;
                    }

                    await WriteMessageAsync(new { ok = delivered });
                    continue;
                }
            }

            await WriteMessageAsync(new { ok = false, error = "Pesan tidak valid." });
        }
    }

    private static async Task<bool> TrySendToRunningDesktopAsync(string url)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);

            using var timeout = new CancellationTokenSource(600);
            await pipe.ConnectAsync(timeout.Token);

            await using var writer = new StreamWriter(pipe, new UTF8Encoding(false))
            {
                AutoFlush = true
            };

            await writer.WriteLineAsync(url);
            return true;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or TimeoutException)
        {
            return false;
        }
    }

    private static void LaunchDesktop(string url)
    {
        var baseDir = AppContext.BaseDirectory;
        var exe = Path.GetFullPath(Path.Combine(baseDir, "..", "DownloadAja.exe"));

        if (!File.Exists(exe))
            exe = Path.Combine(baseDir, "DownloadAja.exe");

        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = true,
            ArgumentList = { "--add-url", url }
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
