using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DownloadAja.Core.Models;
using DownloadAja.Desktop.ViewModels;

namespace DownloadAja.Desktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly DispatcherTimer _refreshTimer;
    private bool _refreshInProgress;
    private bool _closing;
    private bool _changingFilter;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(750)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
    }

    public async Task EnqueueUrlAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;

        try
        {
            EngineStatusText.Text = "Menambahkan download...";
            var item = await _viewModel.AddAndStartAsync(uri.AbsoluteUri);
            SelectItem(item);
            EngineStatusText.Text = "Siap";

            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
            Activate();
        }
        catch (Exception ex)
        {
            EngineStatusText.Text = "Gagal";
            MessageBox.Show(this, ex.Message, "Download Aja",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.InitializeAsync();
            EngineStatusText.Text = "Mesin download siap";
            _refreshTimer.Start();
        }
        catch (Exception ex)
        {
            EngineStatusText.Text = "Mesin download tidak tersedia";
            MessageBox.Show(this, ex.Message, "Download Aja",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_closing) return;

        e.Cancel = true;
        _closing = true;
        _refreshTimer.Stop();

        try
        {
            await _viewModel.DisposeAsync();
        }
        finally
        {
            Closing -= Window_Closing;
            Close();
        }
    }

    private async void AddUrl_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddUrlWindow(_viewModel.DownloadDirectory) { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            EngineStatusText.Text = dialog.StartImmediately
                ? "Menambahkan download..."
                : "Menambahkan ke antrean...";

            var item = await _viewModel.AddAsync(
                dialog.DownloadUrl,
                dialog.DirectoryPath,
                dialog.StartImmediately);

            SelectItem(item);
            EngineStatusText.Text = dialog.StartImmediately ? "Download dimulai" : "Masuk antrean";
        }
        catch (Exception ex)
        {
            EngineStatusText.Text = "Gagal";
            MessageBox.Show(this, ex.Message, "Download Aja",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (DownloadsGrid.SelectedItem is not DownloadItem item) return;
        await RunItemActionAsync(() => _viewModel.StartAsync(item), "Memulai download...");
    }

    private async void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (DownloadsGrid.SelectedItem is not DownloadItem item) return;
        await RunItemActionAsync(() => _viewModel.PauseAsync(item), "Menjeda download...");
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (DownloadsGrid.SelectedItem is not DownloadItem item) return;
        await RunItemActionAsync(() => _viewModel.StopAsync(item), "Menghentikan download...");
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (DownloadsGrid.SelectedItem is not DownloadItem item) return;
        await RunItemActionAsync(() => _viewModel.RemoveAsync(item), "Menghapus dari daftar...");
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var item = DownloadsGrid.SelectedItem as DownloadItem;
        var directory = item?.DirectoryPath;

        if (item is not null && !string.IsNullOrWhiteSpace(item.FilePath) && File.Exists(item.FilePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{item.FilePath}\"",
                UseShellExecute = true
            });
            return;
        }

        if (string.IsNullOrWhiteSpace(directory))
            directory = _viewModel.DownloadDirectory;

        if (Directory.Exists(directory))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true
            });
        }
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(
            _viewModel.ConnectionsPerDownload,
            _viewModel.SpeedLimitBytesPerSecond)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            await _viewModel.UpdateSettingsAsync(
                dialog.ConnectionsPerDownload,
                dialog.SpeedLimitBytesPerSecond);

            EngineStatusText.Text = $"Pengaturan disimpan — {dialog.ConnectionsPerDownload} koneksi/download";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Download Aja",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        => _viewModel.SetSearchText(SearchBox.Text);

    private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_changingFilter || StatusFilterList.SelectedItem is not ListBoxItem selected)
            return;

        _changingFilter = true;
        CategoryFilterList.SelectedIndex = -1;
        _changingFilter = false;

        _viewModel.SetFilter(selected.Tag?.ToString());
    }

    private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_changingFilter || CategoryFilterList.SelectedItem is not ListBoxItem selected)
            return;

        _changingFilter = true;
        StatusFilterList.SelectedIndex = -1;
        _changingFilter = false;

        _viewModel.SetFilter(selected.Tag?.ToString());
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (_refreshInProgress) return;
        _refreshInProgress = true;

        try
        {
            await _viewModel.RefreshAsync();
            SpeedStatusText.Text = $"Kecepatan: {FormatBytes(_viewModel.TotalSpeed)}/s";
            ActiveStatusText.Text = $"Aktif: {_viewModel.ActiveCount}";
        }
        catch
        {
            // Refresh berikutnya akan mencoba lagi.
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private async Task RunItemActionAsync(Func<Task> action, string status)
    {
        try
        {
            EngineStatusText.Text = status;
            await action();
            EngineStatusText.Text = "Siap";
        }
        catch (Exception ex)
        {
            EngineStatusText.Text = "Gagal";
            MessageBox.Show(this, ex.Message, "Download Aja",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SelectItem(DownloadItem item)
    {
        DownloadsGrid.SelectedItem = item;
        DownloadsGrid.ScrollIntoView(item);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:F0} {units[unit]}" : $"{value:F1} {units[unit]}";
    }
}
