namespace DownloadAja.Core.Models;

public sealed class DownloadSettings
{
    public int ConnectionsPerDownload { get; set; } = 8;
    public long SpeedLimitBytesPerSecond { get; set; }

    public DownloadSettings Normalize()
    {
        ConnectionsPerDownload = Math.Clamp(ConnectionsPerDownload, 1, 16);
        SpeedLimitBytesPerSecond = Math.Max(0, SpeedLimitBytesPerSecond);
        return this;
    }
}
