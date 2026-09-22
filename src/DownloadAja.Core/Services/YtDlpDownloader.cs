using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using DownloadAja.Core.Models;

namespace DownloadAja.Core.Services;

public sealed class YtDlpDownloader
{
    public YtDlpSession Start(
        string url,
        string directory,
        string? suggestedName,
        string? formatProfile = null,
        long speedLimitBytesPerSecond = 0,
        DownloadRequestContext? context = null)
    {
        var ytDlp = Path.Combine(AppContext.BaseDirectory, "tools", "yt-dlp", "yt-dlp.exe");
        var deno = Path.Combine(AppContext.BaseDirectory, "tools", "deno", "deno.exe");
        var ffmpegDirectory = Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg");
        var ffmpeg = Path.Combine(ffmpegDirectory, "ffmpeg.exe");
        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
        var ytDlpCache = Path.Combine(dataDirectory, "yt-dlp-cache");
        var denoCache = Path.Combine(dataDirectory, "deno-cache");

        if (!File.Exists(ytDlp))
            throw new FileNotFoundException("yt-dlp tidak ditemukan. Gunakan build portable terbaru.", ytDlp);

        if (!File.Exists(deno))
            throw new FileNotFoundException("Deno tidak ditemukan. Dukungan YouTube membutuhkan runtime JavaScript portable.", deno);

        if (!File.Exists(ffmpeg))
            throw new FileNotFoundException("FFmpeg tidak ditemukan. Gunakan build portable terbaru.", ffmpeg);

        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(ytDlpCache);
        Directory.CreateDirectory(denoCache);

        var outputTemplate = BuildOutputTemplate(suggestedName);

        var psi = new ProcessStartInfo
        {
            FileName = ytDlp,
            WorkingDirectory = Path.GetDirectoryName(ytDlp)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // Semua state runtime disimpan di folder portable Download Aja.
        psi.Environment["DENO_DIR"] = denoCache;
        psi.Environment["NO_COLOR"] = "1";

        // Jangan biarkan config/plugin/cookie dari profil Windows mengubah perilaku aplikasi.
        Add(psi, "--ignore-config");
        Add(psi, "--no-plugin-dirs");
        Add(psi, "--no-cookies-from-browser");
        Add(psi, "--cache-dir", ytDlpCache);

        Add(psi, "--no-playlist");
        Add(psi, "--windows-filenames");
        Add(psi, "--trim-filenames", "180");
        Add(psi, "--continue");
        Add(psi, "--newline");
        Add(psi, "--no-colors");
        Add(psi, "--no-update");
        Add(psi, "--no-remote-components");
        Add(psi, "--retries", "5");
        Add(psi, "--fragment-retries", "5");
        Add(psi, "--concurrent-fragments", "4");
        Add(psi, "--format", YouTubeFormatProfiles.GetFormatSelector(formatProfile));
        Add(psi, "--merge-output-format", "mkv");
        Add(psi, "--ffmpeg-location", ffmpegDirectory);
        Add(psi, "--js-runtimes", $"deno:{deno}");
        Add(psi, "--paths", directory);
        Add(psi, "--output", outputTemplate);
        Add(psi, "--print", "before_dl:__DA_TITLE__%(title)s");
        Add(psi, "--print", "after_move:__DA_FILE__%(filepath)s");

        // --print dapat mengaktifkan quiet mode, jadi --progress ditempatkan setelahnya.
        Add(psi, "--progress");

        if (speedLimitBytesPerSecond > 0)
            Add(psi, "--limit-rate", speedLimitBytesPerSecond.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(context?.UserAgent))
            Add(psi, "--user-agent", SanitizeHeaderValue(context.UserAgent, 4096));

        if (!string.IsNullOrWhiteSpace(context?.Referer))
            Add(psi, "--referer", SanitizeHeaderValue(context.Referer, 4096));

        // Tahap 0.5.0 dibatasi pada media publik/non-DRM.
        psi.ArgumentList.Add(url);

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Gagal menjalankan yt-dlp.");

        return new YtDlpSession(process);
    }

    private static void Add(ProcessStartInfo psi, string name, string? value = null)
    {
        psi.ArgumentList.Add(name);
        if (value is not null)
            psi.ArgumentList.Add(value);
    }

    private static string BuildOutputTemplate(string? suggestedName)
    {
        var baseName = NormalizeBaseName(suggestedName);
        return string.IsNullOrWhiteSpace(baseName)
            ? "%(title).180B [%(id)s].%(ext)s"
            : $"{baseName} [%(id)s].%(ext)s";
    }

    private static string? NormalizeBaseName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var baseName = Path.GetFileNameWithoutExtension(value.Trim());
        if (baseName.Equals("YouTube video", StringComparison.OrdinalIgnoreCase))
            return null;

        foreach (var invalid in Path.GetInvalidFileNameChars())
            baseName = baseName.Replace(invalid, '_');

        baseName = baseName.Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(baseName))
            return null;

        return baseName.Length > 120 ? baseName[..120] : baseName;
    }

    private static string SanitizeHeaderValue(string value, int maxLength)
    {
        var clean = value.Replace("\r", "").Replace("\n", "");
        return clean.Length <= maxLength ? clean : clean[..maxLength];
    }
}

public sealed class YtDlpSession : IAsyncDisposable
{
    private static readonly Regex PercentRegex = new(
        @"\[download\]\s+(?<value>\d+(?:\.\d+)?)%",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TotalRegex = new(
        @"\bof\s+(?:~\s*)?(?<value>\d+(?:\.\d+)?)\s*(?<unit>[KMGTPE]?i?B)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex SpeedRegex = new(
        @"\bat\s+(?<value>\d+(?:\.\d+)?)\s*(?<unit>[KMGTPE]?i?B)/s",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly Process _process;
    private readonly object _gate = new();
    private readonly Queue<string> _errorTail = new();
    private string? _title;
    private string? _finalPath;

    public long TotalBytes { get; private set; }
    public long DownloadedBytes { get; private set; }
    public long SpeedBytesPerSecond { get; private set; }
    public bool HasExited => _process.HasExited;
    public int? ExitCode => _process.HasExited ? _process.ExitCode : null;
    public Task Completion { get; }

    public string? Title
    {
        get { lock (_gate) return _title; }
    }

    public string? FinalPath
    {
        get { lock (_gate) return _finalPath; }
    }

    public string ErrorText
    {
        get
        {
            lock (_gate)
                return string.Join(Environment.NewLine, _errorTail);
        }
    }

    internal YtDlpSession(Process process)
    {
        _process = process;
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
            // Best effort. File .part dibiarkan agar dapat dilanjutkan.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _process.Dispose();
    }

    private async Task ObserveAsync()
    {
        var stdout = ReadStdoutAsync();
        var stderr = ReadStderrAsync();

        await _process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);

        SpeedBytesPerSecond = 0;

        var finalPath = FinalPath;
        if (!string.IsNullOrWhiteSpace(finalPath) && File.Exists(finalPath))
        {
            var length = new FileInfo(finalPath).Length;
            TotalBytes = Math.Max(TotalBytes, length);
            DownloadedBytes = Math.Max(DownloadedBytes, length);
        }
    }

    private async Task ReadStdoutAsync()
    {
        string? line;
        while ((line = await _process.StandardOutput.ReadLineAsync()) is not null)
            ParseLine(line, recordError: false);
    }

    private async Task ReadStderrAsync()
    {
        string? line;
        while ((line = await _process.StandardError.ReadLineAsync()) is not null)
            ParseLine(line, recordError: true);
    }

    private void ParseLine(string line, bool recordError)
    {
        if (line.StartsWith("__DA_TITLE__", StringComparison.Ordinal))
        {
            lock (_gate)
                _title = line["__DA_TITLE__".Length..].Trim();
            return;
        }

        if (line.StartsWith("__DA_FILE__", StringComparison.Ordinal))
        {
            lock (_gate)
                _finalPath = line["__DA_FILE__".Length..].Trim();
            return;
        }

        var isProgress = line.Contains("[download]", StringComparison.OrdinalIgnoreCase);
        if (isProgress)
            ParseProgress(line);

        if (recordError && !isProgress && !string.IsNullOrWhiteSpace(line))
        {
            lock (_gate)
            {
                _errorTail.Enqueue(line);
                while (_errorTail.Count > 30)
                    _errorTail.Dequeue();
            }
        }
    }

    private void ParseProgress(string line)
    {
        var totalMatch = TotalRegex.Match(line);
        if (totalMatch.Success)
        {
            var parsed = ParseSize(totalMatch.Groups["value"].Value, totalMatch.Groups["unit"].Value);
            if (parsed > 0)
                TotalBytes = parsed;
        }

        var percentMatch = PercentRegex.Match(line);
        if (percentMatch.Success &&
            double.TryParse(
                percentMatch.Groups["value"].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var percent) &&
            TotalBytes > 0)
        {
            DownloadedBytes = (long)Math.Clamp(
                TotalBytes * percent / 100d,
                0,
                TotalBytes);
        }

        var speedMatch = SpeedRegex.Match(line);
        if (speedMatch.Success)
        {
            SpeedBytesPerSecond = ParseSize(
                speedMatch.Groups["value"].Value,
                speedMatch.Groups["unit"].Value);
        }
    }

    private static long ParseSize(string valueText, string unit)
    {
        if (!double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return 0;

        var multiplier = unit.ToUpperInvariant() switch
        {
            "B" => 1d,
            "KB" => 1000d,
            "KIB" => 1024d,
            "MB" => 1000d * 1000d,
            "MIB" => 1024d * 1024d,
            "GB" => 1000d * 1000d * 1000d,
            "GIB" => 1024d * 1024d * 1024d,
            "TB" => 1000d * 1000d * 1000d * 1000d,
            "TIB" => 1024d * 1024d * 1024d * 1024d,
            _ => 0d
        };

        if (multiplier <= 0)
            return 0;

        var result = value * multiplier;
        return result >= long.MaxValue ? long.MaxValue : Math.Max(0, (long)result);
    }
}
