namespace ShortifyKit;

public static class DependencyInjection
{
    /// <summary>
    /// Name of the <see cref="IHttpClientFactory"/> client used by <see cref="IShortifyClient"/>.
    /// Use it as the service name when you attach your own logging handler.
    /// </summary>
    public const string ServiceName = "Shortify";

    /// <summary>
    /// Registers <see cref="IShortifyClient"/> bound to the <see cref="ShortifyOptions.SectionName"/> section.
    /// Logging, retries and correlation are left to the application: chain your own handlers onto the returned
    /// <see cref="IHttpClientBuilder"/> (for example <c>.AddStandardResilienceHandler()</c>).
    /// </summary>
    public static IHttpClientBuilder AddShortify(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ShortifyOptions>(configuration.GetSection(ShortifyOptions.SectionName));

        return AddClient(services);
    }

    /// <summary>Registers <see cref="IShortifyClient"/> with options configured in code instead of configuration.</summary>
    public static IHttpClientBuilder AddShortify(this IServiceCollection services, Action<ShortifyOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);

        return AddClient(services);
    }

    // The client builds absolute request URIs from ShortifyOptions.BaseUrl, so no BaseAddress is set here.
    private static IHttpClientBuilder AddClient(IServiceCollection services)
        => services.AddHttpClient<IShortifyClient, ShortifyClient>(ServiceName);
}