using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace CrashBytes.Http;

/// <summary>
/// Dependency-injection extensions that wire CrashBytes HTTP client conventions
/// (correlation IDs, bearer tokens, resilience) onto <see cref="IHttpClientBuilder"/>.
/// </summary>
public static class HttpClientBuilderExtensions
{
    /// <summary>
    /// Registers a named <see cref="HttpClient"/> with CrashBytes default conventions:
    /// correlation-ID propagation and the standard resilience pipeline (retry +
    /// circuit-breaker) from <c>Microsoft.Extensions.Http.Resilience</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The logical name for the HTTP client.</param>
    /// <param name="configure">Optional configuration callback.</param>
    /// <returns>The <see cref="IHttpClientBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="name"/> is <c>null</c>.
    /// </exception>
    public static IHttpClientBuilder AddCrashBytesHttpClient(
        this IServiceCollection services,
        string name,
        Action<CrashBytesHttpClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(name);

        var options = new CrashBytesHttpClientOptions();
        configure?.Invoke(options);

        var builder = services.AddHttpClient(name);
        return ApplyCrashBytesConventions(builder, options);
    }

    /// <summary>
    /// Registers a typed <see cref="HttpClient"/> for <typeparamref name="TClient"/> with
    /// CrashBytes default conventions: correlation-ID propagation and the standard resilience
    /// pipeline (retry + circuit-breaker) from <c>Microsoft.Extensions.Http.Resilience</c>.
    /// </summary>
    /// <typeparam name="TClient">The typed client class.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The logical name for the HTTP client.</param>
    /// <param name="configure">Optional configuration callback.</param>
    /// <returns>The <see cref="IHttpClientBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="name"/> is <c>null</c>.
    /// </exception>
    public static IHttpClientBuilder AddCrashBytesHttpClient<TClient>(
        this IServiceCollection services,
        string name,
        Action<CrashBytesHttpClientOptions>? configure = null)
        where TClient : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(name);

        var options = new CrashBytesHttpClientOptions();
        configure?.Invoke(options);

        var builder = services.AddHttpClient<TClient>(name);
        return ApplyCrashBytesConventions(builder, options);
    }

    /// <summary>
    /// Adds a <see cref="CorrelationIdHandler"/> to the pipeline.
    /// </summary>
    /// <param name="builder">The HTTP client builder.</param>
    /// <param name="configure">Optional configuration callback for correlation options.</param>
    /// <returns>The <see cref="IHttpClientBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <c>null</c>.</exception>
    public static IHttpClientBuilder AddCorrelationIdHandler(
        this IHttpClientBuilder builder,
        Action<CorrelationIdOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new CorrelationIdOptions();
        configure?.Invoke(options);
        return builder.AddHttpMessageHandler(() => new CorrelationIdHandler(options));
    }

    /// <summary>
    /// Adds a <see cref="BearerTokenHandler"/> to the pipeline.
    /// </summary>
    /// <param name="builder">The HTTP client builder.</param>
    /// <param name="tokenProvider">Asynchronous bearer-token provider.</param>
    /// <param name="configure">Optional configuration callback for token options.</param>
    /// <returns>The <see cref="IHttpClientBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> or <paramref name="tokenProvider"/> is <c>null</c>.
    /// </exception>
    public static IHttpClientBuilder AddBearerTokenHandler(
        this IHttpClientBuilder builder,
        Func<CancellationToken, Task<string>> tokenProvider,
        Action<BearerTokenOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(tokenProvider);

        var options = new BearerTokenOptions { TokenProvider = tokenProvider };
        configure?.Invoke(options);
        if (options.TokenProvider is null)
        {
            throw new InvalidOperationException(
                "BearerTokenOptions.TokenProvider was cleared by the configure callback.");
        }

        return builder.AddHttpMessageHandler(() => new BearerTokenHandler(options));
    }

    private static IHttpClientBuilder ApplyCrashBytesConventions(
        IHttpClientBuilder builder,
        CrashBytesHttpClientOptions options)
    {
        if (options.EnableCorrelationId)
        {
            var correlationOptions = options.CorrelationId;
            builder.AddHttpMessageHandler(() => new CorrelationIdHandler(correlationOptions));
        }

        if (options.EnableBearerToken)
        {
            if (options.BearerToken.TokenProvider is null)
            {
                throw new InvalidOperationException(
                    "EnableBearerToken is true but BearerToken.TokenProvider is not configured.");
            }

            var bearerOptions = options.BearerToken;
            builder.AddHttpMessageHandler(() => new BearerTokenHandler(bearerOptions));
        }

        if (options.EnableResilience)
        {
            builder.AddStandardResilienceHandler();
        }

        return builder;
    }
}
