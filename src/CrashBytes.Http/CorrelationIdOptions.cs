namespace CrashBytes.Http;

/// <summary>
/// Configuration for <see cref="CorrelationIdHandler"/>.
/// </summary>
public sealed class CorrelationIdOptions
{
    /// <summary>
    /// The default correlation header name (<c>X-Correlation-ID</c>).
    /// </summary>
    public const string DefaultHeaderName = "X-Correlation-ID";

    /// <summary>
    /// Gets or sets the outgoing header name. Defaults to <c>X-Correlation-ID</c>.
    /// </summary>
    public string HeaderName { get; set; } = DefaultHeaderName;

    /// <summary>
    /// Gets or sets a value indicating whether to use the trace identifier from
    /// <see cref="System.Diagnostics.Activity.Current"/> when no correlation ID is already present
    /// on the outgoing request. Defaults to <c>true</c>.
    /// </summary>
    public bool UseActivityTraceId { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional factory that produces a correlation ID when none is available
    /// from the request or the current <see cref="System.Diagnostics.Activity"/>. Defaults to
    /// generating a new <see cref="System.Guid"/>.
    /// </summary>
    public Func<string> CorrelationIdFactory { get; set; } = () => Guid.NewGuid().ToString("N");
}
