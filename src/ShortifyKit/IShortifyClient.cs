namespace ShortifyKit;

/// <summary>
/// Server-to-server client for the Shortify API, authenticated with the account's <c>X-Api-Key</c>. Every call is
/// confined to that account. Never expose the key to a browser or mobile app; hand out <see cref="ShortLink.ShortUrl"/>.
/// Failures throw <see cref="ShortifyApiException"/>.
/// </summary>
public interface IShortifyClient
{
    /// <summary>The account behind the key: plan, quota use, key metadata, link counts and balance. A good startup check.</summary>
    Task<AccountOverview> GetAccountAsync(CancellationToken cancellationToken = default);

    /// <summary>Quota against use, with a daily history (server default: the last 30 days).</summary>
    Task<Usage> GetUsageAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default);

    /// <summary>Clicks across all the account's links (server default: the last 30 days, clamped to the plan's retention).</summary>
    Task<AnalyticsReport> GetAnalyticsAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default);

    /// <summary>The plans and their quota, in display order. Needs no API key.</summary>
    Task<IReadOnlyList<Plan>> ListPlansAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a short link. Every create carries an <c>Idempotency-Key</c>: yours (e.g. an order id), or a generated
    /// one. A retry with the same key and body returns the original link instead of creating a second one, so a
    /// retry policy on this client is safe for creates. 429 / 42902 when the plan's link quota is used up.
    /// </summary>
    Task<ShortLink> CreateLinkAsync(CreateLinkRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default);

    /// <summary>Creates up to 100 links in one call (Enterprise plan). Validated as a whole: one bad item fails the call.</summary>
    Task<IReadOnlyList<ShortLink>> CreateLinksAsync(IReadOnlyList<CreateLinkRequest> links, CancellationToken cancellationToken = default);

    Task<ShortLink> GetLinkAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lists, searches and filters the account's links.</summary>
    Task<ShortifyPage<ShortLink>> ListLinksAsync(LinkListQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>Repoints or edits a link. The short URL itself never changes.</summary>
    Task<ShortLink> UpdateLinkAsync(Guid id, UpdateLinkRequest request, CancellationToken cancellationToken = default);

    /// <summary>Pauses (<c>false</c>: the link answers 404) or resumes (<c>true</c>) a link.</summary>
    Task<ShortLink> SetLinkEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>Deletes a link. Its code is retired and never reissued.</summary>
    Task DeleteLinkAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Clicks for one link (server default: the last 30 days, clamped to the plan's retention).</summary>
    Task<AnalyticsReport> GetLinkAnalyticsAsync(Guid id, DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default);

    /// <summary>The short domains the account can create links on: platform domains plus its own branded ones.</summary>
    Task<ShortifyPage<ShortDomain>> ListDomainsAsync(DomainListQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Connects a branded domain such as <c>go.acme.com</c> (plan permitting). Publish the returned
    /// <see cref="ShortDomain.Dns"/> records, then call <see cref="VerifyDomainAsync"/>.
    /// </summary>
    Task<ShortDomain> AddDomainAsync(string host, CancellationToken cancellationToken = default);

    /// <summary>Checks DNS now. Does not throw for a failed check: read <see cref="ShortDomain.Status"/> and <see cref="ShortDomain.FailureReason"/>.</summary>
    Task<ShortDomain> VerifyDomainAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Disconnects a branded domain. 409 while it still has links.</summary>
    Task DeleteDomainAsync(Guid id, CancellationToken cancellationToken = default);
}