namespace ShortifyKit;

/// <summary>Filters, search, sort and paging for <see cref="IShortifyClient.ListLinksAsync"/>. Null properties are not sent.</summary>
public sealed record LinkListQuery
{
    /// <summary>1-based. Server default 1.</summary>
    public int? Page { get; init; }

    /// <summary>Server default 25.</summary>
    public int? PageSize { get; init; }

    /// <summary>Matches code, destination, title, external id and tags.</summary>
    public string? Search { get; init; }

    /// <summary>
    /// <c>createdAt</c> (default), <c>code</c>, <c>destination</c>, <c>title</c>, <c>clicks</c>, <c>lastClickedAt</c>,
    /// <c>expiresAt</c> or <c>status</c>. An unknown value falls back to the default order. See <see cref="LinkSortBy"/>.
    /// </summary>
    public string? SortBy { get; init; }

    /// <summary>Server default <see cref="SortDirection.Desc"/>.</summary>
    public SortDirection? SortDirection { get; init; }

    public Guid? DomainId { get; init; }
    public LinkStatus? Status { get; init; }
    public string? Tag { get; init; }
    public string? ExternalId { get; init; }

    /// <summary><c>true</c> for expired links only, <c>false</c> for unexpired only.</summary>
    public bool? Expired { get; init; }

    public DateTime? CreatedFromUtc { get; init; }
    public DateTime? CreatedToUtc { get; init; }
}

/// <summary>The <see cref="LinkListQuery.SortBy"/> values the server knows.</summary>
public static class LinkSortBy
{
    public const string CreatedAt = "createdAt";
    public const string Code = "code";
    public const string Destination = "destination";
    public const string Title = "title";
    public const string Clicks = "clicks";
    public const string LastClickedAt = "lastClickedAt";
    public const string ExpiresAt = "expiresAt";
    public const string Status = "status";
}

/// <summary>Search, sort and paging for <see cref="IShortifyClient.ListDomainsAsync"/>. Null properties are not sent.</summary>
public sealed record DomainListQuery
{
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public string? Search { get; init; }

    /// <summary><c>createdAt</c> (default), <c>host</c> or <c>status</c>.</summary>
    public string? SortBy { get; init; }

    public SortDirection? SortDirection { get; init; }
    public ShortDomainStatus? Status { get; init; }
}