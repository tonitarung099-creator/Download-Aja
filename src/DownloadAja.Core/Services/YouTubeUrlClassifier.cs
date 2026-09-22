namespace DownloadAja.Core.Services;

public static class YouTubeUrlClassifier
{
    public static bool IsYouTubeHost(string? value)
    {
        if (!TryGetHttpUri(value, out var uri))
            return false;

        var host = uri.Host.ToLowerInvariant();
        return host == "youtu.be"
            || host == "youtube.com"
            || host.EndsWith(".youtube.com", StringComparison.Ordinal)
            || host == "youtube-nocookie.com"
            || host.EndsWith(".youtube-nocookie.com", StringComparison.Ordinal);
    }

    public static bool IsVideoUrl(string? value)
    {
        if (!TryGetHttpUri(value, out var uri))
            return false;

        var host = uri.Host.ToLowerInvariant();
        var path = uri.AbsolutePath;

        if (host == "youtu.be")
            return path.Split('/', StringSplitOptions.RemoveEmptyEntries).Length >= 1;

        var isYouTube = host == "youtube.com"
            || host.EndsWith(".youtube.com", StringComparison.Ordinal)
            || host == "youtube-nocookie.com"
            || host.EndsWith(".youtube-nocookie.com", StringComparison.Ordinal);

        if (!isYouTube)
            return false;

        if (path.Equals("/watch", StringComparison.OrdinalIgnoreCase))
            return HasNonEmptyQueryParameter(uri, "v");

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            return false;

        return segments[0].Equals("shorts", StringComparison.OrdinalIgnoreCase)
            || segments[0].Equals("live", StringComparison.OrdinalIgnoreCase)
            || segments[0].Equals("embed", StringComparison.OrdinalIgnoreCase)
            || segments[0].Equals("v", StringComparison.OrdinalIgnoreCase)
            || segments[0].Equals("clip", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetHttpUri(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var parsed) &&
            (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
        {
            uri = parsed;
            return true;
        }

        uri = null!;
        return false;
    }

    private static bool HasNonEmptyQueryParameter(Uri uri, string name)
    {
        var query = uri.Query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(query))
            return false;

        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            var rawKey = separator >= 0 ? part[..separator] : part;
            if (!Uri.UnescapeDataString(rawKey).Equals(name, StringComparison.OrdinalIgnoreCase))
                continue;

            var rawValue = separator >= 0 ? part[(separator + 1)..] : "";
            return !string.IsNullOrWhiteSpace(Uri.UnescapeDataString(rawValue));
        }

        return false;
    }
}
