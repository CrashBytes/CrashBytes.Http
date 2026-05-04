namespace CrashBytes.Http;

/// <summary>
/// Configuration for <see cref="HttpClientBuilderExtensions.AddCrashBytesHttpClient"/>.
/// </summary>
public sealed class CrashBytesHttpClientOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to register a <see cref="CorrelationIdHandler"/>
    /// that propagates an <c>X-Correlation-ID</c> header on outgoing requests. Defaults to <c>true</c>.
    /// </summary>
    public bool EnableCorrelationId { get; set; } = true;

    /// <summary>
    /// Gets or sets the correlation ID handler options. Ignored when <see cref="EnableCorrelationId"/>
    /// is <c>false</c>.
    /// </summary>
    public CorrelationIdOptions CorrelationId { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to register a <see cref="BearerTokenHandler"/>.
    /// When <c>true</c>, <see cref="BearerToken"/> must be configured with a token provider.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool EnableBearerToken { get; set; }

    /// <summary>
    /// Gets or sets the bearer token handler options.
    /// </summary>
    public BearerTokenOptions BearerToken { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to attach a standard resilience pipeline
    /// (retry with exponential backoff plus circuit breaker / timeout defaults provided by
    /// <c>Microsoft.Extensions.Http.Resilience</c>). Defaults to <c>true</c>.
    /// </summary>
    public bool EnableResilience { get; set; } = true;
}
