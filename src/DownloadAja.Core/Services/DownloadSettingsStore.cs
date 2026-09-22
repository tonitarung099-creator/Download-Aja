using System.Text.Json;
using DownloadAja.Core.Models;

namespace DownloadAja.Core.Services;

public sealed class DownloadSettingsStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public DownloadSettingsStore(string path)
    {
        _path = path;
    }

    public async Task<DownloadSettings> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_path))
            return new DownloadSettings();

        try
        {
            await using var stream = File.OpenRead(_path);
            return (await JsonSerializer.DeserializeAsync<DownloadSettings>(stream, _jsonOptions, ct)
                    ?? new DownloadSettings())
                .Normalize();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new DownloadSettings();
        }
    }

    public async Task SaveAsync(DownloadSettings settings, CancellationToken ct = default)
    {
        settings.Normalize();

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var temp = _path + ".tmp";
        await using (var stream = new FileStream(
            temp,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            4096,
            useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions, ct);
            await stream.FlushAsync(ct);
        }

        File.Move(temp, _path, overwrite: true);
    }
}
