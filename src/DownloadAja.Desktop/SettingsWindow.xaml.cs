using System.Windows;
using System.Windows.Controls;

namespace DownloadAja.Desktop;

public partial class SettingsWindow : Window
{
    public int ConnectionsPerDownload { get; private set; }
    public long SpeedLimitBytesPerSecond { get; private set; }
    public bool ClipboardMonitoringEnabled { get; private set; }

    public SettingsWindow(
        int connectionsPerDownload,
        long speedLimitBytesPerSecond,
        bool clipboardMonitoringEnabled)
    {
        InitializeComponent();

        ConnectionsPerDownload = Math.Clamp(connectionsPerDownload, 1, 16);
        SpeedLimitBytesPerSecond = Math.Max(0, speedLimitBytesPerSecond);
        ClipboardMonitoringEnabled = clipboardMonitoringEnabled;

        foreach (ComboBoxItem item in ConnectionsBox.Items)
        {
            if (int.TryParse(item.Tag?.ToString(), out var value) && value == ConnectionsPerDownload)
            {
                ConnectionsBox.SelectedItem = item;
                break;
            }
        }

        if (ConnectionsBox.SelectedIndex < 0)
            ConnectionsBox.SelectedIndex = 3;

        SpeedLimitBox.Text = (SpeedLimitBytesPerSecond / 1024L).ToString();
        ClipboardMonitoringBox.IsChecked = ClipboardMonitoringEnabled;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ConnectionsBox.SelectedItem is not ComboBoxItem selected ||
            !int.TryParse(selected.Tag?.ToString(), out var connections))
        {
            ValidationText.Text = "Pilih jumlah koneksi.";
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
            SpeedLimitBytesPerSecond = checked(speedKb * 1024L);
            ClipboardMonitoringEnabled = ClipboardMonitoringBox.IsChecked == true;
            DialogResult = true;
        }
        catch (OverflowException)
        {
            ValidationText.Text = "Batas kecepatan terlalu besar.";
        }
    }
}
