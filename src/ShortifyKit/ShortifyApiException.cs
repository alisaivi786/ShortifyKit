namespace ShortifyKit;

/// <summary>
/// The API refused or failed a call. <see cref="HttpRequestException.StatusCode"/> and <see cref="ErrorCode"/> tell
/// "not found" (404 / 40401) from "plan limit reached" (429 / 42902), "plan upgrade required" (403 / 40302),
/// a conflict (409 / 40901) or a validation problem (400 / 40001, see <see cref="Details"/>).
/// Quote <see cref="TraceId"/> to support.
/// </summary>
public sealed class ShortifyApiException : HttpRequestException
{
    public ShortifyApiException()
        : this("Shortify request failed.", HttpStatusCode.InternalServerError)
    {
    }

    public ShortifyApiException(string message)
        : this(message, HttpStatusCode.InternalServerError)
    {
    }

    public ShortifyApiException(string message, Exception innerException)
        : this(message, HttpStatusCode.InternalServerError, innerException: innerException)
    {
    }

    public ShortifyApiException(
        string message,
        HttpStatusCode statusCode,
        int? errorCode = null,
        string? description = null,
        string? traceId = null,
        IReadOnlyDictionary<string, string[]>? details = null,
        Exception? innerException = null,
        TimeSpan? retryAfter = null)
        : base(message, innerException, statusCode)
    {
        ErrorCode = errorCode;
        Description = description;
        TraceId = traceId;
        Details = details ?? new Dictionary<string, string[]>();
        RetryAfter = retryAfter;
    }

    /// <summary>The service's numeric error code, e.g. <c>42902</c>. Null when the response was not a Shortify error envelope.</summary>
    public int? ErrorCode { get; }

    public string? Description { get; }

    /// <summary>Server-side trace id of the failed request (the envelope's <c>traceId</c>). Log it for support.</summary>
    public string? TraceId { get; }

    /// <summary>Field-level validation messages, keyed by field (bulk items as <c>links[3].destinationUrl</c>).</summary>
    public IReadOnlyDictionary<string, string[]> Details { get; }

    /// <summary>From the <c>Retry-After</c> header, when the server sent one (rate limits). Use it for backoff.</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>The plan's daily/monthly link quota (or domain allowance) is used up - 42902. Retrying will not help.</summary>
    public bool IsPlanLimitReached => ErrorCode == ShortifyErrorCodes.PlanLimitReached;

    /// <summary>The feature (custom alias, bulk create, custom domains) needs a higher plan - 40302.</summary>
    public bool IsPlanUpgradeRequired => ErrorCode == ShortifyErrorCodes.PlanUpgradeRequired;

    /// <summary>Too many requests per minute - 42901. Wait <see cref="RetryAfter"/> and retry.</summary>
    public bool IsRateLimited => ErrorCode == ShortifyErrorCodes.RateLimited;
}

/// <summary>The numeric codes in a Shortify error envelope.</summary>
public static class ShortifyErrorCodes
{
    public const int ValidationFailed = 40001;
    public const int Unauthorized = 40101;
    public const int Forbidden = 40301;
    public const int PlanUpgradeRequired = 40302;
    public const int NotFound = 40401;
    public const int Conflict = 40901;
    public const int RateLimited = 42901;
    public const int PlanLimitReached = 42902;
    public const int DependencyUnavailable = 50301;
}