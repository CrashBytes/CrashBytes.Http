using System.Diagnostics;
using System.Net;

namespace CrashBytes.Http.Tests;

public class CorrelationIdHandlerTests
{
    private static HttpClient CreateClient(CorrelationIdHandler handler, RecordingHandler recorder)
    {
        handler.InnerHandler = recorder;
        return new HttpClient(handler) { BaseAddress = new Uri("http://test.local/") };
    }

    [Fact]
    public async Task DefaultHeaderName_IsAdded_WhenMissing()
    {
        var recorder = new RecordingHandler();
        var handler = new CorrelationIdHandler(new CorrelationIdOptions { UseActivityTraceId = false });
        var client = CreateClient(handler, recorder);

        var response = await client.GetAsync("/api");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(recorder.LastRequest);
        Assert.True(recorder.LastRequest!.Headers.Contains("X-Correlation-ID"));
    }

    [Fact]
    public async Task ExistingHeader_IsPreserved()
    {
        var recorder = new RecordingHandler();
        var handler = new CorrelationIdHandler(new CorrelationIdOptions { UseActivityTraceId = false });
        var client = CreateClient(handler, recorder);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api");
        request.Headers.Add("X-Correlation-ID", "existing-id-123");

        await client.SendAsync(request);

        var values = recorder.LastRequest!.Headers.GetValues("X-Correlation-ID").ToList();
        Assert.Single(values);
        Assert.Equal("existing-id-123", values[0]);
    }

    [Fact]
    public async Task CustomHeaderName_IsHonored()
    {
        var recorder = new RecordingHandler();
        var handler = new CorrelationIdHandler(new CorrelationIdOptions
        {
            HeaderName = "X-Request-Id",
            UseActivityTraceId = false
        });
        var client = CreateClient(handler, recorder);

        await client.GetAsync("/api");

        Assert.True(recorder.LastRequest!.Headers.Contains("X-Request-Id"));
        Assert.False(recorder.LastRequest!.Headers.Contains("X-Correlation-ID"));
    }

    [Fact]
    public async Task ActivityTraceId_IsUsed_WhenEnabledAndAvailable()
    {
        var recorder = new RecordingHandler();
        var handler = new CorrelationIdHandler(new CorrelationIdOptions { UseActivityTraceId = true });
        var client = CreateClient(handler, recorder);

        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var source = new ActivitySource("CorrelationIdHandlerTests");
        using var activity = source.StartActivity("test");
        Assert.NotNull(activity);

        await client.GetAsync("/api");

        var sentValue = recorder.LastRequest!.Headers.GetValues("X-Correlation-ID").Single();
        Assert.Equal(activity!.TraceId.ToString(), sentValue);
    }

    [Fact]
    public async Task CustomFactory_IsUsed_WhenNoTraceIdAvailable()
    {
        var recorder = new RecordingHandler();
        var handler = new CorrelationIdHandler(new CorrelationIdOptions
        {
            UseActivityTraceId = false,
            CorrelationIdFactory = () => "custom-factory-value"
        });
        var client = CreateClient(handler, recorder);

        await client.GetAsync("/api");

        Assert.Equal("custom-factory-value",
            recorder.LastRequest!.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public void NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CorrelationIdHandler((CorrelationIdOptions)null!));
    }

    [Fact]
    public async Task GeneratedId_IsReasonableGuid_WhenNoActivity()
    {
        var recorder = new RecordingHandler();
        var handler = new CorrelationIdHandler(new CorrelationIdOptions { UseActivityTraceId = false });
        var client = CreateClient(handler, recorder);

        await client.GetAsync("/api");

        var value = recorder.LastRequest!.Headers.GetValues("X-Correlation-ID").Single();
        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.True(value.Length >= 16);
    }
}
