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

    public async Task<string> AddUriAsync(
        string url,
        string directory,
        int connections = 8,
        string? outputFileName = null,
        long speedLimitBytesPerSecond = 0,
        string? referer = null,
        string? userAgent = null,
        string? cookieHeader = null,
        CancellationToken ct = default)
    {
        var safeConnections = Math.Clamp(connections, 1, 16);
        var options = new Dictionary<string, object>
        {
            ["dir"] = directory,
            ["continue"] = "true",
            ["split"] = safeConnections.ToString(),
            ["max-connection-per-server"] = safeConnections.ToString(),
            ["min-split-size"] = "1M",
            ["file-allocation"] = "none",
            ["auto-file-renaming"] = "false",
            ["allow-overwrite"] = "false",
            ["max-tries"] = "5",
            ["retry-wait"] = "3",
            ["connect-timeout"] = "30",
            ["timeout"] = "60"
        };

        if (!string.IsNullOrWhiteSpace(outputFileName))
            options["out"] = outputFileName;

        if (speedLimitBytesPerSecond > 0)
            options["max-download-limit"] = speedLimitBytesPerSecond.ToString();

        if (!string.IsNullOrWhiteSpace(referer))
            options["referer"] = SanitizeOptionValue(referer, 4096);

        if (!string.IsNullOrWhiteSpace(userAgent))
            options["user-agent"] = SanitizeOptionValue(userAgent, 4096);

        if (!string.IsNullOrWhiteSpace(cookieHeader))
            options["header"] = new[] { $"Cookie: {SanitizeOptionValue(cookieHeader, 262144)}" };

        var result = await CallAsync("aria2.addUri", WithToken([new[] { url }, options]), ct);
        return result.GetString() ?? throw new InvalidOperationException("aria2 tidak mengembalikan GID.");
    }

    public Task PauseAsync(string gid, CancellationToken ct = default)
        => CallNoResultAsync("aria2.pause", WithToken([gid]), ct);

    public Task ForcePauseAsync(string gid, CancellationToken ct = default)
        => CallNoResultAsync("aria2.forcePause", WithToken([gid]), ct);

    public Task UnpauseAsync(string gid, CancellationToken ct = default)
        => CallNoResultAsync("aria2.unpause", WithToken([gid]), ct);

    public Task ForceRemoveAsync(string gid, CancellationToken ct = default)
        => CallNoResultAsync("aria2.forceRemove", WithToken([gid]), ct);

    public async Task<JsonElement> TellStatusAsync(string gid, CancellationToken ct = default)
        => await CallAsync("aria2.tellStatus", WithToken([gid,
            new[] { "gid", "status", "totalLength", "completedLength", "downloadSpeed", "files", "errorCode", "errorMessage" }]), ct);

    public Task<JsonElement> GetVersionAsync(CancellationToken ct = default)
        => CallAsync("aria2.getVersion", WithToken([]), ct);

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

    private static string SanitizeOptionValue(string value, int maxLength)
    {
        var clean = value.Replace("\r", "").Replace("\n", "");
        return clean.Length <= maxLength ? clean : clean[..maxLength];
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
