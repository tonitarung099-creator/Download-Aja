using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private bool _clipboardDialogOpen;
    private string? _lastClipboardUrl;

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

    public async Task EnqueueUrlAsync(string url, DownloadRequestContext? requestContext = null)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;

        try
        {
            EngineStatusText.Text = "Menambahkan download...";
            var item = await _viewModel.AddAndStartAsync(uri.AbsoluteUri, requestContext);
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
            UpdateStatusBar();
            _refreshTimer.Start();

            var scheduledResult = await _viewModel.TryRunScheduledQueueAsync(DateTimeOffset.Now);
            if (scheduledResult.HasValue)
                EngineStatusText.Text = $"Scheduler menjalankan {scheduledResult.Value} item antrean";
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
                dialog.StartImmediately,
                dialog.OutputFileName);

            SelectItem(item);
            EngineStatusText.Text = dialog.StartImmediately ? "Download dimulai" : "Masuk antrean";
            UpdateStatusBar();
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

    private async void StartQueue_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            EngineStatusText.Text = "Memulai antrean...";
            var count = await _viewModel.StartQueuedAsync();
            EngineStatusText.Text = count == 0
                ? "Tidak ada item dalam antrean"
                : $"{count} item antrean dimulai";
            UpdateStatusBar();
        }
        catch (Exception ex)
        {
            EngineStatusText.Text = "Gagal memulai antrean";
            MessageBox.Show(this, ex.Message, "Download Aja",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StopQueue_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.StopQueue();
        EngineStatusText.Text = "Antrean dihentikan — download aktif tetap berjalan";
        UpdateStatusBar();
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

    private async void Scheduler_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SchedulerWindow(
            _viewModel.SchedulerEnabled ? _viewModel.ScheduledQueueStartAt : null)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            await _viewModel.ConfigureSchedulerAsync(dialog.ScheduledAt);
            EngineStatusText.Text = dialog.ScheduledAt.HasValue
                ? $"Scheduler aktif: {dialog.ScheduledAt.Value.LocalDateTime:dd/MM/yyyy HH:mm}"
                : "Scheduler dinonaktifkan";
            UpdateStatusBar();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Download Aja",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.UnicodeText)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.UnicodeText))
            return;

        var text = e.Data.GetData(DataFormats.UnicodeText) as string;
        if (string.IsNullOrWhiteSpace(text))
            return;

        var urls = text
            .Split(['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null)
            .Where(uri => uri is not null && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .Select(uri => uri!.AbsoluteUri)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();

        if (urls.Length == 0)
        {
            EngineStatusText.Text = "Drop tidak berisi URL HTTP/HTTPS";
            return;
        }

        EngineStatusText.Text = $"Menambahkan {urls.Length} URL...";
        foreach (var url in urls)
            await EnqueueUrlAsync(url);

        EngineStatusText.Text = $"{urls.Length} URL ditambahkan";
    }

    private void BrowserIntegration_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new BrowserIntegrationWindow
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(
            _viewModel.ConnectionsPerDownload,
            _viewModel.SpeedLimitBytesPerSecond,
            _viewModel.MaxSimultaneousDownloads,
            _viewModel.ClipboardMonitoringEnabled)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            await _viewModel.UpdateSettingsAsync(
                dialog.ConnectionsPerDownload,
                dialog.SpeedLimitBytesPerSecond,
                dialog.MaxSimultaneousDownloads,
                dialog.ClipboardMonitoringEnabled);

            EngineStatusText.Text = $"Pengaturan disimpan — {dialog.ConnectionsPerDownload} koneksi/download";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Download Aja",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var focusedInEditor = Keyboard.FocusedElement is TextBox or ComboBox;

        if (ctrl && e.Key == Key.N)
        {
            e.Handled = true;
            AddUrl_Click(sender, new RoutedEventArgs());
            return;
        }

        if (ctrl && e.Key == Key.F)
        {
            e.Handled = true;
            SearchBox.Focus();
            SearchBox.SelectAll();
            return;
        }

        if (ctrl && e.Key == Key.B)
        {
            e.Handled = true;
            BrowserIntegration_Click(sender, new RoutedEventArgs());
            return;
        }

        if (focusedInEditor)
            return;

        if (DownloadsGrid.SelectedItem is not DownloadItem item)
            return;

        if (e.Key == Key.Space)
        {
            e.Handled = true;
            if (item.Status == DownloadStatus.Mengunduh)
                await RunItemActionAsync(() => _viewModel.PauseAsync(item), "Menjeda download...");
            else if (item.Status != DownloadStatus.Selesai)
                await RunItemActionAsync(() => _viewModel.StartAsync(item), "Memulai download...");
            return;
        }

        if (e.Key == Key.Enter && item.Status == DownloadStatus.Selesai)
        {
            e.Handled = true;
            OpenFile(item);
            return;
        }

        if (e.Key == Key.Delete)
        {
            e.Handled = true;
            var answer = MessageBox.Show(
                this,
                $"Hapus \"{item.Name}\" dari daftar?\n\nFile yang sudah terunduh tidak akan dihapus dari disk.",
                "Download Aja",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer == MessageBoxResult.Yes)
                await RunItemActionAsync(() => _viewModel.RemoveAsync(item), "Menghapus dari daftar...");
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

            var scheduledResult = await _viewModel.TryRunScheduledQueueAsync(DateTimeOffset.Now);
            if (scheduledResult.HasValue)
                EngineStatusText.Text = $"Scheduler menjalankan {scheduledResult.Value} item antrean";

            UpdateStatusBar();
            await TryOpenClipboardUrlDialogAsync();
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

    private async Task TryOpenClipboardUrlDialogAsync()
    {
        if (!_viewModel.ClipboardMonitoringEnabled || _clipboardDialogOpen)
            return;

        string text;
        try
        {
            if (!Clipboard.ContainsText())
                return;

            text = Clipboard.GetText().Trim();
        }
        catch
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text) ||
            string.Equals(text, _lastClipboardUrl, StringComparison.Ordinal))
            return;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;

        _lastClipboardUrl = text;

        if (_viewModel.Downloads.Any(x =>
            string.Equals(x.Url, uri.AbsoluteUri, StringComparison.OrdinalIgnoreCase)))
            return;

        _clipboardDialogOpen = true;
        try
        {
            var dialog = new AddUrlWindow(_viewModel.DownloadDirectory, uri.AbsoluteUri)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
                return;

            await AddFromDialogAsync(dialog);
        }
        finally
        {
            _clipboardDialogOpen = false;
        }
    }

    private async Task AddFromDialogAsync(AddUrlWindow dialog)
    {
        try
        {
            EngineStatusText.Text = dialog.StartImmediately
                ? "Menambahkan download..."
                : "Menambahkan ke antrean...";

            var item = await _viewModel.AddAsync(
                dialog.DownloadUrl,
                dialog.DirectoryPath,
                dialog.StartImmediately,
                dialog.OutputFileName);

            SelectItem(item);
            EngineStatusText.Text = dialog.StartImmediately ? "Download dimulai" : "Masuk antrean";
            UpdateStatusBar();
        }
        catch (Exception ex)
        {
            EngineStatusText.Text = "Gagal";
            MessageBox.Show(this, ex.Message, "Download Aja",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DownloadsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DownloadsGrid.SelectedItem is DownloadItem item &&
            item.Status == DownloadStatus.Selesai)
        {
            OpenFile(item);
        }
    }

    private void OpenFileMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DownloadsGrid.SelectedItem is DownloadItem item)
            OpenFile(item);
    }

    private void OpenFolderMenu_Click(object sender, RoutedEventArgs e)
        => OpenFolder_Click(sender, e);

    private void CopyUrlMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DownloadsGrid.SelectedItem is not DownloadItem item ||
            string.IsNullOrWhiteSpace(item.Url))
            return;

        try
        {
            Clipboard.SetText(item.Url);
            EngineStatusText.Text = "URL disalin";
        }
        catch
        {
            EngineStatusText.Text = "Clipboard sedang tidak tersedia";
        }
    }

    private async void StartMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DownloadsGrid.SelectedItem is not DownloadItem item)
            return;

        await RunItemActionAsync(() => _viewModel.StartAsync(item), "Memulai download...");
    }

    private async void PauseMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DownloadsGrid.SelectedItem is not DownloadItem item)
            return;

        await RunItemActionAsync(() => _viewModel.PauseAsync(item), "Menjeda download...");
    }

    private void OpenFile(DownloadItem item)
    {
        if (string.IsNullOrWhiteSpace(item.FilePath) || !File.Exists(item.FilePath))
        {
            EngineStatusText.Text = "File belum tersedia";
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = item.FilePath,
            UseShellExecute = true
        });
    }

    private async Task RunItemActionAsync(Func<Task> action, string status)
    {
        try
        {
            EngineStatusText.Text = status;
            await action();
            EngineStatusText.Text = "Siap";
            UpdateStatusBar();
        }
        catch (Exception ex)
        {
            EngineStatusText.Text = "Gagal";
            MessageBox.Show(this, ex.Message, "Download Aja",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateStatusBar()
    {
        SpeedStatusText.Text = $"Kecepatan: {FormatBytes(_viewModel.TotalSpeed)}/s";
        ActiveStatusText.Text = $"Aktif: {_viewModel.ActiveCount}";
        QueueStatusText.Text = _viewModel.QueueRunning
            ? $"Antrean: {_viewModel.QueuedCount} (jalan, maks {_viewModel.MaxSimultaneousDownloads})"
            : $"Antrean: {_viewModel.QueuedCount}";
        SchedulerStatusText.Text = _viewModel.SchedulerEnabled && _viewModel.ScheduledQueueStartAt.HasValue
            ? $"Scheduler: {_viewModel.ScheduledQueueStartAt.Value.LocalDateTime:dd/MM HH:mm}"
            : "Scheduler: nonaktif";
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
