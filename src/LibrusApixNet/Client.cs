using System.Net;
using System.Text.Json;

namespace LibrusApixNet;

public sealed class Token
{
    public Token(string? apiKey = null, string? dzienniks = null, string? sdzienniks = null)
    {
        ApiKey = !string.IsNullOrEmpty(apiKey) ? apiKey :
            !string.IsNullOrEmpty(dzienniks) && !string.IsNullOrEmpty(sdzienniks) ? $"{dzienniks}:{sdzienniks}" : "";
    }

    public string ApiKey { get; set; }
    public string CsrfToken { get; set; } = "";
    public string OAuth { get; set; } = "";

    public IReadOnlyDictionary<string, string> AccessCookies()
    {
        var parts = ApiKey.Split(':');
        if (parts.Length != 2)
            throw new TokenKeyException("ApiKey must be in the format 'DZIENNIKSID:SDZIENNIKSID'.");
        return new Dictionary<string, string> { ["DZIENNIKSID"] = parts[0], ["SDZIENNIKSID"] = parts[1] };
    }

    public override string ToString() => ApiKey;
}

/// <summary>Reusable HTTP session. Dispose when finished. The supplied handler is owned by this client.</summary>
public sealed class Client : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _manualCookies;

    public Client(Token? token = null, ClientOptions? options = null, HttpMessageHandler? handler = null,
        CookieContainer? cookies = null)
    {
        Token = token ?? new Token();
        Options = options ?? new ClientOptions();
        Cookies = cookies ?? new CookieContainer();
        _manualCookies = handler is not null;
        handler ??= new HttpClientHandler
        {
            CookieContainer = Cookies,
            AutomaticDecompression = DecompressionMethods.All,
            Proxy = Options.Proxy,
            UseProxy = Options.Proxy is not null
        };
        _http = new HttpClient(handler) { Timeout = Options.Timeout };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", Options.UserAgent);
    }

    public Token Token { get; set; }
    public ClientOptions Options { get; }
    public CookieContainer Cookies { get; }

    public static Client NewClient(Token? token = null, ClientOptions? options = null) => new(token, options);

    public async Task<Token> GetTokenAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        using (var maintenance = await SendAsync(HttpMethod.Get, Options.ApiUrl, null, false, cancellationToken))
        {
            if (maintenance.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                var message = "maintenance";
                try
                {
                    var json = await ReadJsonAsync(maintenance, cancellationToken);
                    if (json.TryGetProperty("Message", out var messages) && messages.ValueKind == JsonValueKind.Array && messages.GetArrayLength() > 0)
                        message = messages[0].GetProperty("description").GetString() ?? message;
                }
                catch (JsonException) { }
                throw new MaintenanceException(message);
            }
            maintenance.EnsureSuccessStatusCode();
        }
        using (var portal = await SendAsync(HttpMethod.Get, Options.LoginPortalUrl, null, false, cancellationToken))
            portal.EnsureSuccessStatusCode();
        using var response = await SendAsync(HttpMethod.Post, Options.AuthorizationUrl,
            new Dictionary<string, string> { ["action"] = "login", ["login"] = username, ["pass"] = password }, false, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await ReadJsonAsync(response, cancellationToken);
        using (var twoFactor = await SendAsync(HttpMethod.Get, Options.TwoFactorUrl, null, false, cancellationToken))
            twoFactor.EnsureSuccessStatusCode();
        if (result.TryGetProperty("status", out var status) && status.GetString() == "error")
            throw new AuthorizationException(result.GetProperty("errors")[0].GetProperty("message").GetString() ?? "Authorization failed.");
        var dzienniks = FindCookie("DZIENNIKSID");
        var sdzienniks = FindCookie("SDZIENNIKSID");
        if (dzienniks is null || sdzienniks is null)
            throw new AuthorizationException("Authorization cookies were not found.");
        return Token = new Token(dzienniks: dzienniks, sdzienniks: sdzienniks);
    }

    public async Task<string> RefreshOAuthAsync(CancellationToken cancellationToken = default)
    {
        using var response = await GetAsync(Options.RefreshOAuthUrl, cancellationToken);
        var issuedOAuth = response.Headers.TryGetValues("Set-Cookie", out var headers) &&
            headers.Any(h => h.TrimStart().StartsWith("oauth_token=", StringComparison.Ordinal));
        var oauth = Cookies.GetCookies(new Uri(Options.RefreshOAuthUrl))["oauth_token"]?.Value;
        if (response.StatusCode != HttpStatusCode.OK || !issuedOAuth || string.IsNullOrEmpty(oauth))
            throw new AuthorizationException("Error while refreshing OAuth token.");
        Token.OAuth = oauth;
        return oauth;
    }

    /// <summary>The caller owns and must dispose the response.</summary>
    public Task<HttpResponseMessage> GetAsync(string url, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, url, null, true, cancellationToken);

    /// <summary>Repeated keys are supported, e.g. DoKogo for multiple recipients. Caller disposes the response.</summary>
    public Task<HttpResponseMessage> PostAsync(string url, IEnumerable<KeyValuePair<string, string>> data,
        CancellationToken cancellationToken = default) => SendAsync(HttpMethod.Post, url, data, true, cancellationToken);

    internal async Task<string> GetTextAsync(string url, CancellationToken ct)
    {
        using var response = await GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    internal async Task<string> PostTextAsync(string url, IEnumerable<KeyValuePair<string, string>> data, CancellationToken ct)
    {
        using var response = await PostAsync(url, data, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    internal async Task<JsonElement> GetJsonAsync(string url, CancellationToken ct)
    {
        using var response = await GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response, ct);
    }

    internal static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return document.RootElement.Clone();
    }

    private string? FindCookie(string name) => Cookies.GetAllCookies().Cast<Cookie>().FirstOrDefault(c => c.Name == name)?.Value;

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url,
        IEnumerable<KeyValuePair<string, string>>? data, bool authenticated, CancellationToken ct)
    {
        var uri = new Uri(url);
        if (authenticated)
        {
            var baseUri = new Uri(Options.BaseUrl);
            foreach (var cookie in Token.AccessCookies())
                SetSessionCookie(baseUri, cookie.Key, cookie.Value);
            if (!string.IsNullOrEmpty(Token.OAuth))
                SetSessionCookie(baseUri, "oauth_token", Token.OAuth);
            else
                foreach (Cookie cookie in Cookies.GetCookies(baseUri))
                    if (cookie.Name == "oauth_token") cookie.Expired = true;
        }
        using var request = new HttpRequestMessage(method, uri);
        if (data is not null) request.Content = new FormUrlEncodedContent(data);
        if (_manualCookies)
        {
            var header = Cookies.GetCookieHeader(uri);
            if (header.Length > 0) request.Headers.TryAddWithoutValidation("Cookie", header);
        }
        var response = await _http.SendAsync(request, ct);
        if (response.Headers.TryGetValues("Set-Cookie", out var values))
            foreach (var value in values) Cookies.SetCookies(response.RequestMessage?.RequestUri ?? uri, value);
        return response;
    }

    private void SetSessionCookie(Uri uri, string name, string value)
    {
        var existing = Cookies.GetCookies(uri).Cast<Cookie>().Where(c => c.Name == name).ToList();
        if (existing.Count == 0) Cookies.Add(uri, new Cookie(name, value, "/"));
        else foreach (var cookie in existing) cookie.Value = value;
    }

    public void Dispose() => _http.Dispose();
}
