using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Utility.Translation;
using Xunit;

namespace Utility.Tests;

public sealed class JsonResourceCacheTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "utility-cache-" + Guid.NewGuid().ToString("N")
    );

    [Fact]
    public async Task ConcurrentReadersShareOneDownloadAndPersistTheResult()
    {
        using var handler = new Handler(async ct =>
        {
            await Task.Delay(40, ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"name\":\"translated\"}"),
            };
        });
        using var client = new HttpClient(handler);
        var cache = Create(client);
        var tasks = new Task<Dictionary<string, string>?>[12];
        for (int i = 0; i < tasks.Length; i++)
            tasks[i] = cache.LoadAsync<Dictionary<string, string>>(
                "names",
                CachePath,
                "https://example.test/names",
                null
            );
        var results = await Task.WhenAll(tasks);
        Assert.Equal(1, handler.Calls);
        Assert.All(results, item => Assert.Equal("translated", item!["name"]));
        Assert.Contains("translated", await File.ReadAllTextAsync(CachePath));
    }

    [Fact]
    public async Task CancelledRequestDoesNotStartCooldownOrPublishPartialData()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int attempts = 0;
        using var handler = new Handler(async ct =>
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        });
        using var client = new HttpClient(handler);
        int failures = 0;
        var cache = Create(client, (_, _) => failures++);
        using var cancellation = new CancellationTokenSource();
        var pending = cache.LoadAsync<Dictionary<string, string>>(
            "names",
            CachePath,
            "https://example.test/names",
            null,
            cancellation.Token
        );
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.False(File.Exists(CachePath));
        Assert.Equal(0, failures);
        Assert.NotNull(
            await cache.LoadAsync<Dictionary<string, string>>(
                "names",
                CachePath,
                "https://example.test/names",
                null
            )
        );
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task HashMismatchKeepsExistingCacheAndReportsStaleFallback()
    {
        Directory.CreateDirectory(_directory);
        const string old = "{\"name\":\"old\"}";
        await File.WriteAllTextAsync(CachePath, old);
        using var handler = new Handler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"name\":\"untrusted\"}"),
                }
            )
        );
        using var client = new HttpClient(handler);
        CacheFallback? fallback = null;
        var cache = Create(client, (_, value) => fallback = value);
        var data = await cache.LoadAsync<Dictionary<string, string>>(
            "names",
            CachePath,
            "https://example.test/names",
            "expected"
        );
        Assert.Equal("old", data!["name"]);
        Assert.Equal(old, await File.ReadAllTextAsync(CachePath));
        Assert.Equal(CacheFallback.Stale, fallback);
    }

    [Fact]
    public async Task FailedResourceEntersCooldownWithoutBlockingAnotherResource()
    {
        using var handler = new Handler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
        );
        using var client = new HttpClient(handler);
        var cache = Create(client);
        Assert.Null(
            await cache.LoadAsync<object>("names", CachePath, "https://example.test/names", null)
        );
        Assert.Null(
            await cache.LoadAsync<object>("names", CachePath, "https://example.test/names", null)
        );
        Assert.Null(
            await cache.LoadAsync<object>("other", CachePath, "https://example.test/other", null)
        );
        Assert.Equal(2, handler.Calls);
    }

    private string CachePath => Path.Combine(_directory, "names.json");

    private static JsonResourceCache Create(
        HttpClient client,
        Action<string, CacheFallback>? fallback = null
    ) => new(client, json => json, _ => { }, _ => { }, fallback ?? ((_, _) => { }));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    private sealed class Handler(Func<CancellationToken, Task<HttpResponseMessage>> response)
        : HttpMessageHandler
    {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Interlocked.Increment(ref Calls);
            return response(cancellationToken);
        }
    }
}
