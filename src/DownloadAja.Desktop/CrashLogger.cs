using System.Linq;
using System;
using System.IO;
using System.Text;

namespace DownloadAja.Desktop;

public static class CrashLogger
{
    private static readonly object Gate = new();

    public static string Log(string source, Exception exception)
    {
        try
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "data", "logs");
            Directory.CreateDirectory(directory);

            var path = Path.Combine(
                directory,
                $"crash-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}.log");

            var builder = new StringBuilder();
            builder.AppendLine("Download Aja crash log");
            builder.AppendLine($"Waktu: {DateTimeOffset.Now:O}");
            builder.AppendLine($"Sumber: {source}");
            builder.AppendLine($"OS: {Environment.OSVersion}");
            builder.AppendLine($".NET: {Environment.Version}");
            builder.AppendLine($"64-bit process: {Environment.Is64BitProcess}");
            builder.AppendLine();
            builder.AppendLine(Sanitize(exception.ToString()));

            lock (Gate)
                File.WriteAllText(path, builder.ToString(), Encoding.UTF8);

            return path;
        }
        catch
        {
            return "";
        }
    }

    private static string Sanitize(string value)
    {
        var lines = value
            .Replace("\r\n", "\n")
            .Split('\n');

        var safe = lines.Select(line =>
        {
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
                return "[header Cookie disembunyikan]";

            if (trimmed.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                return "[header Authorization disembunyikan]";

            return line;
        });

        return string.Join(Environment.NewLine, safe);
    }
}
