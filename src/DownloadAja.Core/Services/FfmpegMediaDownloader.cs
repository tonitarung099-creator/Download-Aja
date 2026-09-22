using System.Diagnostics;
using System.Globalization;
using System.Text;
using DownloadAja.Core.Models;

namespace DownloadAja.Core.Services;

public sealed class FfmpegMediaDownloader
{
    public FfmpegMediaSession Start(
        string url,
        string outputPath,
        DownloadRequestContext? context = null)
    {
        var exe = Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", "ffmpeg.exe");
        if (!File.Exists(exe))
            throw new FileNotFoundException(
                "FFmpeg tidak ditemukan. Gunakan build portable terbaru dari GitHub Actions.",
                exe);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-nostdin");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("warning");
        psi.ArgumentList.Add("-progress");
        psi.ArgumentList.Add("pipe:1");
        psi.ArgumentList.Add("-nostats");

        if (!string.IsNullOrWhiteSpace(context?.UserAgent))
        {
            psi.ArgumentList.Add("-user_agent");
            psi.ArgumentList.Add(SanitizeHeaderValue(context.UserAgent, 4096));
        }

        var headers = BuildHeaders(context);
        if (!string.IsNullOrWhiteSpace(headers))
        {
            psi.ArgumentList.Add("-headers");
            psi.ArgumentList.Add(headers);
        }

        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(url);
        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("0:v?");
        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("0:a?");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("copy");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("matroska");
        psi.ArgumentList.Add("-n");
        psi.ArgumentList.Add(outputPath);

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Gagal menjalankan FFmpeg.");

        return new FfmpegMediaSession(process, outputPath);
    }

    private static string BuildHeaders(DownloadRequestContext? context)
    {
        if (context is null)
            return "";

        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(context.Referer))
            builder.Append("Referer: ").Append(SanitizeHeaderValue(context.Referer, 4096)).Append("\r\n");

        if (!string.IsNullOrWhiteSpace(context.CookieHeader))
            builder.Append("Cookie: ").Append(SanitizeHeaderValue(context.CookieHeader, 262144)).Append("\r\n");

        return builder.ToString();
    }

    private static string SanitizeHeaderValue(string value, int maxLength)
    {
        var clean = value.Replace("\r", "").Replace("\n", "");
        return clean.Length <= maxLength ? clean : clean[..maxLength];
    }
}

public sealed class FfmpegMediaSession : IAsyncDisposable
{
    private readonly Process _process;
    private readonly object _gate = new();
    private readonly Queue<string> _errorTail = new();
    private readonly Stopwatch _speedClock = Stopwatch.StartNew();
    private long _lastSize;
    private long _lastSpeedSampleMs;

    public string OutputPath { get; }
    public long BytesWritten { get; private set; }
    public long SpeedBytesPerSecond { get; private set; }
    public bool HasExited => _process.HasExited;
    public int? ExitCode => _process.HasExited ? _process.ExitCode : null;
    public Task Completion { get; }

    public string ErrorText
    {
        get
        {
            lock (_gate)
                return string.Join(Environment.NewLine, _errorTail);
        }
    }

    internal FfmpegMediaSession(Process process, string outputPath)
    {
        _process = process;
        OutputPath = outputPath;
        Completion = ObserveAsync();
    }

    public async Task StopAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
        }
        catch
        {
            // Best effort saat pengguna menghentikan stream.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _process.Dispose();
    }

    private async Task ObserveAsync()
    {
        var progressTask = ReadProgressAsync();
        var errorTask = ReadErrorsAsync();

        await _process.WaitForExitAsync();
        await Task.WhenAll(progressTask, errorTask);

        if (File.Exists(OutputPath))
        {
            var size = new FileInfo(OutputPath).Length;
            BytesWritten = Math.Max(BytesWritten, size);
        }

        SpeedBytesPerSecond = 0;
    }

    private async Task ReadProgressAsync()
    {
        string? line;
        while ((line = await _process.StandardOutput.ReadLineAsync()) is not null)
        {
            if (line.StartsWith("total_size=", StringComparison.Ordinal) &&
                long.TryParse(line.AsSpan("total_size=".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
            {
                UpdateSize(size);
            }
        }
    }

    private async Task ReadErrorsAsync()
    {
        string? line;
        while ((line = await _process.StandardError.ReadLineAsync()) is not null)
        {
            lock (_gate)
            {
                _errorTail.Enqueue(line);
                while (_errorTail.Count > 20)
                    _errorTail.Dequeue();
            }
        }
    }

    private void UpdateSize(long size)
    {
        size = Math.Max(0, size);
        var nowMs = _speedClock.ElapsedMilliseconds;
        var elapsed = nowMs - _lastSpeedSampleMs;

        if (elapsed >= 500)
        {
            var delta = Math.Max(0, size - _lastSize);
            SpeedBytesPerSecond = (long)(delta * 1000d / Math.Max(1, elapsed));
            _lastSize = size;
            _lastSpeedSampleMs = nowMs;
        }

        BytesWritten = size;
    }
}
