namespace DownloadAja.Core.Services;

public static class YouTubeFormatProfiles
{
    public const string Best = "best";
    public const string P2160 = "2160p";
    public const string P1440 = "1440p";
    public const string P1080 = "1080p";
    public const string P720 = "720p";
    public const string P480 = "480p";
    public const string P360 = "360p";

    public static string Normalize(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            P2160 => P2160,
            P1440 => P1440,
            P1080 => P1080,
            P720 => P720,
            P480 => P480,
            P360 => P360,
            _ => Best
        };

    public static string GetFormatSelector(string? profile)
        => Normalize(profile) switch
        {
            P2160 => BuildMaxHeightSelector(2160),
            P1440 => BuildMaxHeightSelector(1440),
            P1080 => BuildMaxHeightSelector(1080),
            P720 => BuildMaxHeightSelector(720),
            P480 => BuildMaxHeightSelector(480),
            P360 => BuildMaxHeightSelector(360),
            _ => "bestvideo*+bestaudio/best"
        };

    public static string GetDisplayName(string? profile)
        => Normalize(profile) switch
        {
            P2160 => "Maks. 2160p (4K)",
            P1440 => "Maks. 1440p (2K)",
            P1080 => "Maks. 1080p",
            P720 => "Maks. 720p",
            P480 => "Maks. 480p",
            P360 => "Maks. 360p",
            _ => "Terbaik tersedia"
        };

    private static string BuildMaxHeightSelector(int height)
        => $"bestvideo*[height<={height}]+bestaudio/best[height<={height}]";
}
