using System.Net.Http.Json;
using System.Text.Json;

namespace DownloadAja.Core.Services;

public sealed class Aria2RpcClient
{
    private readonly HttpClient _http;
    private readonly string? _secret;
    private long _requestId;

    public Aria2RpcClient(HttpClient http, string endpoint = "http://127.0.0.1:6800/jsonrpc", string? secret = null)
    {
        _http = http;
        _http.BaseAddress = new Uri(endpoint);
        _secret = secret;
    }

    public async Task<string> AddUriAsync(string url, string directory, int connections = 8, CancellationToken ct = default)
    {
        var options = new Dictionary<string, string>
        {
            ["dir"] = directory,
            ["continue"] = "true",
            ["split"] = Math.Clamp(connections, 1, 16).ToString(),
            ["max-connection-per-server"] = Math.Clamp(connections, 1, 16).ToString(),
            ["min-split-size"] = "1M",
            ["file-allocation"] = "none",
            ["auto-file-renaming"] = "false"
        };

        var parameters = WithToken(new object[] { new[] { url }, options });
        var result = await CallAsync("aria2.addUri", parameters, ct);
        return result.GetString() ?? throw new InvalidOperationException("aria2 tidak mengembalikan GID.");
    }

    public Task PauseAsync(string gid, CancellationToken ct = default)
        => CallNoResultAsync("aria2.pause", WithToken(new object[] { gid }), ct);

    public Task UnpauseAsync(string gid, CancellationToken ct = default)
        => CallNoResultAsync("aria2.unpause", WithToken(new object[] { gid }), ct);

    public Task RemoveAsync(string gid, CancellationToken ct = default)
        => CallNoResultAsync("aria2.remove", WithToken(new object[] { gid }), ct);

    public async Task<JsonElement> TellStatusAsync(string gid, CancellationToken ct = default)
        => await CallAsync("aria2.tellStatus", WithToken(new object[] { gid }), ct);

    private async Task CallNoResultAsync(string method, object[] parameters, CancellationToken ct)
        => _ = await CallAsync(method, parameters, ct);

    private async Task<JsonElement> CallAsync(string method, object[] parameters, CancellationToken ct)
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = Interlocked.Increment(ref _requestId).ToString(),
            method,
            @params = parameters
        };

        using var response = await _http.PostAsJsonAsync("", request, ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));

        if (doc.RootElement.TryGetProperty("error", out var error))
            throw new InvalidOperationException(error.ToString());

        return doc.RootElement.GetProperty("result").Clone();
    }

    private object[] WithToken(object[] parameters)
    {
        if (string.IsNullOrWhiteSpace(_secret)) return parameters;
        var result = new object[parameters.Length + 1];
        result[0] = $"token:{_secret}";
        Array.Copy(parameters, 0, result, 1, parameters.Length);
        return result;
    }
}
