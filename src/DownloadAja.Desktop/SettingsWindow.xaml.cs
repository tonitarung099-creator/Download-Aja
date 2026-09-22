using System.Windows;
using System.Windows.Controls;

namespace DownloadAja.Desktop;

public partial class SettingsWindow : Window
{
    public int ConnectionsPerDownload { get; private set; }
    public long SpeedLimitBytesPerSecond { get; private set; }
    public int MaxSimultaneousDownloads { get; private set; }
    public bool ClipboardMonitoringEnabled { get; private set; }

    public SettingsWindow(
        int connectionsPerDownload,
        long speedLimitBytesPerSecond,
        int maxSimultaneousDownloads,
        bool clipboardMonitoringEnabled)
    {
        InitializeComponent();

        ConnectionsPerDownload = Math.Clamp(connectionsPerDownload, 1, 16);
        SpeedLimitBytesPerSecond = Math.Max(0, speedLimitBytesPerSecond);
        MaxSimultaneousDownloads = Math.Clamp(maxSimultaneousDownloads, 1, 20);
        ClipboardMonitoringEnabled = clipboardMonitoringEnabled;

        SelectComboValue(ConnectionsBox, ConnectionsPerDownload, fallbackIndex: 3);
        SelectComboValue(SimultaneousBox, MaxSimultaneousDownloads, fallbackIndex: 2);

        SpeedLimitBox.Text = (SpeedLimitBytesPerSecond / 1024L).ToString();
        ClipboardMonitoringBox.IsChecked = ClipboardMonitoringEnabled;
    }

    private void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
        new DiagnosticsWindow { Owner = this }.ShowDialog();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadComboValue(ConnectionsBox, out var connections))
        {
            ValidationText.Text = "Pilih jumlah koneksi.";
            return;
        }

        if (!TryReadComboValue(SimultaneousBox, out var simultaneous))
        {
            ValidationText.Text = "Pilih jumlah download simultan.";
            return;
        }

        if (!long.TryParse(SpeedLimitBox.Text.Trim(), out var speedKb) || speedKb < 0)
        {
            ValidationText.Text = "Batas kecepatan harus berupa angka 0 atau lebih.";
            return;
        }

        try
        {
            ConnectionsPerDownload = Math.Clamp(connections, 1, 16);
            MaxSimultaneousDownloads = Math.Clamp(simultaneous, 1, 20);
            SpeedLimitBytesPerSecond = checked(speedKb * 1024L);
            ClipboardMonitoringEnabled = ClipboardMonitoringBox.IsChecked == true;
            DialogResult = true;
        }
        catch (OverflowException)
        {
            ValidationText.Text = "Batas kecepatan terlalu besar.";
        }
    }

    private static bool TryReadComboValue(ComboBox combo, out int value)
    {
        value = 0;
        return combo.SelectedItem is ComboBoxItem selected
            && int.TryParse(selected.Tag?.ToString(), out value);
    }

    private static void SelectComboValue(ComboBox combo, int value, int fallbackIndex)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (int.TryParse(item.Tag?.ToString(), out var candidate) && candidate == value)
            {
                combo.SelectedItem = item;
                return;
            }
        }

        combo.SelectedIndex = fallbackIndex;
    }
}
