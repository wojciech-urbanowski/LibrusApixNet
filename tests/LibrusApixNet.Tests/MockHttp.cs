using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace LibrusApixNet.Tests;

internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string Body, string Cookie);

internal sealed class MockHttp(Func<RecordedRequest, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
{
    public ConcurrentQueue<RecordedRequest> Requests { get; } = new();

    public MockHttp(Func<RecordedRequest, HttpResponseMessage> callback) : this((request, _) => Task.FromResult(callback(request))) { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = new RecordedRequest(request.Method, request.RequestUri!,
            request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken),
            request.Headers.TryGetValues("Cookie", out var cookies) ? string.Join("; ", cookies) : "");
        Requests.Enqueue(snapshot);
        return await callback(snapshot, cancellationToken);
    }

    public static HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK, string mediaType = "text/html") =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };

    public static ClientOptions Options => new() { BaseUrl = "https://school.test", ApiUrl = "https://api.test" };
    public static Client Create(Func<RecordedRequest, HttpResponseMessage> callback) => new(new Token("first:second"), Options, new MockHttp(callback));
}
