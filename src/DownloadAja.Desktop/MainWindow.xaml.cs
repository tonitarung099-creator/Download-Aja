using System.Windows;
using DownloadAja.Desktop.ViewModels;

namespace DownloadAja.Desktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void AddUrl_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddUrlWindow { Owner = this };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.DownloadUrl))
            _viewModel.AddPlaceholder(dialog.DownloadUrl);
    }
}
