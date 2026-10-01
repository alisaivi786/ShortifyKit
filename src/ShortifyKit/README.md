# ShortifyKit

Typed .NET client for the **Shortify** URL-shortening API. It covers short links (create, bulk create, list and
search, edit, pause, delete), idempotent creates, click analytics, usage and quota, branded domains with DNS
verification, and plans.

Targets .NET 8, 9 and 10.

```bash
dotnet add package ShortifyKit
```

## Configure

```json
{
  "Shortify": {
    "BaseUrl": "https://api-gateway.absoluit.com/shortify"
  }
}
```

`BaseUrl` is the API root **without** `/api/v1`. A path prefix such as `/shortify` is kept, and a trailing slash
is optional.

The API key (`shfy_…`) is a backend secret. Keep it out of `appsettings.json` and source control. Supply it from
the environment (`Shortify__ApiKey`), user secrets or a vault, and never send it to a browser or a mobile app.

```csharp
builder.Services.AddShortify(builder.Configuration);

// or in code:
builder.Services.AddShortify(options =>
{
    options.BaseUrl = "https://api-gateway.absoluit.com/shortify";
    options.ApiKey = builder.Configuration["Shortify:ApiKey"]!;
});
```

`AddShortify` returns the `IHttpClientBuilder`, so you add your own retries, logging or timeouts to it:

```csharp
builder.Services.AddShortify(builder.Configuration)
    .AddStandardResilienceHandler();   // Microsoft.Extensions.Http.Resilience
```

Retrying a create is safe because every create carries an idempotency key (see [Create a link](#create-a-link)).

## Use

Inject `IShortifyClient`. Every call is confined to the account that owns the API key.

### Check the key at startup

```csharp
AccountOverview me = await shortify.GetAccountAsync();
// me.Account.Name, me.Account.PlanCode, me.Usage.LinksCreatedToday, me.Account.Entitlements.DailyLinkLimit
```

### Create a link

```csharp
ShortLink link = await shortify.CreateLinkAsync(
    new CreateLinkRequest
    {
        DestinationUrl = "https://example.com/spring-sale",
        Title = "Spring sale",
        Tags = ["spring", "email"],
        Utm = new Utm(Source: "newsletter", Medium: "email"),
        ExternalId = order.Id.ToString()
    },
    idempotencyKey: $"order-{order.Id}");

Console.WriteLine(link.ShortUrl);   // https://shfy.link/A7kd92x
```

Each create sends an `Idempotency-Key` header. You can pass your own (an order id works well), or the client
generates one. If the same key is sent again with the same body, the server returns the original link instead of
creating a second one.

With the Enterprise plan, `CreateLinksAsync` creates up to 100 links in one call. The server validates the batch
as a whole, so one bad item fails the whole call.

### List, edit, pause, delete

```csharp
ShortifyPage<ShortLink> page = await shortify.ListLinksAsync(new LinkListQuery
{
    Search = "spring",
    Status = LinkStatus.Active,
    SortBy = LinkSortBy.Clicks,
    SortDirection = SortDirection.Desc,
    PageSize = 50
});

// PATCH: properties left null are not sent and stay unchanged.
ShortLink edited = await shortify.UpdateLinkAsync(link.Id, new UpdateLinkRequest
{
    DestinationUrl = "https://example.com/summer-sale",
    Version = link.Version   // 409 instead of overwriting someone else's edit
});

await shortify.SetLinkEnabledAsync(link.Id, enabled: false);   // paused links answer 404
await shortify.DeleteLinkAsync(link.Id);                        // the code is never reissued
```

### Analytics and quota

```csharp
AnalyticsReport clicks = await shortify.GetLinkAnalyticsAsync(link.Id, from: new DateOnly(2026, 9, 1));
// clicks.HumanClicks, clicks.Daily, clicks.Countries, clicks.Referrers, clicks.Devices, ...

AnalyticsReport all = await shortify.GetAnalyticsAsync();       // every link on the account
Usage usage = await shortify.GetUsageAsync();                   // usage.Limits vs usage.Current, usage.History
```

If you don't pass dates, analytics cover the last 30 days. The range is limited to the plan's analytics retention.

### Branded domains

```csharp
ShortDomain domain = await shortify.AddDomainAsync("go.acme.com");
// Publish domain.Dns: a TXT record (ownership) and a CNAME record (routing). Then:
domain = await shortify.VerifyDomainAsync(domain.Id);
// domain.Status == ShortDomainStatus.Active, or read domain.FailureReason

ShortLink branded = await shortify.CreateLinkAsync(new CreateLinkRequest
{
    DestinationUrl = "https://acme.com/launch",
    Domain = "go.acme.com",
    Alias = "launch"
});
```

## Errors

When the API refuses or fails a call, the client throws `ShortifyApiException`, which is an `HttpRequestException`:

| Status / `ErrorCode` | Meaning | Helper |
|---|---|---|
| 400 / 40001 | Validation failed. `Details` maps each field to its messages (`links[3].destinationUrl` for bulk). | |
| 401 / 40101 | Missing or invalid API key. | |
| 403 / 40301 | Not allowed: suspended account, or a blocked link. | |
| 403 / 40302 | The plan doesn't include this feature (custom alias, bulk, custom domains). | `IsPlanUpgradeRequired` |
| 404 / 40401 | Not found. Other accounts' resources also return this. | |
| 409 / 40901 | Conflict: alias taken, stale `Version`, or an idempotency key reused with a different body. | |
| 429 / 42901 | Rate limited. Wait `RetryAfter`. | `IsRateLimited` |
| 429 / 42902 | Plan limit reached (links per day or month, domains). Retrying won't help. | `IsPlanLimitReached` |

```csharp
try
{
    await shortify.CreateLinkAsync(request);
}
catch (ShortifyApiException ex) when (ex.IsPlanLimitReached)
{
    // tell the user to upgrade; ex.TraceId identifies the request for support
}
```

If a response isn't a Shortify envelope (for example a proxy's error page), you still get a
`ShortifyApiException`: `ErrorCode` is null and `Description` holds the start of the raw body.

## Logging

The client doesn't log anything itself. To record outbound calls with redacted headers, add
[NinjaVault.Http](https://www.nuget.org/packages/NinjaVault.Http) to the client:

```csharp
builder.Services.AddShortify(builder.Configuration)
    .AddNinjaVaultHttpLogging(ShortifyKit.DependencyInjection.ServiceName);
```

The `X-Api-Key` header is redacted, but request and response bodies are captured by default, and they contain your
destination URLs. Turn body capture off (`NinjaVault:ExternalApiLogging:CaptureRequestBody` /
`CaptureResponseBody` set to `false`) if those URLs are sensitive.

## License

MIT