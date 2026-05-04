using System.Net;

namespace CrashBytes.Http.Tests;

/// <summary>
/// Test double that records every <see cref="HttpRequestMessage"/> it sees and returns a
/// configurable canned response. Useful for asserting outgoing request shape (headers, body)
/// produced by delegating handlers.
/// </summary>
internal sealed class RecordingHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _responder;
    private readonly object _gate = new();
    private readonly List<HttpRequestMessage> _requests = new();

    public IReadOnlyList<HttpRequestMessage> Requests
    {
        get
        {
            lock (_gate) return _requests.ToArray();
        }
    }

    public int CallCount
    {
        get { lock (_gate) return _requests.Count; }
    }

    public HttpRequestMessage? LastRequest
    {
        get { lock (_gate) return _requests.Count == 0 ? null : _requests[^1]; }
    }

    public RecordingHandler()
        : this((_, _) => new HttpResponseMessage(HttpStatusCode.OK))
    {
    }

    public RecordingHandler(HttpStatusCode status)
        : this((_, _) => new HttpResponseMessage(status))
    {
    }

    public RecordingHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Snapshot headers before continuation: HttpClient may dispose the request after send.
        var snapshot = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            snapshot.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        int index;
        lock (_gate)
        {
            _requests.Add(snapshot);
            index = _requests.Count;
        }

        var response = _responder(request, index);
        return Task.FromResult(response);
    }
}
