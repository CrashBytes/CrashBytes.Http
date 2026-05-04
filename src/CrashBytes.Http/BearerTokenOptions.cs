namespace CrashBytes.Http;

/// <summary>
/// Configuration for <see cref="BearerTokenHandler"/>.
/// </summary>
public sealed class BearerTokenOptions
{
    /// <summary>
    /// Gets or sets the asynchronous token provider. The provider is invoked when no cached
    /// token is available or after the cached token has expired.
    /// </summary>
    public Func<CancellationToken, Task<string>>? TokenProvider { get; set; }

    /// <summary>
    /// Gets or sets the duration for which a fetched token is reused before the provider is
    /// invoked again. Set to <see cref="TimeSpan.Zero"/> to fetch on every request. Defaults to
    /// 5 minutes.
    /// </summary>
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the authentication scheme. Defaults to <c>Bearer</c>.
    /// </summary>
    public string Scheme { get; set; } = "Bearer";

    /// <summary>
    /// Gets or sets a value indicating whether an existing <c>Authorization</c> header on the
    /// outgoing request should be left untouched. Defaults to <c>true</c>.
    /// </summary>
    public bool PreserveExistingAuthorization { get; set; } = true;
}
