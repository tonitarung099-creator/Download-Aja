using System.IO;
using System.IO.Pipes;
using System.Text;

namespace DownloadAja.Desktop;

public sealed class SingleInstanceCoordinator : IAsyncDisposable
{
    public const string MutexName = "Local\\DownloadAja.SingleInstance";
    public const string PipeName = "DownloadAja.UrlPipe.v1";

    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cts = new();
    private Task? _serverTask;

    public bool IsPrimary { get; }

    public event Func<string, Task>? MessageReceived;

    public SingleInstanceCoordinator()
    {
        _mutex = new Mutex(initiallyOwned: false, MutexName, out var createdNew);
        IsPrimary = createdNew;

        if (IsPrimary)
            _serverTask = RunServerAsync(_cts.Token);
    }

    public static async Task<bool> SendToPrimaryAsync(string message, int timeoutMs = 1500)
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(
                    ".",
                    PipeName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous);

                using var timeout = new CancellationTokenSource(timeoutMs);
                await pipe.ConnectAsync(timeout.Token);

                await using var writer = new StreamWriter(pipe, new UTF8Encoding(false))
                {
                    AutoFlush = true
                };

                await writer.WriteLineAsync(message);
                return true;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or OperationCanceledException)
            {
                await Task.Delay(120);
            }
        }

        return false;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();

        if (_serverTask is not null)
        {
            try
            {
                await _serverTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts.Dispose();

        _mutex.Dispose();
    }

    private async Task RunServerAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await using var server = new NamedPipeServerStream(
                PipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(ct);

                using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                var message = await reader.ReadLineAsync(ct);
                if (!string.IsNullOrWhiteSpace(message) && MessageReceived is not null)
                    await MessageReceived.Invoke(message);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Server loop berikutnya tetap berjalan.
            }
        }
    }
}
