using System.ComponentModel;
using System.Windows;
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
            DownloadsGrid.SelectedItem = item;
            DownloadsGrid.ScrollIntoView(item);
            EngineStatusText.Text = "Siap";
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
        var dialog = new AddUrlWindow { Owner = this };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.DownloadUrl))
            await EnqueueUrlAsync(dialog.DownloadUrl);
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
