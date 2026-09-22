namespace DownloadAja.Core.Models;

public sealed class DownloadSettings
{
    public int ConnectionsPerDownload { get; set; } = 8;
    public long SpeedLimitBytesPerSecond { get; set; }
    public int MaxSimultaneousDownloads { get; set; } = 3;
    public bool ClipboardMonitoringEnabled { get; set; }
    public bool SchedulerEnabled { get; set; }
    public DateTimeOffset? ScheduledQueueStartAt { get; set; }

    public DownloadSettings Normalize()
    {
        ConnectionsPerDownload = Math.Clamp(ConnectionsPerDownload, 1, 16);
        SpeedLimitBytesPerSecond = Math.Max(0, SpeedLimitBytesPerSecond);
        MaxSimultaneousDownloads = Math.Clamp(MaxSimultaneousDownloads, 1, 20);

        if (!SchedulerEnabled)
            ScheduledQueueStartAt = null;

        return this;
    }
}
