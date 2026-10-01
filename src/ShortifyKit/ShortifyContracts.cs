namespace ShortifyKit;

/// <summary>Campaign parameters appended to the destination when the link redirects.</summary>
public sealed record Utm(
    string? Source = null,
    string? Medium = null,
    string? Campaign = null,
    string? Term = null,
    string? Content = null);

/// <summary>Creates one short link. Only <see cref="DestinationUrl"/> is required.</summary>
public sealed record CreateLinkRequest
{
    /// <summary>Where the link redirects. Absolute http(s) only.</summary>
    public required string DestinationUrl { get; init; }

    /// <summary>A short-domain host you own or a platform domain. Null for the account's default.</summary>
    public string? Domain { get; init; }

    /// <summary>Your own back-half, e.g. <c>spring-sale</c> (plan permitting). Null to generate one.</summary>
    public string? Alias { get; init; }

    public string? Title { get; init; }

    /// <summary>Null for the server default, <see cref="RedirectType.Temporary"/>.</summary>
    public RedirectType? RedirectType { get; init; }

    public DateTime? ExpiresAtUtc { get; init; }

    /// <summary>Where visitors go after expiry, instead of a "link expired" page.</summary>
    public string? ExpiredRedirectUrl { get; init; }

    public Utm? Utm { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Your own id for what the link is for (an order id, a campaign id). Filterable with <see cref="LinkListQuery.ExternalId"/>.</summary>
    public string? ExternalId { get; init; }
}

/// <summary>
/// Edits a link. Null properties are not sent and stay unchanged. Send <c>""</c> to clear a text field,
/// an empty list to clear <see cref="Tags"/>, and <see cref="ClearExpiry"/> to remove the expiry.
/// </summary>
public sealed record UpdateLinkRequest
{
    public string? DestinationUrl { get; init; }
    public string? Title { get; init; }
    public RedirectType? RedirectType { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public bool ClearExpiry { get; init; }
    public string? ExpiredRedirectUrl { get; init; }
    public Utm? Utm { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public string? ExternalId { get; init; }

    /// <summary>
    /// The <see cref="ShortLink.Version"/> you read. When set, the edit is refused with 409 (40901) if someone
    /// changed the link since, instead of silently overwriting their change.
    /// </summary>
    public int? Version { get; init; }
}

/// <summary>A short link.</summary>
public sealed record ShortLink
{
    public Guid Id { get; init; }

    /// <summary>The URL to share, e.g. <c>https://shfy.link/A7kd92x</c>.</summary>
    public string ShortUrl { get; init; } = string.Empty;

    public string Domain { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string DestinationUrl { get; init; } = string.Empty;
    public string? Title { get; init; }
    public RedirectType RedirectType { get; init; }
    public LinkStatus Status { get; init; }

    /// <summary>Why an administrator blocked the link, when <see cref="Status"/> is <see cref="LinkStatus.Blocked"/>.</summary>
    public string? StatusReason { get; init; }

    public bool IsCustomAlias { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public bool IsExpired { get; init; }
    public string? ExpiredRedirectUrl { get; init; }
    public Utm? Utm { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string? ExternalId { get; init; }

    /// <summary>Human clicks so far. Updated by the analytics rollup, typically within a minute.</summary>
    public long Clicks { get; init; }

    public DateTime? LastClickedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? ModifiedAtUtc { get; init; }

    /// <summary>Send back as <see cref="UpdateLinkRequest.Version"/> for optimistic concurrency.</summary>
    public int Version { get; init; }
}

/// <summary>One page of a list.</summary>
public sealed record ShortifyPage<T>(
    IReadOnlyList<T> Items,
    long TotalCount,
    int Page,
    int PageSize);

/// <summary>A short domain: a shared platform domain, or one of the account's own branded domains.</summary>
public sealed record ShortDomain
{
    public Guid Id { get; init; }

    /// <summary>E.g. <c>go.acme.com</c>.</summary>
    public string Host { get; init; } = string.Empty;

    public ShortDomainStatus Status { get; init; }

    /// <summary>A shared domain every account can use, as opposed to a branded domain this account owns.</summary>
    public bool IsPlatform { get; init; }

    public bool IsDefault { get; init; }

    /// <summary>The DNS records to publish. Null for platform domains.</summary>
    public DnsSetup? Dns { get; init; }

    public DateTime? VerifiedAtUtc { get; init; }
    public DateTime? LastCheckedAtUtc { get; init; }

    /// <summary>Why the last DNS check failed, in words to act on.</summary>
    public string? FailureReason { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}

/// <summary>
/// Two DNS records for a branded domain: a TXT record that proves ownership (checked before the domain
/// activates) and a CNAME that routes visitors to Shortify's redirect service.
/// </summary>
public sealed record DnsSetup(
    string VerificationRecordType,
    string VerificationRecordName,
    string VerificationRecordValue,
    string RoutingRecordType,
    string RoutingRecordName,
    string RoutingRecordValue);

/// <summary>Click analytics: a daily series plus top-N breakdowns, largest first.</summary>
public sealed record AnalyticsReport
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public long TotalClicks { get; init; }
    public long BotClicks { get; init; }
    public long HumanClicks { get; init; }
    public IReadOnlyList<DailyClicks> Daily { get; init; } = [];
    public IReadOnlyList<DimensionCount> Countries { get; init; } = [];
    public IReadOnlyList<DimensionCount> Referrers { get; init; } = [];
    public IReadOnlyList<DimensionCount> Devices { get; init; } = [];
    public IReadOnlyList<DimensionCount> OperatingSystems { get; init; } = [];
    public IReadOnlyList<DimensionCount> Browsers { get; init; } = [];
}

public sealed record DailyClicks(DateOnly Date, long Clicks, long BotClicks);

public sealed record DimensionCount(string Value, long Clicks);

/// <summary>An account's quota and capabilities.</summary>
public sealed record Entitlements
{
    public long DailyLinkLimit { get; init; }
    public long MonthlyLinkLimit { get; init; }

    /// <summary>Clicks the plan includes per month. Reported, never enforced - links keep redirecting past it.</summary>
    public long MonthlyTrackedClickLimit { get; init; }

    public int MaxCustomDomains { get; init; }
    public int AnalyticsRetentionDays { get; init; }
    public bool CustomAliases { get; init; }
    public bool BulkCreate { get; init; }
}

/// <summary>Consumption for today and this calendar month (UTC), and the branded domains connected now.</summary>
public sealed record UsageSnapshot
{
    public DateOnly Today { get; init; }
    public DateOnly MonthStart { get; init; }
    public long LinksCreatedToday { get; init; }
    public long LinksCreatedThisMonth { get; init; }
    public long TrackedClicksThisMonth { get; init; }

    /// <summary>The account's own short domains - compare with <see cref="Entitlements.MaxCustomDomains"/>.</summary>
    public int CustomDomains { get; init; }
}

public sealed record UsageDay(DateOnly Date, long LinksCreated, long TrackedClicks);

/// <summary>Quota against use, with a day-by-day history.</summary>
public sealed record Usage
{
    public Entitlements Limits { get; init; } = new();
    public UsageSnapshot Current { get; init; } = new();
    public IReadOnlyList<UsageDay> History { get; init; } = [];
}

/// <summary>The caller's account: who it is, its plan, quota use, key metadata, link counts and balance.</summary>
public sealed record AccountOverview
{
    public Account Account { get; init; } = new();
    public Subscription? Subscription { get; init; }
    public UsageSnapshot Usage { get; init; } = new();

    /// <summary>The account's key as metadata - never the secret.</summary>
    public ApiKeyInfo? ApiKey { get; init; }

    public LinkCounts Links { get; init; } = new();
    public decimal OutstandingAmount { get; init; }
}

public sealed record Account
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ContactEmail { get; init; }
    public AccountStatus Status { get; init; }
    public Entitlements Entitlements { get; init; } = new();

    /// <summary><c>standard</c> or <c>enterprise</c>; null without an active subscription.</summary>
    public string? PlanCode { get; init; }

    public string? PlanName { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed record Subscription
{
    public Guid Id { get; init; }
    public string PlanCode { get; init; } = string.Empty;
    public string PlanName { get; init; } = string.Empty;
    public decimal MonthlyPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
    public SubscriptionStatus Status { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime CurrentPeriodStartUtc { get; init; }

    /// <summary>When the next monthly bill is issued.</summary>
    public DateTime CurrentPeriodEndUtc { get; init; }
}

public sealed record ApiKeyInfo
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;

    /// <summary>The leading, non-secret part of the key, e.g. <c>shfy_a3f9c2d1</c>.</summary>
    public string KeyPrefix { get; init; } = string.Empty;

    public ApiKeyStatus Status { get; init; }

    /// <summary>Accurate to within a few minutes.</summary>
    public DateTime? LastUsedAtUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}

public sealed record LinkCounts
{
    public long Total { get; init; }
    public long Active { get; init; }
    public long Disabled { get; init; }
    public long Blocked { get; init; }
    public long Expired { get; init; }
}

/// <summary>A plan as a pricing card shows it.</summary>
public sealed record Plan
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal MonthlyPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
    public Entitlements Quota { get; init; } = new();
    public IReadOnlyList<string> Features { get; init; } = [];
}