using System.Net;
using System.Text.Json;

namespace CrashBytes.Http.Tests;

// ──────────────────────────────────────────────
//  Test infrastructure
// ──────────────────────────────────────────────

public class MockHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public MockHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    public MockHandler(HttpResponseMessage response)
        : this((_, _) => Task.FromResult(response))
    {
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        _handler(request, cancellationToken);
}

public record TestPayload(int Id, string Name);

// ──────────────────────────────────────────────
//  JSON method tests
// ──────────────────────────────────────────────

public class GetJsonAsyncTests
{
    [Fact]
    public async Task GetJsonAsync_Success_DeserializesResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":1,\"name\":\"test\"}", System.Text.Encoding.UTF8, "application/json")
        };
        var client = new HttpClient(new MockHandler(response)) { BaseAddress = new Uri("http://test.com") };

        var result = await client.GetJsonAsync<TestPayload>("/api/data");
        Assert.NotNull(result);
        Assert.Equal(1, result!.Id);
        Assert.Equal("test", result.Name);
    }

    [Fact]
    public async Task GetJsonAsync_NonSuccess_ThrowsHttpRequestException()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var client = new HttpClient(new MockHandler(response)) { BaseAddress = new Uri("http://test.com") };

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetJsonAsync<TestPayload>("/api/data"));
    }

    [Fact]
    public async Task GetJsonAsync_NullClient_ThrowsArgumentNullException()
    {
        HttpClient client = null!;
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.GetJsonAsync<TestPayload>("/api/data"));
    }

    [Fact]
    public async Task GetJsonAsync_NullUri_ThrowsArgumentNullException()
    {
        var client = new HttpClient(new MockHandler(new HttpResponseMessage(HttpStatusCode.OK)));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.GetJsonAsync<TestPayload>(null!));
    }

    [Fact]
    public async Task GetJsonAsync_CustomOptions_UsesOptions()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"Id\":1,\"Name\":\"test\"}", System.Text.Encoding.UTF8, "application/json")
        };
        var client = new HttpClient(new MockHandler(response)) { BaseAddress = new Uri("http://test.com") };
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var result = await client.GetJsonAsync<TestPayload>("/api/data", options);
        Assert.NotNull(result);
        Assert.Equal(1, result!.Id);
    }
}

public class PostJsonAsyncTests
{
    [Fact]
    public async Task PostJsonAsync_Success_DeserializesResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":2,\"name\":\"created\"}", System.Text.Encoding.UTF8, "application/json")
        };
        var client = new HttpClient(new MockHandler(response)) { BaseAddress = new Uri("http://test.com") };

        var result = await client.PostJsonAsync<TestPayload, TestPayload>("/api/data", new TestPayload(1, "input"));
        Assert.NotNull(result);
        Assert.Equal(2, result!.Id);
    }

    [Fact]
    public async Task PostJsonAsync_NonSuccess_ThrowsHttpRequestException()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest);
        var client = new HttpClient(new MockHandler(response)) { BaseAddress = new Uri("http://test.com") };

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.PostJsonAsync<TestPayload, TestPayload>("/api/data", new TestPayload(1, "x")));
    }

    [Fact]
    public async Task PostJsonAsync_NullClient_ThrowsArgumentNullException()
    {
        HttpClient client = null!;
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.PostJsonAsync<TestPayload, TestPayload>("/api", new TestPayload(1, "x")));
    }

    [Fact]
    public async Task PostJsonAsync_NullUri_ThrowsArgumentNullException()
    {
        var client = new HttpClient(new MockHandler(new HttpResponseMessage(HttpStatusCode.OK))) { BaseAddress = new Uri("http://test.com") };
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.PostJsonAsync<TestPayload, TestPayload>(null!, new TestPayload(1, "x")));
    }

    [Fact]
    public async Task PostJsonAsync_CustomOptions_Works()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"Id\":2,\"Name\":\"ok\"}", System.Text.Encoding.UTF8, "application/json")
        };
        var client = new HttpClient(new MockHandler(response)) { BaseAddress = new Uri("http://test.com") };
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var result = await client.PostJsonAsync<TestPayload, TestPayload>("/api", new TestPayload(1, "x"), options);
        Assert.NotNull(result);
    }
}

public class PutJsonAsyncTests
{
    [Fact]
    public async Task PutJsonAsync_Success_DeserializesResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":1,\"name\":\"updated\"}", System.Text.Encoding.UTF8, "application/json")
        };
        var client = new HttpClient(new MockHandler(response)) { BaseAddress = new Uri("http://test.com") };

        var result = await client.PutJsonAsync<TestPayload, TestPayload>("/api/data/1", new TestPayload(1, "updated"));
        Assert.NotNull(result);
        Assert.Equal("updated", result!.Name);
    }

    [Fact]
    public async Task PutJsonAsync_NullClient_ThrowsArgumentNullException()
    {
        HttpClient client = null!;
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.PutJsonAsync<TestPayload, TestPayload>("/api", new TestPayload(1, "x")));
    }

    [Fact]
    public async Task PutJsonAsync_NullUri_ThrowsArgumentNullException()
    {
        var client = new HttpClient(new MockHandler(new HttpResponseMessage(HttpStatusCode.OK))) { BaseAddress = new Uri("http://test.com") };
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.PutJsonAsync<TestPayload, TestPayload>(null!, new TestPayload(1, "x")));
    }
}

public class PatchJsonAsyncTests
{
    [Fact]
    public async Task PatchJsonAsync_Success_DeserializesResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":1,\"name\":\"patched\"}", System.Text.Encoding.UTF8, "application/json")
        };
        var client = new HttpClient(new MockHandler(response)) { BaseAddress = new Uri("http://test.com") };

        var result = await client.PatchJsonAsync<TestPayload, TestPayload>("/api/data/1", new TestPayload(1, "patched"));
        Assert.NotNull(result);
        Assert.Equal("patched", result!.Name);
    }

    [Fact]
    public async Task PatchJsonAsync_NullClient_ThrowsArgumentNullException()
    {
        HttpClient client = null!;
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.PatchJsonAsync<TestPayload, TestPayload>("/api", new TestPayload(1, "x")));
    }

    [Fact]
    public async Task PatchJsonAsync_NullUri_ThrowsArgumentNullException()
    {
        var client = new HttpClient(new MockHandler(new HttpResponseMessage(HttpStatusCode.OK))) { BaseAddress = new Uri("http://test.com") };
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.PatchJsonAsync<TestPayload, TestPayload>(null!, new TestPayload(1, "x")));
    }
}

public class DeleteAsyncExtensionTests
{
    [Fact]
    public async Task DeleteAsync_Success_ReturnsTrue()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NoContent);
        var client = new HttpClient(new MockHandler(response)) { BaseAddress = new Uri("http://test.com") };

        Assert.True(await HttpClientExtensions.DeleteAsync(client, "/api/data/1"));
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsFalse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var client = new HttpClient(new MockHandler(response)) { BaseAddress = new Uri("http://test.com") };

        Assert.False(await HttpClientExtensions.DeleteAsync(client, "/api/data/999"));
    }

    [Fact]
    public async Task DeleteAsync_NullClient_ThrowsArgumentNullException()
    {
        HttpClient client = null!;
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            HttpClientExtensions.DeleteAsync(client, "/api"));
    }

    [Fact]
    public async Task DeleteAsync_NullUri_ThrowsArgumentNullException()
    {
        var client = new HttpClient(new MockHandler(new HttpResponseMessage(HttpStatusCode.OK)));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            HttpClientExtensions.DeleteAsync(client, null!));
    }
}

// ──────────────────────────────────────────────
//  Retry tests
// ──────────────────────────────────────────────

public class SendWithRetryAsyncTests
{
    [Fact]
    public async Task SendWithRetry_SuccessOnFirstAttempt_ReturnsImmediately()
    {
        int attempts = 0;
        var handler = new MockHandler((req, ct) =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var client = new HttpClient(handler);

        var result = await client.SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "http://test.com/api"),
            maxRetries: 3, initialDelay: TimeSpan.FromMilliseconds(1));

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task SendWithRetry_ServerError_RetriesAndSucceeds()
    {
        int attempts = 0;
        var handler = new MockHandler((req, ct) =>
        {
            attempts++;
            var status = attempts < 3 ? HttpStatusCode.InternalServerError : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status));
        });
        var client = new HttpClient(handler);

        var result = await client.SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "http://test.com/api"),
            maxRetries: 3, initialDelay: TimeSpan.FromMilliseconds(1));

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task SendWithRetry_AllAttemptsFail_ReturnsLastResponse()
    {
        int attempts = 0;
        var handler = new MockHandler((req, ct) =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });
        var client = new HttpClient(handler);

        var result = await client.SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "http://test.com/api"),
            maxRetries: 2, initialDelay: TimeSpan.FromMilliseconds(1));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
        Assert.Equal(3, attempts); // 1 initial + 2 retries
    }

    [Fact]
    public async Task SendWithRetry_ClientError_DoesNotRetry()
    {
        int attempts = 0;
        var handler = new MockHandler((req, ct) =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        });
        var client = new HttpClient(handler);

        var result = await client.SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "http://test.com/api"),
            maxRetries: 3, initialDelay: TimeSpan.FromMilliseconds(1));

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task SendWithRetry_NullClient_ThrowsArgumentNullException()
    {
        HttpClient client = null!;
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.SendWithRetryAsync(() => new HttpRequestMessage()));
    }

    [Fact]
    public async Task SendWithRetry_NegativeRetries_ThrowsArgumentOutOfRangeException()
    {
        var client = new HttpClient(new MockHandler(new HttpResponseMessage(HttpStatusCode.OK)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.SendWithRetryAsync(() => new HttpRequestMessage(), maxRetries: -1));
    }

    [Fact]
    public async Task SendWithRetry_NullRequestFactory_ThrowsArgumentNullException()
    {
        var client = new HttpClient(new MockHandler(new HttpResponseMessage(HttpStatusCode.OK)));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.SendWithRetryAsync(null!));
    }

    [Fact]
    public async Task SendWithRetry_HttpRequestException_RetriesAndSucceeds()
    {
        int attempts = 0;
        var handler = new MockHandler((req, ct) =>
        {
            attempts++;
            if (attempts < 3)
                throw new HttpRequestException("Connection refused");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var client = new HttpClient(handler);

        var result = await client.SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "http://test.com/api"),
            maxRetries: 3, initialDelay: TimeSpan.FromMilliseconds(1));

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task SendWithRetry_HttpRequestException_AllFail_Throws()
    {
        var handler = new MockHandler((req, ct) =>
        {
            throw new HttpRequestException("Connection refused");
        });
        var client = new HttpClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SendWithRetryAsync(
                () => new HttpRequestMessage(HttpMethod.Get, "http://test.com/api"),
                maxRetries: 1, initialDelay: TimeSpan.FromMilliseconds(1)));
    }

    [Fact]
    public async Task SendWithRetry_ZeroRetries_SingleAttempt()
    {
        int attempts = 0;
        var handler = new MockHandler((req, ct) =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        });
        var client = new HttpClient(handler);

        var result = await client.SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "http://test.com/api"),
            maxRetries: 0, initialDelay: TimeSpan.FromMilliseconds(1));

        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task SendWithRetry_DefaultDelay_Works()
    {
        int attempts = 0;
        var handler = new MockHandler((req, ct) =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var client = new HttpClient(handler);

        // Use default initialDelay (null -> 1 second) but only 1 attempt so no wait
        var result = await client.SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "http://test.com/api"),
            maxRetries: 0);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(1, attempts);
    }
}

// ──────────────────────────────────────────────
//  Header helper tests
// ──────────────────────────────────────────────

public class WithBearerTokenTests
{
    [Fact]
    public void WithBearerToken_SetsAuthorizationHeader()
    {
        var client = new HttpClient();
        client.WithBearerToken("my-token");
        Assert.Equal("Bearer", client.DefaultRequestHeaders.Authorization?.Scheme);
        Assert.Equal("my-token", client.DefaultRequestHeaders.Authorization?.Parameter);
    }

    [Fact]
    public void WithBearerToken_NullClient_ThrowsArgumentNullException()
    {
        HttpClient client = null!;
        Assert.Throws<ArgumentNullException>(() => client.WithBearerToken("token"));
    }

    [Fact]
    public void WithBearerToken_NullToken_ThrowsArgumentNullException()
    {
        var client = new HttpClient();
        Assert.Throws<ArgumentNullException>(() => client.WithBearerToken(null!));
    }
}

public class WithCorrelationIdTests
{
    [Fact]
    public void WithCorrelationId_SetsHeader()
    {
        var client = new HttpClient();
        client.WithCorrelationId("abc-123");
        Assert.Contains("abc-123", client.DefaultRequestHeaders.GetValues("X-Correlation-ID"));
    }

    [Fact]
    public void WithCorrelationId_NullId_GeneratesGuid()
    {
        var client = new HttpClient();
        client.WithCorrelationId();
        var value = client.DefaultRequestHeaders.GetValues("X-Correlation-ID").First();
        Assert.True(Guid.TryParse(value, out _));
    }

    [Fact]
    public void WithCorrelationId_CustomHeaderName()
    {
        var client = new HttpClient();
        client.WithCorrelationId("id-1", "X-Request-ID");
        Assert.Contains("id-1", client.DefaultRequestHeaders.GetValues("X-Request-ID"));
    }

    [Fact]
    public void WithCorrelationId_NullClient_ThrowsArgumentNullException()
    {
        HttpClient client = null!;
        Assert.Throws<ArgumentNullException>(() => client.WithCorrelationId());
    }
}

public class WithBaseAddressTests
{
    [Fact]
    public void WithBaseAddress_SetsBaseAddress()
    {
        var client = new HttpClient();
        client.WithBaseAddress("https://api.example.com");
        Assert.Equal(new Uri("https://api.example.com"), client.BaseAddress);
    }

    [Fact]
    public void WithBaseAddress_NullClient_ThrowsArgumentNullException()
    {
        HttpClient client = null!;
        Assert.Throws<ArgumentNullException>(() => client.WithBaseAddress("https://test.com"));
    }

    [Fact]
    public void WithBaseAddress_NullAddress_ThrowsArgumentNullException()
    {
        var client = new HttpClient();
        Assert.Throws<ArgumentNullException>(() => client.WithBaseAddress(null!));
    }
}

public class WithHeaderTests
{
    [Fact]
    public void WithHeader_SetsDefaultHeader()
    {
        var client = new HttpClient();
        client.WithHeader("X-Custom", "value1");
        Assert.Contains("value1", client.DefaultRequestHeaders.GetValues("X-Custom"));
    }

    [Fact]
    public void WithHeader_ReplacesExistingHeader()
    {
        var client = new HttpClient();
        client.WithHeader("X-Custom", "old");
        client.WithHeader("X-Custom", "new");
        var values = client.DefaultRequestHeaders.GetValues("X-Custom").ToList();
        Assert.Single(values);
        Assert.Equal("new", values[0]);
    }

    [Fact]
    public void WithHeader_NullClient_ThrowsArgumentNullException()
    {
        HttpClient client = null!;
        Assert.Throws<ArgumentNullException>(() => client.WithHeader("key", "value"));
    }
}

public class WithTimeoutTests
{
    [Fact]
    public void WithTimeout_SetsTimeout()
    {
        var client = new HttpClient();
        client.WithTimeout(TimeSpan.FromSeconds(30));
        Assert.Equal(TimeSpan.FromSeconds(30), client.Timeout);
    }

    [Fact]
    public void WithTimeout_NullClient_ThrowsArgumentNullException()
    {
        HttpClient client = null!;
        Assert.Throws<ArgumentNullException>(() => client.WithTimeout(TimeSpan.FromSeconds(5)));
    }
}
