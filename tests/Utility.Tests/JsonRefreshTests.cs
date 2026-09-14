using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Utility.Caching;
using Xunit;

namespace Utility.Tests;

public sealed class JsonRefreshTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "json-refresh-" + Guid.NewGuid().ToString("N")
    );
    private string CachePath => Path.Combine(_directory, "resource.json");

    [Theory]
    [InlineData(JsonCachePolicy.PreferLocal)]
    [InlineData(JsonCachePolicy.LocalOnly)]
    public async Task LocalPoliciesReadEditsAndNeverRequireAValidHash(JsonCachePolicy policy)
    {
        Directory.CreateDirectory(_directory);
        using var handler = new Handler(_ =>
            throw new InvalidOperationException("Must not download")
        );
        using var client = new HttpClient(handler);
        var cache = Create(client);
        await File.WriteAllTextAsync(CachePath, "{\"value\":\"first\"}");
        Assert.Equal("first", (await Load(cache, policy, _ => false))!["value"]);
        await File.WriteAllTextAsync(CachePath, "{\"value\":\"edited\"}");
        Assert.Equal("edited", (await Load(cache, policy, _ => false))!["value"]);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task LocalOnlyDoesNotFetchMissingOrCorruptFiles()
    {
        using var handler = new Handler(_ =>
            throw new InvalidOperationException("Must not download")
        );
        using var client = new HttpClient(handler);
        var cache = Create(client);
        Assert.Null(await Load(cache, JsonCachePolicy.LocalOnly));
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(CachePath, "not json");
        Assert.Null(await Load(cache, JsonCachePolicy.LocalOnly));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task TypedValidatorAvoidsDownloadAndRejectsBadRemoteWithoutOverwritingDisk()
    {
        using var handler = new Handler(_ => Task.FromResult(Response("bad")));
        using var client = new HttpClient(handler);
        var cache = Create(client);
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(CachePath, "{\"value\":\"valid\"}");
        Assert.Equal("valid", (await Load(cache, validate: x => x["value"] == "valid"))!["value"]);
        Assert.Equal(0, handler.Calls);
        Assert.Equal("valid", (await Load(cache, validate: x => x["value"] == "new"))!["value"]);
        Assert.Contains("valid", await File.ReadAllTextAsync(CachePath));
        File.Delete(CachePath);
        Assert.Null(await Load(cache, validate: _ => false));
        Assert.False(File.Exists(CachePath));
    }

    [Fact]
    public async Task RefreshWithoutHashDownloadsAgainAndUsesStaleDataAfterFailure()
    {
        int attempt = 0;
        using var handler = new Handler(_ =>
            Task.FromResult(
                ++attempt <= 2
                    ? Response(attempt.ToString())
                    : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            )
        );
        using var client = new HttpClient(handler);
        var cache = Create(client);
        Assert.Equal("1", (await Load(cache))!["value"]);
        Assert.Equal("2", (await Load(cache))!["value"]);
        Assert.Equal("2", (await Load(cache))!["value"]);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task CorruptPreferredCacheIsRepairedAndWriteFailureDoesNotDiscardDownload()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(CachePath, "{");
        using var handler = new Handler(_ => Task.FromResult(Response("valid")));
        using var client = new HttpClient(handler);
        var cache = Create(client);
        Assert.Equal("valid", (await Load(cache, JsonCachePolicy.PreferLocal))!["value"]);
        Assert.Contains("valid", await File.ReadAllTextAsync(CachePath));
        File.Delete(CachePath);
        Directory.CreateDirectory(CachePath);
        Assert.Equal("valid", (await Load(cache))!["value"]);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task CancellationKeepsOldFileAndReleasesTheResourceLock()
    {
        Directory.CreateDirectory(_directory);
        const string original = "{\"value\":\"old\"}";
        await File.WriteAllTextAsync(CachePath, original);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int attempt = 0;
        using var handler = new Handler(async token =>
        {
            if (Interlocked.Increment(ref attempt) == 1)
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
            }
            return Response("new");
        });
        using var client = new HttpClient(handler);
        int failures = 0;
        var cache = new JsonResourceCache(client, _ => { }, _ => { }, (_, _) => failures++);
        using var cts = new CancellationTokenSource();
        var pending = Load(cache, token: cts.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(original, await File.ReadAllTextAsync(CachePath));
        Assert.Equal(0, failures);
        Assert.Equal("new", (await Load(cache))!["value"]);
    }

    [Fact]
    public async Task WebJsonIsNormalizedForSubsequentLocalReads()
    {
        using var handler = new Handler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"name\":\"中文\",\"count\":\"12\"}"),
                }
            )
        );
        using var client = new HttpClient(handler);
        var cache = Create(client);
        var value = await cache.RefreshAsync<Example>(
            "object",
            CachePath,
            "https://example.test/object"
        );
        var local = await cache.LoadLocalAsync<Example>(CachePath);
        Assert.Equal("中文", value!.Name);
        Assert.Equal(value.Name, local!.Name);
        Assert.Equal(12, local.Count);
    }

    public sealed class Example
    {
        public string? Name { get; set; }
        public int Count { get; set; }
    }

    private Task<Dictionary<string, string>?> Load(
        JsonResourceCache cache,
        JsonCachePolicy policy = JsonCachePolicy.Refresh,
        Func<Dictionary<string, string>, bool>? validate = null,
        CancellationToken token = default
    ) =>
        cache.RefreshAsync(
            "resource",
            CachePath,
            "https://example.test/resource",
            policy,
            validate,
            token
        );

    private static JsonResourceCache Create(HttpClient client) => new(client, _ => { }, _ => { });

    private static HttpResponseMessage Response(string value) =>
        new(HttpStatusCode.OK) { Content = new StringContent("{\"value\":\"" + value + "\"}") };

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    private sealed class Handler(Func<CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Interlocked.Increment(ref Calls);
            return respond(cancellationToken);
        }
    }
}
