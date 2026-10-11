using System.Net;

namespace LibrusApixNet.Tests;

public sealed class ClientTests
{
    [Fact]
    public void TokenSupportsBothFormsAndValidatesAtAccess()
    {
        var token = new Token(dzienniks: "a", sdzienniks: "b");
        Assert.Equal("a:b", token.ToString()); Assert.Equal("a", token.AccessCookies()["DZIENNIKSID"]);
        Assert.Equal("x:y", new Token("x:y", "ignored", "ignored").ApiKey);
        Assert.Throws<TokenKeyException>(() => new Token().AccessCookies());
        Assert.Throws<TokenKeyException>(() => new Token("a:b:c").AccessCookies());
    }

    [Fact]
    public async Task LoginRunsCompleteHandshakeAndCapturesCookies()
    {
        var handler = new MockHttp(request =>
        {
            var response = MockHttp.Response(request.Uri.AbsolutePath.EndsWith("Authorization") ? "{\"status\":\"ok\"}" : "{}");
            if (request.Uri.AbsolutePath.Contains("portalRodzina"))
            {
                response.Headers.Add("Set-Cookie", "DZIENNIKSID=abc; Path=/; Secure; HttpOnly");
                response.Headers.Add("Set-Cookie", "SDZIENNIKSID=def; Path=/; Secure; HttpOnly");
            }
            return response;
        });
        using var client = new Client(options: MockHttp.Options, handler: handler);
        var token = await client.GetTokenAsync("user", "p&ss+");
        Assert.Same(client.Token, token); Assert.Equal("abc:def", token.ApiKey);
        Assert.Equal(4, handler.Requests.Count);
        var request = handler.Requests.ElementAt(2);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("?client_id=46", request.Uri.Query);
        Assert.Equal("action=login&login=user&pass=p%26ss%2B", request.Body);
        Assert.DoesNotContain("DZIENNIKSID", handler.Requests.Last().Cookie); // synergia cookies must not leak to api.test
        using var response = await client.GetAsync(client.Options.InfoUrl);
        Assert.Contains("DZIENNIKSID=abc", handler.Requests.Last().Cookie);
        Assert.Contains("SDZIENNIKSID=def", handler.Requests.Last().Cookie);
    }

    [Theory]
    [InlineData("{\"Message\":[]}", "maintenance")]
    [InlineData("{\"Message\":[{\"description\":\"Przerwa techniczna\"}]}", "Przerwa techniczna")]
    [InlineData("Service unavailable", "maintenance")]
    public async Task MaintenanceStopsLogin(string body, string expected)
    {
        var handler = new MockHttp(_ => MockHttp.Response(body, HttpStatusCode.ServiceUnavailable));
        using var client = new Client(options: MockHttp.Options, handler: handler);
        var error = await Assert.ThrowsAsync<MaintenanceException>(() => client.GetTokenAsync("user", "pass"));
        Assert.Equal(expected, error.Message); Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task LoginReportsBadCredentials()
    {
        using var client = new Client(options: MockHttp.Options, handler: new MockHttp(request => MockHttp.Response(
            request.Method == HttpMethod.Post ? "{\"status\":\"error\",\"errors\":[{\"message\":\"Niepoprawne hasło\"}]}" : "{}")));
        var error = await Assert.ThrowsAsync<AuthorizationException>(() => client.GetTokenAsync("user", "pass"));
        Assert.Equal("Niepoprawne hasło", error.Message);
    }

    [Fact]
    public async Task LoginRejectsMissingAuthorizationCookies()
    {
        using var client = new Client(options: MockHttp.Options, handler: new MockHttp(_ => MockHttp.Response("{\"status\":\"ok\"}")));
        await Assert.ThrowsAsync<AuthorizationException>(() => client.GetTokenAsync("user", "pass"));
    }

    [Fact]
    public async Task RefreshOAuthStoresAndSendsCookie()
    {
        var handler = new MockHttp(request =>
        {
            var response = MockHttp.Response("{}");
            if (request.Uri.AbsolutePath == "/refreshToken") response.Headers.Add("Set-Cookie", "oauth_token=fresh; Path=/; Secure");
            return response;
        });
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        Assert.Equal("fresh", await client.RefreshOAuthAsync()); Assert.Equal("fresh", client.Token.OAuth);
        using var response = await client.GetAsync(client.Options.GatewayApiAttendanceUrl);
        Assert.Contains("oauth_token=fresh", handler.Requests.Last().Cookie);
        client.Token = new Token("new:token");
        using var next = await client.GetAsync(client.Options.InfoUrl);
        Assert.Contains("DZIENNIKSID=new", handler.Requests.Last().Cookie);
        Assert.Contains("SDZIENNIKSID=token", handler.Requests.Last().Cookie);
        Assert.DoesNotContain("oauth_token", handler.Requests.Last().Cookie);
    }

    [Fact]
    public async Task RefreshOAuthRequiresCookieEvenOnHttp200()
    {
        using var client = MockHttp.Create(_ => MockHttp.Response("{}"));
        await Assert.ThrowsAsync<AuthorizationException>(() => client.RefreshOAuthAsync());
    }

    [Fact]
    public async Task RefreshDoesNotMistakeOldOAuthForNewCookie()
    {
        using var client = MockHttp.Create(_ => MockHttp.Response("{}"));
        client.Token.OAuth = "stale";
        await Assert.ThrowsAsync<AuthorizationException>(() => client.RefreshOAuthAsync());
    }

    [Fact]
    public async Task TokenReplacementUpdatesExistingDomainCookies()
    {
        var handler = new MockHttp(_ => MockHttp.Response(""));
        using var client = new Client(new Token("new:token"), MockHttp.Options, handler);
        client.Cookies.Add(new Cookie("DZIENNIKSID", "old", "/", ".school.test"));
        using var response = await client.GetAsync(client.Options.InfoUrl);
        Assert.Equal("new", client.Cookies.GetCookies(new Uri(client.Options.BaseUrl))["DZIENNIKSID"]?.Value);
        Assert.DoesNotContain("DZIENNIKSID=old", Assert.Single(handler.Requests).Cookie);
    }

    [Fact]
    public async Task ClientsHaveIndependentSessionState()
    {
        using var first = MockHttp.Create(_ => MockHttp.Response(""));
        using var second = MockHttp.Create(_ => MockHttp.Response(""));
        first.Cookies.Add(new Uri(first.Options.BaseUrl), new Cookie("custom", "one", "/"));
        Assert.Null(second.Cookies.GetCookies(new Uri(second.Options.BaseUrl))["custom"]);
        first.Token.OAuth = "one"; Assert.Equal("", second.Token.OAuth);
        using var response = await first.GetAsync(first.Options.InfoUrl);
        Assert.Null(second.Cookies.GetCookies(new Uri(second.Options.BaseUrl))["DZIENNIKSID"]);
    }

    [Fact]
    public async Task CancellationReachesHttpHandler()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new MockHttp(async (_, ct) =>
        {
            entered.SetResult(); await Task.Delay(Timeout.Infinite, ct); return MockHttp.Response("");
        });
        using var client = new Client(new Token("a:b"), MockHttp.Options, handler);
        using var cancellation = new CancellationTokenSource();
        var task = client.GetAsync(client.Options.InfoUrl, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }
}
