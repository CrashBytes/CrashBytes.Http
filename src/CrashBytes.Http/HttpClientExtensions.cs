using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace CrashBytes.Http;

/// <summary>
/// Extension methods for <see cref="HttpClient"/> providing JSON convenience methods,
/// retry with exponential backoff, correlation IDs, and bearer token support.
/// </summary>
public static class HttpClientExtensions
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ──────────────────────────────────────────────
    //  JSON convenience methods
    // ──────────────────────────────────────────────

    /// <summary>
    /// Sends a GET request and deserializes the JSON response to <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="requestUri"/> is <c>null</c>.</exception>
    /// <exception cref="HttpRequestException">The response status code does not indicate success.</exception>
    public static async Task<T?> GetJsonAsync<T>(
        this HttpClient client,
        string requestUri,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        if (requestUri is null) throw new ArgumentNullException(nameof(requestUri));

        var response = await client.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(options ?? DefaultJsonOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a POST request with a JSON body and deserializes the JSON response to <typeparamref name="TResponse"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="requestUri"/> is <c>null</c>.</exception>
    /// <exception cref="HttpRequestException">The response status code does not indicate success.</exception>
    public static async Task<TResponse?> PostJsonAsync<TRequest, TResponse>(
        this HttpClient client,
        string requestUri,
        TRequest payload,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        if (requestUri is null) throw new ArgumentNullException(nameof(requestUri));

        var opts = options ?? DefaultJsonOptions;
        var response = await client.PostAsJsonAsync(requestUri, payload, opts, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(opts, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a PUT request with a JSON body and deserializes the JSON response to <typeparamref name="TResponse"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="requestUri"/> is <c>null</c>.</exception>
    /// <exception cref="HttpRequestException">The response status code does not indicate success.</exception>
    public static async Task<TResponse?> PutJsonAsync<TRequest, TResponse>(
        this HttpClient client,
        string requestUri,
        TRequest payload,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        if (requestUri is null) throw new ArgumentNullException(nameof(requestUri));

        var opts = options ?? DefaultJsonOptions;
        var response = await client.PutAsJsonAsync(requestUri, payload, opts, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(opts, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a PATCH request with a JSON body and deserializes the JSON response to <typeparamref name="TResponse"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="requestUri"/> is <c>null</c>.</exception>
    /// <exception cref="HttpRequestException">The response status code does not indicate success.</exception>
    public static async Task<TResponse?> PatchJsonAsync<TRequest, TResponse>(
        this HttpClient client,
        string requestUri,
        TRequest payload,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        if (requestUri is null) throw new ArgumentNullException(nameof(requestUri));

        var opts = options ?? DefaultJsonOptions;
        var json = JsonSerializer.Serialize(payload, opts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PatchAsync(requestUri, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(opts, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a DELETE request and returns <c>true</c> if the response indicates success.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="requestUri"/> is <c>null</c>.</exception>
    public static async Task<bool> DeleteAsync(
        this HttpClient client,
        string requestUri,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        if (requestUri is null) throw new ArgumentNullException(nameof(requestUri));

        var response = await client.DeleteAsync(requestUri, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    // ──────────────────────────────────────────────
    //  Retry with exponential backoff
    // ──────────────────────────────────────────────

    /// <summary>
    /// Sends the request with automatic retry on transient failures using exponential backoff.
    /// Retries on 5xx status codes and <see cref="HttpRequestException"/>.
    /// </summary>
    /// <param name="client">The HTTP client.</param>
    /// <param name="requestFactory">A factory that creates the request for each attempt (requests cannot be reused).</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3).</param>
    /// <param name="initialDelay">Initial delay between retries (default: 1 second).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final <see cref="HttpResponseMessage"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="requestFactory"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxRetries"/> is negative.</exception>
    public static async Task<HttpResponseMessage> SendWithRetryAsync(
        this HttpClient client,
        Func<HttpRequestMessage> requestFactory,
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        if (requestFactory is null) throw new ArgumentNullException(nameof(requestFactory));
        if (maxRetries < 0) throw new ArgumentOutOfRangeException(nameof(maxRetries), "Max retries must not be negative.");

        var delay = initialDelay ?? TimeSpan.FromSeconds(1);
        HttpResponseMessage? response = null;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                response = await client.SendAsync(requestFactory(), cancellationToken).ConfigureAwait(false);

                if ((int)response.StatusCode < 500 || attempt == maxRetries)
                    return response;
            }
            catch (HttpRequestException) when (attempt < maxRetries)
            {
                // Will retry
            }

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            delay = TimeSpan.FromTicks(delay.Ticks * 2); // exponential backoff
        }

        return response!;
    }

    // ──────────────────────────────────────────────
    //  Header helpers
    // ──────────────────────────────────────────────

    /// <summary>
    /// Sets the Authorization header to a Bearer token.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="token"/> is <c>null</c>.</exception>
    public static HttpClient WithBearerToken(this HttpClient client, string token)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        if (token is null) throw new ArgumentNullException(nameof(token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Adds a correlation ID header to the client's default request headers.
    /// </summary>
    /// <param name="client">The HTTP client.</param>
    /// <param name="correlationId">The correlation ID. If <c>null</c>, a new GUID is generated.</param>
    /// <param name="headerName">The header name (default: "X-Correlation-ID").</param>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is <c>null</c>.</exception>
    public static HttpClient WithCorrelationId(
        this HttpClient client,
        string? correlationId = null,
        string headerName = "X-Correlation-ID")
    {
        if (client is null) throw new ArgumentNullException(nameof(client));

        client.DefaultRequestHeaders.Remove(headerName);
        client.DefaultRequestHeaders.Add(headerName, correlationId ?? Guid.NewGuid().ToString());
        return client;
    }

    /// <summary>
    /// Sets the base address of the HTTP client. Returns the client for fluent chaining.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="baseAddress"/> is <c>null</c>.</exception>
    public static HttpClient WithBaseAddress(this HttpClient client, string baseAddress)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        if (baseAddress is null) throw new ArgumentNullException(nameof(baseAddress));

        client.BaseAddress = new Uri(baseAddress);
        return client;
    }

    /// <summary>
    /// Sets a default request header. Returns the client for fluent chaining.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is <c>null</c>.</exception>
    public static HttpClient WithHeader(this HttpClient client, string name, string value)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));

        client.DefaultRequestHeaders.Remove(name);
        client.DefaultRequestHeaders.Add(name, value);
        return client;
    }

    /// <summary>
    /// Sets the request timeout. Returns the client for fluent chaining.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is <c>null</c>.</exception>
    public static HttpClient WithTimeout(this HttpClient client, TimeSpan timeout)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));

        client.Timeout = timeout;
        return client;
    }
}
