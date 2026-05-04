using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace CrashBytes.Http.Tests;

public class HttpClientBuilderExtensionsTests
{
    private sealed class TestTypedClient
    {
        public HttpClient Inner { get; }
        public TestTypedClient(HttpClient client) => Inner = client;
    }

    [Fact]
    public void AddCrashBytesHttpClient_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            HttpClientBuilderExtensions.AddCrashBytesHttpClient(null!, "name"));
    }

    [Fact]
    public void AddCrashBytesHttpClient_NullName_Throws()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddCrashBytesHttpClient(null!));
    }

    [Fact]
    public void AddCrashBytesHttpClient_RegistersNamedClient()
    {
        var services = new ServiceCollection();
        services.AddCrashBytesHttpClient("github", o =>
        {
            o.EnableResilience = false;
            o.EnableCorrelationId = false;
        });

        using var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient("github");
        Assert.NotNull(client);
    }

    [Fact]
    public void AddCrashBytesHttpClient_Typed_RegistersTypedClient()
    {
        var services = new ServiceCollection();
        services.AddCrashBytesHttpClient<TestTypedClient>("typed", o =>
        {
            o.EnableResilience = false;
            o.EnableCorrelationId = false;
        });

        using var sp = services.BuildServiceProvider();
        var typed = sp.GetRequiredService<TestTypedClient>();
        Assert.NotNull(typed.Inner);
    }

    [Fact]
    public async Task CorrelationIdHandler_IsWiredByDefault()
    {
        var recorder = new RecordingHandler();
        var services = new ServiceCollection();
        services.AddCrashBytesHttpClient("api", o =>
        {
            o.EnableResilience = false; // simplify pipeline
            o.CorrelationId = new CorrelationIdOptions { UseActivityTraceId = false };
        }).ConfigurePrimaryHttpMessageHandler(() => recorder);

        using var sp = services.BuildServiceProvider();
        var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient("api");
        client.BaseAddress = new Uri("http://test.local/");

        await client.GetAsync("/x");

        Assert.True(recorder.LastRequest!.Headers.Contains("X-Correlation-ID"));
    }

    [Fact]
    public async Task BearerTokenHandler_AttachesTokenWhenEnabled()
    {
        var recorder = new RecordingHandler();
        var services = new ServiceCollection();
        services.AddCrashBytesHttpClient("api", o =>
        {
            o.EnableResilience = false;
            o.EnableCorrelationId = false;
            o.EnableBearerToken = true;
            o.BearerToken.TokenProvider = _ => Task.FromResult("di-token");
        }).ConfigurePrimaryHttpMessageHandler(() => recorder);

        using var sp = services.BuildServiceProvider();
        var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient("api");
        client.BaseAddress = new Uri("http://test.local/");

        await client.GetAsync("/x");

        Assert.Equal("Bearer", recorder.LastRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("di-token", recorder.LastRequest!.Headers.Authorization?.Parameter);
    }

    [Fact]
    public void EnableBearerToken_WithoutProvider_ThrowsAtRegistration()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() =>
            services.AddCrashBytesHttpClient("api", o =>
            {
                o.EnableBearerToken = true;
                // intentionally leave TokenProvider null
            }));
    }

    [Fact]
    public async Task ResilienceHandler_RetriesTransientFailures_WhenEnabled()
    {
        int attempts = 0;
        var recorder = new RecordingHandler((req, _) =>
        {
            Interlocked.Increment(ref attempts);
            // standard resilience treats 500 as transient
            return new HttpResponseMessage(attempts < 3 ? HttpStatusCode.InternalServerError : HttpStatusCode.OK);
        });

        var services = new ServiceCollection();
        services.AddCrashBytesHttpClient("api", o =>
        {
            o.EnableCorrelationId = false;
            o.EnableResilience = true;
        }).ConfigurePrimaryHttpMessageHandler(() => recorder);

        using var sp = services.BuildServiceProvider();
        var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient("api");
        client.BaseAddress = new Uri("http://test.local/");
        client.Timeout = TimeSpan.FromSeconds(30);

        var response = await client.GetAsync("/x");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(attempts >= 2, $"Expected retries, got {attempts}");
    }

    [Fact]
    public async Task AddCorrelationIdHandler_StandaloneExtension_Works()
    {
        var recorder = new RecordingHandler();
        var services = new ServiceCollection();
        services.AddHttpClient("c1")
            .AddCorrelationIdHandler(o =>
            {
                o.UseActivityTraceId = false;
                o.HeaderName = "X-Trace";
            })
            .ConfigurePrimaryHttpMessageHandler(() => recorder);

        using var sp = services.BuildServiceProvider();
        var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient("c1");
        client.BaseAddress = new Uri("http://test.local/");

        await client.GetAsync("/x");

        Assert.True(recorder.LastRequest!.Headers.Contains("X-Trace"));
    }

    [Fact]
    public async Task AddBearerTokenHandler_StandaloneExtension_Works()
    {
        var recorder = new RecordingHandler();
        var services = new ServiceCollection();
        services.AddHttpClient("c1")
            .AddBearerTokenHandler(_ => Task.FromResult("standalone-token"))
            .ConfigurePrimaryHttpMessageHandler(() => recorder);

        using var sp = services.BuildServiceProvider();
        var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient("c1");
        client.BaseAddress = new Uri("http://test.local/");

        await client.GetAsync("/x");

        Assert.Equal("standalone-token", recorder.LastRequest!.Headers.Authorization?.Parameter);
    }

    [Fact]
    public void AddBearerTokenHandler_NullProvider_Throws()
    {
        var services = new ServiceCollection();
        var builder = services.AddHttpClient("c1");
        Assert.Throws<ArgumentNullException>(() => builder.AddBearerTokenHandler(null!));
    }

    [Fact]
    public void AddCorrelationIdHandler_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            HttpClientBuilderExtensions.AddCorrelationIdHandler(null!));
    }
}
