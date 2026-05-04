using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace CrashBytes.Http;

/// <summary>
/// A <see cref="DelegatingHandler"/> that attaches a bearer token to outgoing requests.
/// </summary>
/// <remarks>
/// The token is obtained from a configurable provider delegate and cached for
/// <see cref="BearerTokenOptions.TokenLifetime"/>. Concurrent requests share a single token
/// fetch via an internal <see cref="SemaphoreSlim"/>; a non-positive lifetime forces a fetch
/// on every request.
/// </remarks>
public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly BearerTokenOptions _options;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;

    /// <summary>
    /// Initializes a new instance of <see cref="BearerTokenHandler"/> using the supplied token
    /// provider with default options.
    /// </summary>
    /// <param name="tokenProvider">A delegate that asynchronously yields a bearer token.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tokenProvider"/> is <c>null</c>.</exception>
    public BearerTokenHandler(Func<CancellationToken, Task<string>> tokenProvider)
        : this(new BearerTokenOptions { TokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider)) })
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="BearerTokenHandler"/>.
    /// </summary>
    /// <param name="options">Handler options. <see cref="BearerTokenOptions.TokenProvider"/> must be set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="BearerTokenOptions.TokenProvider"/> is <c>null</c>.</exception>
    public BearerTokenHandler(BearerTokenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.TokenProvider is null)
        {
            throw new InvalidOperationException(
                $"{nameof(BearerTokenOptions.TokenProvider)} must be configured.");
        }

        _options = options;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="BearerTokenHandler"/> with options resolved
    /// from the DI container.
    /// </summary>
    /// <param name="options">Handler options accessor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="BearerTokenOptions.TokenProvider"/> is <c>null</c>.
    /// </exception>
    public BearerTokenHandler(IOptions<BearerTokenOptions> options)
        : this((options ?? throw new ArgumentNullException(nameof(options))).Value)
    {
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_options.PreserveExistingAuthorization && request.Headers.Authorization is not null)
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var token = await GetTokenAsync(cancellationToken).ConfigureAwait(false);
        var scheme = string.IsNullOrWhiteSpace(_options.Scheme) ? "Bearer" : _options.Scheme;
        request.Headers.Authorization = new AuthenticationHeaderValue(scheme, token);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Forces the next request to acquire a fresh token from the configured provider.
    /// </summary>
    public void InvalidateCachedToken()
    {
        _refreshLock.Wait();
        try
        {
            _cachedToken = null;
            _cachedAt = DateTimeOffset.MinValue;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (IsCachedTokenValid(out var cached))
        {
            return cached;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsCachedTokenValid(out cached))
            {
                return cached;
            }

            var provider = _options.TokenProvider!;
            var token = await provider(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("Bearer token provider returned a null or empty token.");
            }

            _cachedToken = token;
            _cachedAt = DateTimeOffset.UtcNow;
            return token;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool IsCachedTokenValid(out string token)
    {
        if (_cachedToken is null || _options.TokenLifetime <= TimeSpan.Zero)
        {
            token = string.Empty;
            return false;
        }

        if (DateTimeOffset.UtcNow - _cachedAt < _options.TokenLifetime)
        {
            token = _cachedToken;
            return true;
        }

        token = string.Empty;
        return false;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshLock.Dispose();
        }

        base.Dispose(disposing);
    }
}
