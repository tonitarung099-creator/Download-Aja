using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace DownloadAja.Core.Services;

public sealed class Aria2EngineHost : IAsyncDisposable
{
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private Process? _process;
    private HttpClient? _httpClient;

    public Aria2RpcClient? Client { get; private set; }
    public bool IsRunning => _process is { HasExited: false } && Client is not null;

    public async Task<Aria2RpcClient> EnsureStartedAsync(CancellationToken ct = default)
    {
        if (IsRunning) return Client!;

        await _startLock.WaitAsync(ct);
        try
        {
            if (IsRunning) return Client!;

            var exePath = Path.Combine(AppContext.BaseDirectory, "tools", "aria2", "aria2c.exe");
            if (!File.Exists(exePath))
                throw new FileNotFoundException(
                    "Mesin download aria2c.exe tidak ditemukan. Gunakan build portable dari GitHub Actions.",
                    exePath);

            var port = GetAvailablePort();
            var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath)!,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("--enable-rpc=true");
            psi.ArgumentList.Add("--rpc-listen-all=false");
            psi.ArgumentList.Add($"--rpc-listen-port={port}");
            psi.ArgumentList.Add($"--rpc-secret={secret}");
            psi.ArgumentList.Add("--max-concurrent-downloads=5");
            psi.ArgumentList.Add("--continue=true");
            psi.ArgumentList.Add("--summary-interval=0");
            psi.ArgumentList.Add("--console-log-level=warn");
            psi.ArgumentList.Add("--log-level=warn");

            _process = Process.Start(psi)
                ?? throw new InvalidOperationException("Gagal menjalankan aria2.");

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(2)
            };

            var client = new Aria2RpcClient(
                _httpClient,
                $"http://127.0.0.1:{port}/jsonrpc",
                secret);

            Exception? lastError = null;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                if (_process.HasExited)
                    throw new InvalidOperationException($"aria2 berhenti saat start (kode {_process.ExitCode}).");

                try
                {
                    _ = await client.GetVersionAsync(ct);
                    Client = client;
                    return client;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    lastError = ex;
                    await Task.Delay(100, ct);
                }
            }

            await StopAsync();
            throw new InvalidOperationException("aria2 tidak merespons RPC lokal.", lastError);
        }
        finally
        {
            _startLock.Release();
        }
    }

    public async Task StopAsync()
    {
        Client = null;
        _httpClient?.Dispose();
        _httpClient = null;

        if (_process is null) return;

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
        }
        catch
        {
            // Shutdown best-effort.
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _startLock.Dispose();
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
