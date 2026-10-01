using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace ShortifyKit.Tests;

internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    private readonly ConcurrentQueue<CapturedRequest> _requests = new();

    public IReadOnlyList<CapturedRequest> Requests => _requests.ToArray();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        _requests.Enqueue(new CapturedRequest(request.Method, request.RequestUri, request.Headers.ToDictionary(x => x.Key, x => x.Value.ToArray()), body));
        return await responder(request, cancellationToken);
    }
}

internal sealed record CapturedRequest(
    HttpMethod Method,
    Uri? Uri,
    IReadOnlyDictionary<string, string[]> Headers,
    string? Body);

internal static class StubResponses
{
    public static HttpResponseMessage Json(HttpStatusCode statusCode, string body)
        => new(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Ok(string body)
        => Json(HttpStatusCode.OK, body);

    public const string Link = """
    {
      "success": true,
      "data": {
        "id": "6f1d2b8e-4c7a-4f2e-9a51-0d3c7e9b1a24",
        "shortUrl": "https://shfy.link/A7kd92x",
        "domain": "shfy.link",
        "code": "A7kd92x",
        "destinationUrl": "https://example.com/spring",
        "title": "Spring sale",
        "redirectType": "Temporary",
        "status": "Active",
        "isCustomAlias": false,
        "expiresAtUTC": null,
        "isExpired": false,
        "utm": { "source": "newsletter", "medium": "email" },
        "tags": [ "spring" ],
        "externalId": "order-42",
        "clicks": 17,
        "lastClickedAtUTC": "2026-09-30T10:00:00Z",
        "createdAtUTC": "2026-09-01T08:30:00Z",
        "version": 3,
        "accountId": "00000000-0000-0000-0000-000000000000"
      }
    }
    """;
}