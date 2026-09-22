using System.Threading.Tasks;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using DownloadAja.Core.Models;

namespace DownloadAja.Desktop;

public partial class App : Application
{
    private SingleInstanceCoordinator? _singleInstance;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

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

                if (TryParseBrowserRequest(incoming, out var requestUrl, out var requestContext))
                    await main.EnqueueUrlAsync(requestUrl, requestContext);
                else
                    await main.EnqueueUrlAsync(incoming);
            });
        };

        main.Show();

        if (!string.IsNullOrWhiteSpace(url))
            _ = main.EnqueueUrlAsync(url);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

        if (_singleInstance is not null)
            await _singleInstance.DisposeAsync();

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var path = CrashLogger.Log("WPF Dispatcher", e.Exception);
        e.Handled = true;

        var location = string.IsNullOrWhiteSpace(path)
            ? "Log gagal ditulis."
            : $"Log: {path}";

        MessageBox.Show(
            $"Download Aja mengalami error yang tidak tertangani.\n\n{location}",
            "Download Aja",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        Shutdown(-1);
    }

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
            CrashLogger.Log("AppDomain", exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        CrashLogger.Log("TaskScheduler", e.Exception);
        e.SetObserved();
    }

    private static bool TryParseBrowserRequest(
        string message,
        out string url,
        out DownloadRequestContext? context)
    {
        url = "";
        context = null;

        if (!message.TrimStart().StartsWith("{", StringComparison.Ordinal))
            return false;

        try
        {
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;

            if (!root.TryGetProperty("type", out var type) ||
                !string.Equals(type.GetString(), "addDownload", StringComparison.Ordinal))
                return false;

            if (!root.TryGetProperty("url", out var urlElement))
                return false;

            var candidate = urlElement.GetString();
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return false;

            url = uri.AbsoluteUri;
            context = new DownloadRequestContext(
                ReadOptionalString(root, "referrer"),
                ReadOptionalString(root, "userAgent"),
                ReadOptionalString(root, "cookieHeader"),
                ReadOptionalString(root, "mediaKind"),
                ReadOptionalString(root, "suggestedName"),
                ReadOptionalString(root, "formatProfile"));

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
            return null;

        return value.GetString();
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
