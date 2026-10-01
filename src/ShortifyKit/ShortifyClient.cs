namespace ShortifyKit;

public sealed class ShortifyClient(HttpClient httpClient, IOptions<ShortifyOptions> options) : IShortifyClient
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string IdempotencyKeyHeaderName = "Idempotency-Key";

    private static readonly string UserAgent =
        "ShortifyKit/" + (typeof(ShortifyClient).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // Null means "not sent": PATCH leaves those fields unchanged and creates take the server defaults.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public async Task<AccountOverview> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/v1/me");
        return await SendJsonAsync<AccountOverview>(request, cancellationToken);
    }

    public async Task<Usage> GetUsageAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/v1/usage" + DateRangeQuery(from, to));
        return await SendJsonAsync<Usage>(request, cancellationToken);
    }

    public async Task<AnalyticsReport> GetAnalyticsAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/v1/analytics" + DateRangeQuery(from, to));
        return await SendJsonAsync<AnalyticsReport>(request, cancellationToken);
    }

    public async Task<IReadOnlyList<Plan>> ListPlansAsync(CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/v1/plans");
        return await SendJsonAsync<IReadOnlyList<Plan>>(request, cancellationToken);
    }

    public async Task<ShortLink> CreateLinkAsync(CreateLinkRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfBlank(request.DestinationUrl, nameof(request.DestinationUrl));

        using HttpRequestMessage httpRequest = CreateRequest(HttpMethod.Post, "/api/v1/links");
        httpRequest.Headers.TryAddWithoutValidation(
            IdempotencyKeyHeaderName,
            string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString("N") : idempotencyKey);
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        return await SendJsonAsync<ShortLink>(httpRequest, cancellationToken);
    }

    public async Task<IReadOnlyList<ShortLink>> CreateLinksAsync(IReadOnlyList<CreateLinkRequest> links, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(links);

        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, "/api/v1/links/bulk");
        request.Content = JsonContent.Create(new BulkCreateLinksBody(links), options: JsonOptions);

        return await SendJsonAsync<IReadOnlyList<ShortLink>>(request, cancellationToken);
    }

    public async Task<ShortLink> GetLinkAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, LinkPath(id));
        return await SendJsonAsync<ShortLink>(request, cancellationToken);
    }

    public async Task<ShortifyPage<ShortLink>> ListLinksAsync(LinkListQuery? query = null, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/v1/links" + BuildLinkListQuery(query));
        return await SendJsonAsync<ShortifyPage<ShortLink>>(request, cancellationToken);
    }

    public async Task<ShortLink> UpdateLinkAsync(Guid id, UpdateLinkRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using HttpRequestMessage httpRequest = CreateRequest(HttpMethod.Patch, LinkPath(id));
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        return await SendJsonAsync<ShortLink>(httpRequest, cancellationToken);
    }

    public async Task<ShortLink> SetLinkEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Put, LinkPath(id) + "/status");
        request.Content = JsonContent.Create(new SetLinkStatusBody(enabled), options: JsonOptions);

        return await SendJsonAsync<ShortLink>(request, cancellationToken);
    }

    public async Task DeleteLinkAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Delete, LinkPath(id));
        await SendWithoutResultAsync(request, cancellationToken);
    }

    public async Task<AnalyticsReport> GetLinkAnalyticsAsync(Guid id, DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, LinkPath(id) + "/analytics" + DateRangeQuery(from, to));
        return await SendJsonAsync<AnalyticsReport>(request, cancellationToken);
    }

    public async Task<ShortifyPage<ShortDomain>> ListDomainsAsync(DomainListQuery? query = null, CancellationToken cancellationToken = default)
    {
        QueryBuilder builder = new();
        if (query is not null)
        {
            builder.Add("page", query.Page);
            builder.Add("pageSize", query.PageSize);
            builder.Add("search", query.Search);
            builder.Add("sortBy", query.SortBy);
            builder.Add("sortDirection", query.SortDirection);
            builder.Add("status", query.Status);
        }

        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/v1/domains" + builder);
        return await SendJsonAsync<ShortifyPage<ShortDomain>>(request, cancellationToken);
    }

    public async Task<ShortDomain> AddDomainAsync(string host, CancellationToken cancellationToken = default)
    {
        ThrowIfBlank(host, nameof(host));

        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, "/api/v1/domains");
        request.Content = JsonContent.Create(new AddDomainBody(host.Trim()), options: JsonOptions);

        return await SendJsonAsync<ShortDomain>(request, cancellationToken);
    }

    public async Task<ShortDomain> VerifyDomainAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, DomainPath(id) + "/verify");
        return await SendJsonAsync<ShortDomain>(request, cancellationToken);
    }

    public async Task DeleteDomainAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Delete, DomainPath(id));
        await SendWithoutResultAsync(request, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        ShortifyOptions current = options.Value;
        ThrowIfBlank(current.BaseUrl, nameof(ShortifyOptions.BaseUrl));
        ThrowIfBlank(current.ApiKey, nameof(ShortifyOptions.ApiKey));

        // Always absolute: keeps a path prefix on BaseUrl (e.g. https://gateway/shortify) and never mutates the
        // shared HttpClient.BaseAddress, which throws once the client has started sending requests.
        Uri requestUri = new(current.BaseUrl.TrimEnd('/') + path, UriKind.Absolute);

        HttpRequestMessage request = new(method, requestUri);
        request.Headers.TryAddWithoutValidation(ApiKeyHeaderName, current.ApiKey);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        return request;
    }

    private async Task<T> SendJsonAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        ApiEnvelope<T> envelope = await ReadEnvelopeAsync<T>(response, cancellationToken);

        return envelope.Data
            ?? throw new ShortifyApiException("Shortify response carried no data.", response.StatusCode);
    }

    // Deletes answer { success: true, data: null }; only a failure is an error.
    private async Task SendWithoutResultAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        _ = await ReadEnvelopeAsync<JsonElement>(response, cancellationToken, allowEmptyBody: true);
    }

    private static async Task<ApiEnvelope<T>> ReadEnvelopeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        bool allowEmptyBody = false)
    {
        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        ApiEnvelope<T>? envelope = null;
        Exception? parseFailure = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                envelope = JsonSerializer.Deserialize<ApiEnvelope<T>>(body, JsonOptions);
            }
            catch (JsonException ex)
            {
                // Not a Shortify envelope - e.g. a gateway or proxy error page.
                parseFailure = ex;
            }
        }

        if (response.IsSuccessStatusCode && envelope is { Success: true })
        {
            return envelope;
        }

        if (response.IsSuccessStatusCode && allowEmptyBody && string.IsNullOrWhiteSpace(body))
        {
            return new ApiEnvelope<T>(true, default, null);
        }

        ApiError? error = envelope?.Error;
        string fallbackMessage = response.IsSuccessStatusCode
            ? "Shortify returned a response that is not a Shortify envelope."
            : $"Shortify request failed with HTTP {(int)response.StatusCode}.";

        throw new ShortifyApiException(
            error?.Message ?? fallbackMessage,
            response.StatusCode,
            error?.ErrorCode,
            error?.Description ?? (envelope is null ? Truncate(body) : null),
            error?.TraceId,
            error?.Details,
            parseFailure,
            GetRetryAfter(response));
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        RetryConditionHeaderValue? retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is TimeSpan delta)
        {
            return delta;
        }

        if (retryAfter?.Date is DateTimeOffset date)
        {
            TimeSpan delay = date - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        return null;
    }

    private static string BuildLinkListQuery(LinkListQuery? query)
    {
        if (query is null)
        {
            return string.Empty;
        }

        QueryBuilder builder = new();
        builder.Add("page", query.Page);
        builder.Add("pageSize", query.PageSize);
        builder.Add("search", query.Search);
        builder.Add("sortBy", query.SortBy);
        builder.Add("sortDirection", query.SortDirection);
        builder.Add("domainId", query.DomainId);
        builder.Add("status", query.Status);
        builder.Add("tag", query.Tag);
        builder.Add("externalId", query.ExternalId);
        builder.Add("expired", query.Expired);
        builder.Add("createdFrom", query.CreatedFromUtc);
        builder.Add("createdTo", query.CreatedToUtc);
        return builder.ToString();
    }

    private static string DateRangeQuery(DateOnly? from, DateOnly? to)
    {
        QueryBuilder builder = new();
        builder.Add("from", from);
        builder.Add("to", to);
        return builder.ToString();
    }

    private static string LinkPath(Guid id) => "/api/v1/links/" + id.ToString("D", CultureInfo.InvariantCulture);

    private static string DomainPath(Guid id) => "/api/v1/domains/" + id.ToString("D", CultureInfo.InvariantCulture);

    private static string? Truncate(string body)
        => string.IsNullOrWhiteSpace(body) ? null : body.Length <= 1024 ? body : body[..1024];

    private static void ThrowIfBlank(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", paramName);
        }
    }

    private sealed record BulkCreateLinksBody(IReadOnlyList<CreateLinkRequest> Links);

    private sealed record SetLinkStatusBody(bool Enabled);

    private sealed record AddDomainBody(string Host);

    private sealed record ApiEnvelope<T>(bool Success, T? Data, ApiError? Error);

    private sealed record ApiError(
        int ErrorCode,
        string? Message,
        string? Description,
        string? TraceId,
        IReadOnlyDictionary<string, string[]>? Details);

    private sealed class QueryBuilder
    {
        private readonly List<string> _parts = [];

        public void Add<T>(string name, T? value)
        {
            if (value is null)
            {
                return;
            }

            string text = value switch
            {
                DateTime dateTime => dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                bool boolean => boolean ? "true" : "false",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            _parts.Add(Uri.EscapeDataString(name) + "=" + Uri.EscapeDataString(text));
        }

        public override string ToString()
            => _parts.Count == 0 ? string.Empty : "?" + string.Join('&', _parts);
    }
}