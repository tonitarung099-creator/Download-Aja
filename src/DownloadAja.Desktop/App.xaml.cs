using System.Windows;

namespace DownloadAja.Desktop;

public partial class App : Application
{
    private SingleInstanceCoordinator? _singleInstance;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstanceCoordinator();

        var url = GetArgumentValue(e.Args, "--add-url");
        var message = string.IsNullOrWhiteSpace(url) ? "__ACTIVATE__" : url;

        if (!_singleInstance.IsPrimary)
        {
            _ = await SingleInstanceCoordinator.SendToPrimaryAsync(message);
            Shutdown();
            return;
        }

        var main = new MainWindow();
        MainWindow = main;

        _singleInstance.MessageReceived += async incoming =>
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                if (string.Equals(incoming, "__ACTIVATE__", StringComparison.Ordinal))
                {
                    if (main.WindowState == WindowState.Minimized)
                        main.WindowState = WindowState.Normal;
                    main.Activate();
                    return;
                }

                await main.EnqueueUrlAsync(incoming);
            });
        };

        main.Show();

        if (!string.IsNullOrWhiteSpace(url))
            _ = main.EnqueueUrlAsync(url);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_singleInstance is not null)
            await _singleInstance.DisposeAsync();

        base.OnExit(e);
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
