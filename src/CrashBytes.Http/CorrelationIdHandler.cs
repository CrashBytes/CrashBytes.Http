using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace CrashBytes.Http;

/// <summary>
/// A <see cref="DelegatingHandler"/> that ensures every outgoing request carries a correlation
/// identifier in a configurable header (default: <c>X-Correlation-ID</c>).
/// </summary>
/// <remarks>
/// Resolution order on each request:
/// <list type="number">
///   <item>If the configured header is already set on the outgoing request, it is preserved.</item>
///   <item>Otherwise, if <see cref="CorrelationIdOptions.UseActivityTraceId"/> is <c>true</c> and
///         <see cref="Activity.Current"/> has a trace identifier, that value is used.</item>
///   <item>Otherwise, <see cref="CorrelationIdOptions.CorrelationIdFactory"/> is invoked.</item>
/// </list>
/// </remarks>
public sealed class CorrelationIdHandler : DelegatingHandler
{
    private readonly CorrelationIdOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="CorrelationIdHandler"/> with default options.
    /// </summary>
    public CorrelationIdHandler()
        : this(new CorrelationIdOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CorrelationIdHandler"/>.
    /// </summary>
    /// <param name="options">Handler options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    public CorrelationIdHandler(CorrelationIdOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CorrelationIdHandler"/> with options resolved
    /// from the DI container.
    /// </summary>
    /// <param name="options">Handler options accessor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    public CorrelationIdHandler(IOptions<CorrelationIdOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? new CorrelationIdOptions();
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var headerName = string.IsNullOrWhiteSpace(_options.HeaderName)
            ? CorrelationIdOptions.DefaultHeaderName
            : _options.HeaderName;

        if (!request.Headers.Contains(headerName))
        {
            var correlationId = ResolveCorrelationId();
            request.Headers.TryAddWithoutValidation(headerName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }

    private string ResolveCorrelationId()
    {
        if (_options.UseActivityTraceId)
        {
            var activity = Activity.Current;
            if (activity is not null && activity.TraceId != default)
            {
                return activity.TraceId.ToString();
            }
        }

        var factory = _options.CorrelationIdFactory ?? (() => Guid.NewGuid().ToString("N"));
        return factory();
    }
}
