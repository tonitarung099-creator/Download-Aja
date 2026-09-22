namespace DownloadAja.Core.Models;

/// <summary>
/// Metadata sementara dari browser untuk satu request download.
/// Nilai ini sengaja tidak disimpan ke riwayat agar cookie sesi tidak menetap di disk.
/// </summary>
public sealed record DownloadRequestContext(
    string? Referer = null,
    string? UserAgent = null,
    string? CookieHeader = null);
