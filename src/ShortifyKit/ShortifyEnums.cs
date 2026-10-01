namespace ShortifyKit;

/// <summary>How a link redirects. <see cref="Temporary"/> (302) is the server default and the right choice for editable links.</summary>
public enum RedirectType
{
    /// <summary>301 - browsers cache it, so later edits and click counts are unreliable.</summary>
    Permanent,

    /// <summary>302.</summary>
    Temporary,

    /// <summary>307.</summary>
    TemporaryPreserveMethod,

    /// <summary>308.</summary>
    PermanentPreserveMethod
}

public enum LinkStatus
{
    Active,

    /// <summary>Paused by its owner (answers 404). Reversible with <see cref="IShortifyClient.SetLinkEnabledAsync"/>.</summary>
    Disabled,

    /// <summary>Taken down by an administrator. The owner cannot re-enable it.</summary>
    Blocked
}

public enum ShortDomainStatus
{
    PendingVerification,
    Active,
    Failed,

    /// <summary>Disabled by an administrator; links on it stop resolving.</summary>
    Suspended
}

public enum AccountStatus
{
    Active,

    /// <summary>The API key and every link are stopped, reversibly, by an administrator.</summary>
    Suspended
}

public enum SubscriptionStatus
{
    Active,
    Cancelled
}

public enum ApiKeyStatus
{
    Active,
    Revoked
}

public enum SortDirection
{
    Asc,
    Desc
}