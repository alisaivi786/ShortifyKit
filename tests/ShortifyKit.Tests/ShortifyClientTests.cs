using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ShortifyKit.Tests;

public sealed class ShortifyClientTests
{
    private static readonly Guid LinkId = Guid.Parse("6f1d2b8e-4c7a-4f2e-9a51-0d3c7e9b1a24");

    [Fact]
    public async Task CreateLinkAsync_SendsApiKeyIdempotencyKeyAndOnlySetFields()
    {
        StubHttpMessageHandler handler = new((_, _) => Task.FromResult(StubResponses.Ok(StubResponses.Link)));
        IShortifyClient client = CreateClient(handler);

        ShortLink link = await client.CreateLinkAsync(
            new CreateLinkRequest { DestinationUrl = "https://example.com/spring", Tags = ["spring"] },
            "order-42",
            TestContext.Current.CancellationToken);

        Assert.Equal("https://shfy.link/A7kd92x", link.ShortUrl);
        Assert.Equal(RedirectType.Temporary, link.RedirectType);
        Assert.Equal(new DateTime(2026, 9, 1, 8, 30, 0, DateTimeKind.Utc), link.CreatedAtUtc);
        Assert.Equal("newsletter", link.Utm?.Source);

        CapturedRequest request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://gateway.test/shortify/api/v1/links", request.Uri?.ToString());
        Assert.Equal("shfy_secret", Assert.Single(request.Headers["X-Api-Key"]));
        Assert.Equal("order-42", Assert.Single(request.Headers["Idempotency-Key"]));
        Assert.Equal("""{"destinationUrl":"https://example.com/spring","tags":["spring"]}""", request.Body);
    }

    [Fact]
    public async Task CreateLinkAsync_GeneratesAnIdempotencyKeyWhenNoneIsGiven()
    {
        StubHttpMessageHandler handler = new((_, _) => Task.FromResult(StubResponses.Ok(StubResponses.Link)));
        IShortifyClient client = CreateClient(handler);

        _ = await client.CreateLinkAsync(new CreateLinkRequest { DestinationUrl = "https://example.com" }, cancellationToken: TestContext.Current.CancellationToken);

        string key = Assert.Single(Assert.Single(handler.Requests).Headers["Idempotency-Key"]);
        Assert.Equal(32, key.Length);
    }

    [Fact]
    public async Task CreateLinksAsync_WrapsTheLinksInTheBulkBody()
    {
        StubHttpMessageHandler handler = new((_, _) => Task.FromResult(StubResponses.Ok("""{ "success": true, "data": [] }""")));
        IShortifyClient client = CreateClient(handler);

        IReadOnlyList<ShortLink> links = await client.CreateLinksAsync(
            [new CreateLinkRequest { DestinationUrl = "https://a.test" }, new CreateLinkRequest { DestinationUrl = "https://b.test", Alias = "b" }],
            TestContext.Current.CancellationToken);

        Assert.Empty(links);
        CapturedRequest request = Assert.Single(handler.Requests);
        Assert.Equal("https://gateway.test/shortify/api/v1/links/bulk", request.Uri?.ToString());
        Assert.Equal("""{"links":[{"destinationUrl":"https://a.test"},{"destinationUrl":"https://b.test","alias":"b"}]}""", request.Body);
    }

    [Fact]
    public async Task UpdateLinkAsync_SendsPatchWithoutNullFields()
    {
        StubHttpMessageHandler handler = new((_, _) => Task.FromResult(StubResponses.Ok(StubResponses.Link)));
        IShortifyClient client = CreateClient(handler);

        _ = await client.UpdateLinkAsync(
            LinkId,
            new UpdateLinkRequest { DestinationUrl = "https://example.com/new", Tags = [], Version = 3 },
            TestContext.Current.CancellationToken);

        CapturedRequest request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal($"https://gateway.test/shortify/api/v1/links/{LinkId}", request.Uri?.ToString());
        Assert.Equal("""{"destinationUrl":"https://example.com/new","clearExpiry":false,"tags":[],"version":3}""", request.Body);
    }

    [Fact]
    public async Task SetLinkEnabledAsync_PutsTheStatus()
    {
        StubHttpMessageHandler handler = new((_, _) => Task.FromResult(StubResponses.Ok(StubResponses.Link)));
        IShortifyClient client = CreateClient(handler);

        _ = await client.SetLinkEnabledAsync(LinkId, false, TestContext.Current.CancellationToken);

        CapturedRequest request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal($"https://gateway.test/shortify/api/v1/links/{LinkId}/status", request.Uri?.ToString());
        Assert.Equal("""{"enabled":false}""", request.Body);
    }

    [Fact]
    public async Task ListLinksAsync_BuildsTheQueryString()
    {
        StubHttpMessageHandler handler = new((_, _) => Task.FromResult(StubResponses.Ok("""
        { "success": true, "data": { "items": [], "totalCount": 0, "page": 2, "pageSize": 50 } }
        """)));
        IShortifyClient client = CreateClient(handler);

        ShortifyPage<ShortLink> page = await client.ListLinksAsync(
            new LinkListQuery
            {
                Page = 2,
                PageSize = 50,
                Search = "spring sale",
                SortBy = LinkSortBy.Clicks,
                SortDirection = SortDirection.Asc,
                Status = LinkStatus.Active,
                Expired = false,
                CreatedFromUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, page.Page);
        Assert.Equal(
            "https://gateway.test/shortify/api/v1/links?page=2&pageSize=50&search=spring%20sale&sortBy=clicks&sortDirection=Asc&status=Active&expired=false&createdFrom=2026-09-01T00%3A00%3A00.0000000Z",
            Assert.Single(handler.Requests).Uri?.AbsoluteUri);
    }

    [Fact]
    public async Task ListLinksAsync_WithoutAQuerySendsNoQueryString()
    {
        StubHttpMessageHandler handler = new((_, _) => Task.FromResult(StubResponses.Ok("""
        { "success": true, "data": { "items": [], "totalCount": 0, "page": 1, "pageSize": 25 } }
        """)));
        IShortifyClient client = CreateClient(handler);

        _ = await client.ListLinksAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("https://gateway.test/shortify/api/v1/links", Assert.Single(handler.Requests).Uri?.ToString());
    }

    [Fact]
    public async Task GetLinkAnalyticsAsync_SendsTheDateRange()
    {
        StubHttpMessageHandler handler = new((_, _) => Task.FromResult(StubResponses.Ok("""
        {
          "success": true,
          "data": {
            "from": "2026-09-01", "to": "2026-09-30", "totalClicks": 10, "botClicks": 2, "humanClicks": 8,
            "daily": [ { "date": "2026-09-01", "clicks": 10, "botClicks": 2 } ],
            "countries": [ { "value": "PK", "clicks": 6 } ],
            "referrers": [], "devices": [], "operatingSystems": [], "browsers": []
          }
        }
        """)));
        IShortifyClient client = CreateClient(handler);

        AnalyticsReport report = await client.GetLinkAnalyticsAsync(LinkId, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), TestContext.Current.CancellationToken);

        Assert.Equal(8, report.HumanClicks);
        Assert.Equal("PK", Assert.Single(report.Countries).Value);
        Assert.Equal(new DateOnly(2026, 9, 1), Assert.Single(report.Daily).Date);
        Assert.Equal(
            $"https://gateway.test/shortify/api/v1/links/{LinkId}/analytics?from=2026-09-01&to=2026-09-30",
            Assert.Single(handler.Requests).Uri?.ToString());
    }

    [Fact]
    public async Task DeleteLinkAsync_AcceptsAnEnvelopeWithNullData()
    {
        StubHttpMessageHandler handler = new((_, _) => Task.FromResult(StubResponses.Ok("""{ "success": true, "data": null }""")));
        IShortifyClient client = CreateClient(handler);

        await client.DeleteLinkAsync(LinkId, TestContext.Current.CancellationToken);

        CapturedRequest request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal($"https://gateway.test/shortify/api/v1/links/{LinkId}", request.Uri?.ToString());
    }

    [Fact]
    public async Task AddDomainAsync_ReturnsTheDnsRecordsToPublish()
    {
        StubHttpMessageHandler handler = new((_, _) => Task.FromResult(StubResponses.Ok("""
        {
          "success": true,
          "data": {
            "id": "1cf8e5e6-21f0-49a5-90b1-5c0f985c9df7", "host": "go.acme.com", "status": "PendingVerification",
            "isPlatform": false, "isDefault": false,
            "dns": {
              "verificationRecordType": "TXT", "verificationRecordName": "_shortify.go.acme.com", "verificationRecordValue": "shortify-verify=abc",
              "routingRecordType": "CNAME", "routingRecordName": "go.acme.com", "routingRecordValue": "edge.shfy.link"
            },
            "createdAtUTC": "2026-10-01T00:00:00Z"
          }
        }
        """)));
        IShortifyClient client = CreateClient(handler);

        ShortDomain domain = await client.AddDomainAsync(" go.acme.com ", TestContext.Current.CancellationToken);

        Assert.Equal(ShortDomainStatus.PendingVerification, domain.Status);
        Assert.Equal("edge.shfy.link", domain.Dns?.RoutingRecordValue);
        Assert.Equal("""{"host":"go.acme.com"}""", Assert.Single(handler.Requests).Body);
    }

    [Fact]
    public async Task ErrorEnvelope_MapsToShortifyApiException()
    {
        StubHttpMessageHandler handler = new((_, _) =>
        {
            HttpResponseMessage response = StubResponses.Json(HttpStatusCode.TooManyRequests, """
            {
              "success": false,
              "error": {
                "errorCode": 42902,
                "message": "Daily link limit reached.",
                "description": "Upgrade to Enterprise for more links.",
                "traceId": "00-abc-01",
                "details": null
              }
            }
            """);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            return Task.FromResult(response);
        });
        IShortifyClient client = CreateClient(handler);

        ShortifyApiException exception = await Assert.ThrowsAsync<ShortifyApiException>(
            () => client.CreateLinkAsync(new CreateLinkRequest { DestinationUrl = "https://example.com" }, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Equal(42902, exception.ErrorCode);
        Assert.True(exception.IsPlanLimitReached);
        Assert.False(exception.IsRateLimited);
        Assert.Equal("Daily link limit reached.", exception.Message);
        Assert.Equal("00-abc-01", exception.TraceId);
        Assert.Equal(TimeSpan.FromSeconds(30), exception.RetryAfter);
    }

    [Fact]
    public async Task ValidationError_ExposesFieldDetails()
    {
        StubHttpMessageHandler handler = new((_, _) => Task.FromResult(StubResponses.Json(HttpStatusCode.BadRequest, """
        {
          "success": false,
          "error": { "errorCode": 40001, "message": "Validation failed.", "details": { "destinationUrl": [ "Must be an absolute http(s) URL." ] } }
        }
        """)));
        IShortifyClient client = CreateClient(handler);

        ShortifyApiException exception = await Assert.ThrowsAsync<ShortifyApiException>(
            () => client.CreateLinkAsync(new CreateLinkRequest { DestinationUrl = "nope" }, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(ShortifyErrorCodes.ValidationFailed, exception.ErrorCode);
        Assert.Equal("Must be an absolute http(s) URL.", Assert.Single(exception.Details["destinationUrl"]));
    }

    [Fact]
    public async Task NonEnvelopeResponse_MapsToShortifyApiExceptionWithTheRawBody()
    {
        StubHttpMessageHandler handler = new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>Bad gateway</html>", Encoding.UTF8, "text/html")
        }));
        IShortifyClient client = CreateClient(handler);

        ShortifyApiException exception = await Assert.ThrowsAsync<ShortifyApiException>(
            () => client.GetLinkAsync(LinkId, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
        Assert.Null(exception.ErrorCode);
        Assert.Equal("<html>Bad gateway</html>", exception.Description);
    }

    [Fact]
    public async Task SuccessStatusWithNonJsonBody_IsAShortifyApiExceptionNotAJsonException()
    {
        StubHttpMessageHandler handler = new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>Login</html>", Encoding.UTF8, "text/html")
        }));
        IShortifyClient client = CreateClient(handler);

        ShortifyApiException exception = await Assert.ThrowsAsync<ShortifyApiException>(
            () => client.GetAccountAsync(TestContext.Current.CancellationToken));

        Assert.IsType<JsonException>(exception.InnerException, exactMatch: false);
    }

    [Fact]
    public async Task MissingApiKey_ThrowsBeforeAnyRequest()
    {
        StubHttpMessageHandler handler = new((_, _) => Task.FromResult(StubResponses.Ok(StubResponses.Link)));
        ShortifyClient client = new(new HttpClient(handler), Options.Create(new ShortifyOptions { BaseUrl = "https://gateway.test/shortify" }));

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => client.GetLinkAsync(LinkId, TestContext.Current.CancellationToken));

        Assert.Equal(nameof(ShortifyOptions.ApiKey), exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void AddShortify_BindsTheShortifySection()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Shortify:BaseUrl"] = "https://gateway.test/shortify/",
                ["Shortify:ApiKey"] = "shfy_secret"
            })
            .Build();

        ServiceCollection services = new();
        IHttpClientBuilder builder = services.AddShortify(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Equal(DependencyInjection.ServiceName, builder.Name);
        Assert.IsType<ShortifyClient>(provider.GetRequiredService<IShortifyClient>());
        ShortifyOptions options = provider.GetRequiredService<IOptions<ShortifyOptions>>().Value;
        Assert.Equal("https://gateway.test/shortify/", options.BaseUrl);
        Assert.Equal("shfy_secret", options.ApiKey);
    }

    private static ShortifyClient CreateClient(StubHttpMessageHandler handler)
        => new(
            new HttpClient(handler),
            Options.Create(new ShortifyOptions { BaseUrl = "https://gateway.test/shortify/", ApiKey = "shfy_secret" }));
}