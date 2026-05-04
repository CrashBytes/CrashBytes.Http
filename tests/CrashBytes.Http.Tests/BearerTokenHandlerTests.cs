using System.Net;

namespace CrashBytes.Http.Tests;

public class BearerTokenHandlerTests
{
    private static HttpClient CreateClient(BearerTokenHandler handler, RecordingHandler recorder)
    {
        handler.InnerHandler = recorder;
        return new HttpClient(handler) { BaseAddress = new Uri("http://test.local/") };
    }

    [Fact]
    public async Task AttachesBearerHeader_FromTokenProvider()
    {
        var recorder = new RecordingHandler();
        var handler = new BearerTokenHandler(_ => Task.FromResult("abc-token"));
        var client = CreateClient(handler, recorder);

        await client.GetAsync("/api");

        var auth = recorder.LastRequest!.Headers.Authorization;
        Assert.NotNull(auth);
        Assert.Equal("Bearer", auth!.Scheme);
        Assert.Equal("abc-token", auth.Parameter);
    }

    [Fact]
    public async Task TokenIsCached_WithinLifetime()
    {
        int calls = 0;
        var recorder = new RecordingHandler();
        var handler = new BearerTokenHandler(new BearerTokenOptions
        {
            TokenProvider = _ =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult("token-" + calls);
            },
            TokenLifetime = TimeSpan.FromMinutes(1),
        });
        var client = CreateClient(handler, recorder);

        await client.GetAsync("/a");
        await client.GetAsync("/b");
        await client.GetAsync("/c");

        Assert.Equal(1, calls);
        Assert.All(recorder.Requests, r =>
            Assert.Equal("token-1", r.Headers.Authorization?.Parameter));
    }

    [Fact]
    public async Task ZeroLifetime_FetchesEveryRequest()
    {
        int calls = 0;
        var recorder = new RecordingHandler();
        var handler = new BearerTokenHandler(new BearerTokenOptions
        {
            TokenProvider = _ =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult("t-" + calls);
            },
            TokenLifetime = TimeSpan.Zero,
        });
        var client = CreateClient(handler, recorder);

        await client.GetAsync("/a");
        await client.GetAsync("/b");

        Assert.Equal(2, calls);
        Assert.Equal("t-1", recorder.Requests[0].Headers.Authorization?.Parameter);
        Assert.Equal("t-2", recorder.Requests[1].Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task InvalidateCachedToken_ForcesRefresh()
    {
        int calls = 0;
        var recorder = new RecordingHandler();
        var handler = new BearerTokenHandler(new BearerTokenOptions
        {
            TokenProvider = _ =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult("v-" + calls);
            },
            TokenLifetime = TimeSpan.FromMinutes(10),
        });
        var client = CreateClient(handler, recorder);

        await client.GetAsync("/a");
        Assert.Equal(1, calls);

        handler.InvalidateCachedToken();

        await client.GetAsync("/b");
        Assert.Equal(2, calls);
        Assert.Equal("v-2", recorder.Requests[1].Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task PreservesExistingAuthorization_WhenConfigured()
    {
        var recorder = new RecordingHandler();
        var handler = new BearerTokenHandler(new BearerTokenOptions
        {
            TokenProvider = _ => Task.FromResult("default-token"),
            PreserveExistingAuthorization = true,
        });
        var client = CreateClient(handler, recorder);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", "user:pw");

        await client.SendAsync(request);

        Assert.Equal("Basic", recorder.LastRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("user:pw", recorder.LastRequest!.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task OverwritesExistingAuthorization_WhenConfigured()
    {
        var recorder = new RecordingHandler();
        var handler = new BearerTokenHandler(new BearerTokenOptions
        {
            TokenProvider = _ => Task.FromResult("override-token"),
            PreserveExistingAuthorization = false,
        });
        var client = CreateClient(handler, recorder);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", "old");

        await client.SendAsync(request);

        Assert.Equal("Bearer", recorder.LastRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("override-token", recorder.LastRequest!.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task CustomScheme_IsUsed()
    {
        var recorder = new RecordingHandler();
        var handler = new BearerTokenHandler(new BearerTokenOptions
        {
            TokenProvider = _ => Task.FromResult("token"),
            Scheme = "DPoP",
        });
        var client = CreateClient(handler, recorder);

        await client.GetAsync("/api");

        Assert.Equal("DPoP", recorder.LastRequest!.Headers.Authorization?.Scheme);
    }

    [Fact]
    public void NullProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BearerTokenHandler((Func<CancellationToken, Task<string>>)null!));
    }

    [Fact]
    public void OptionsWithoutProvider_ThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new BearerTokenHandler(new BearerTokenOptions()));
    }

    [Fact]
    public async Task EmptyToken_ThrowsInvalidOperation()
    {
        var recorder = new RecordingHandler();
        var handler = new BearerTokenHandler(_ => Task.FromResult(string.Empty));
        var client = CreateClient(handler, recorder);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync("/api"));
    }

    [Fact]
    public async Task ConcurrentRequests_FetchTokenOnce()
    {
        int calls = 0;
        var recorder = new RecordingHandler();
        var handler = new BearerTokenHandler(new BearerTokenOptions
        {
            TokenProvider = async _ =>
            {
                Interlocked.Increment(ref calls);
                await Task.Delay(20);
                return "concurrent-token";
            },
            TokenLifetime = TimeSpan.FromMinutes(1),
        });
        var client = CreateClient(handler, recorder);

        var tasks = Enumerable.Range(0, 8).Select(_ => client.GetAsync("/api")).ToArray();
        await Task.WhenAll(tasks);

        Assert.Equal(1, calls);
        Assert.Equal(8, recorder.CallCount);
    }
}
