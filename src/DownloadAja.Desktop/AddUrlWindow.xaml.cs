using System.Windows;

namespace DownloadAja.Desktop;

public partial class AddUrlWindow : Window
{
    public string DownloadUrl => UrlBox.Text.Trim();

    public AddUrlWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText().Trim();
                if (Uri.TryCreate(text, UriKind.Absolute, out _))
                    UrlBox.Text = text;
            }
            UrlBox.Focus();
            UrlBox.SelectAll();
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!Uri.TryCreate(DownloadUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(this, "Masukkan URL HTTP/HTTPS yang valid.", "Download Aja",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
