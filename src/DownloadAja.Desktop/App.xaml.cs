using System.Windows;

namespace DownloadAja.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var main = new MainWindow();
        MainWindow = main;
        main.Show();

        var url = GetArgumentValue(e.Args, "--add-url");
        if (!string.IsNullOrWhiteSpace(url))
            main.EnqueueUrl(url);
    }

    private static string? GetArgumentValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }
}
