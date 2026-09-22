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

    public void EnqueueUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            _viewModel.AddPlaceholder(uri.AbsoluteUri);
            Activate();
        }
    }

    private void AddUrl_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddUrlWindow { Owner = this };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.DownloadUrl))
            EnqueueUrl(dialog.DownloadUrl);
    }
}
