namespace DownloadAja.Core.Models;

/// <summary>
/// Metadata sementara dari browser untuk satu request download.
/// Cookie sesi sengaja tidak disimpan ke riwayat agar tidak menetap di disk.
/// </summary>
public sealed record DownloadRequestContext(
    string? Referer = null,
    string? UserAgent = null,
    string? CookieHeader = null,
    string? MediaKind = null,
    string? SuggestedName = null,
    string? FormatProfile = null);
