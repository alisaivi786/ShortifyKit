namespace ShortifyKit;

/// <summary>Where Shortify lives and the account API key to call it with. Bound from the <c>Shortify</c> section.</summary>
public sealed class ShortifyOptions
{
    public const string SectionName = "Shortify";

    /// <summary>
    /// The API root, without <c>/api/v1</c>: e.g. <c>https://api-gateway.absoluit.com/shortify</c> behind the
    /// company gateway. A path prefix is kept; a trailing slash is optional.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The account key (<c>shfy_…</c>). A backend secret - never send it to a browser or mobile app.</summary>
    public string ApiKey { get; set; } = string.Empty;
}