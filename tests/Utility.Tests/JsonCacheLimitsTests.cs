using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Utility.Caching;
using Xunit;

namespace Utility.Tests;

public sealed class JsonCacheLimitsTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "cache-limits-" + Guid.NewGuid().ToString("N")
    );

    private string PathFor(string key) => Path.Combine(_root, key + ".json");

    [Fact]
    public async Task RetentionEvictsLeastRecentlyUsedResourcesAndReleasesGates()
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json("{\"v\":\"remote\"}")));
        using var client = new HttpClient(handler);
        var cache = Create(client, new() { MaximumRetainedResources = 2 });
        await Load(cache, "a");
        await Load(cache, "b");
        await Load(cache, "a"); // a is newer than b.
        await Load(cache, "c");
        Assert.Equal(2, cache.RetainedResourceCount);
        await Load(cache, "a");
        Assert.Equal(3, handler.Calls);
        await Load(cache, "b");
        Assert.Equal(4, handler.Calls);
        Assert.Equal(2, cache.RetainedResourceCount);
        Assert.Equal(0, cache.ActiveResourceGateCount);
    }

    [Fact]
    public async Task FailureCooldownsAreBoundedAndInvalidationPermitsImmediateRetry()
    {
        using var handler = new Handler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
        );
        using var client = new HttpClient(handler);
        var cache = Create(client, new() { MaximumRetainedResources = 2 });
        await Load(cache, "a");
        await Load(cache, "a");
        Assert.Equal(1, handler.Calls);
        await Load(cache, "b");
        await Load(cache, "c");
        Assert.Equal(2, cache.RetainedResourceCount);
        await cache.InvalidateAsync("c");
        await Load(cache, "c");
        Assert.Equal(4, handler.Calls);
        Assert.Equal(0, cache.ActiveResourceGateCount);
    }

    [Fact]
    public async Task ZeroRetentionLeavesDiskCachingAvailable()
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json("{}")));
        using var client = new HttpClient(handler);
        var cache = Create(client, new() { MaximumRetainedResources = 0 });
        await Load(cache, "a");
        await Load(cache, "a");
        Assert.Equal(2, handler.Calls);
        Assert.Equal(0, cache.RetainedResourceCount);
        Assert.True(File.Exists(PathFor("a")));
    }

    [Fact]
    public async Task InvalidationWaitsForInFlightPublicationAndKeepsDiskFiles()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler(
            async (_, token) =>
            {
                started.TrySetResult();
                await finish.Task.WaitAsync(token);
                return Json("{}");
            }
        );
        using var client = new HttpClient(handler);
        var cache = Create(client, new());
        var load = Load(cache, "a");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var invalidate = cache.InvalidateAsync("a");
        Assert.False(invalidate.IsCompleted);
        using var cancelled = new CancellationTokenSource();
        var waiter = cache.InvalidateAsync("a", cancelled.Token);
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
        finish.SetResult();
        await load;
        await invalidate;
        Assert.Equal(0, cache.RetainedResourceCount);
        Assert.Equal(0, cache.ActiveResourceGateCount);
        Assert.True(File.Exists(PathFor("a")));
        await Load(cache, "a");
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BodyLimitRejectsDeclaredAndChunkedResponsesWithoutOverwritingCache(
        bool declared
    )
    {
        Directory.CreateDirectory(_root);
        const string old = "{\"v\":\"old\"}";
        await File.WriteAllTextAsync(PathFor("a"), old);
        var stream = new BodyStream(
            Encoding.UTF8.GetBytes("{\"v\":\"" + new string('x', 100) + "\"}")
        );
        using var handler = new Handler(
            (_, _) =>
            {
                var content = new StreamContent(stream);
                if (declared)
                    content.Headers.ContentLength = 108;
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK) { Content = content }
                );
            }
        );
        using var client = new HttpClient(handler);
        var cache = Create(client, new() { MaximumDownloadBytes = 16 });
        var value = await cache.RefreshAsync<Dictionary<string, string>>(
            "a",
            PathFor("a"),
            "https://example.test/a"
        );
        Assert.Equal("old", value!["v"]);
        Assert.Equal(old, await File.ReadAllTextAsync(PathFor("a")));
        Assert.InRange(stream.BytesRead, 0, declared ? 0 : 17);
        Assert.True(stream.Disposed);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public async Task ExactBodyLimitAndUtf16CharsetAreSupported()
    {
        byte[] bytes = Encoding.Unicode.GetBytes("{\"v\":\"中文\"}");
        using var handler = new Handler(
            (_, _) =>
            {
                var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new("application/json") { CharSet = "utf-16" };
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK) { Content = content }
                );
            }
        );
        using var client = new HttpClient(handler);
        var cache = Create(client, new() { MaximumDownloadBytes = bytes.Length });
        Assert.Equal("中文", (await Load(cache, "a"))!["v"]);
    }

    [Fact]
    public async Task HttpClientTimeoutStillCoversStreamingBodyAndFallsBack()
    {
        var stream = new BodyStream(Array.Empty<byte>(), stall: true);
        using var handler = new Handler(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StreamContent(stream),
                    }
                )
        );
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(100) };
        int errors = 0;
        var cache = new JsonResourceCache(client, _ => { }, _ => { }, downloadError: _ => errors++);
        Assert.Null(
            await cache
                .RefreshAsync<object>("a", PathFor("a"), "https://example.test/a")
                .WaitAsync(TimeSpan.FromSeconds(5))
        );
        Assert.Equal(0, errors);
        Assert.Equal(0, cache.ActiveResourceGateCount);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task ExplicitSerializerSettingsAndConvertersApplyToRemoteAndLocalSnapshots()
    {
        var settings = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        settings.Converters.Add(new JsonStringEnumConverter());
        using var handler = new Handler(
            (_, _) => Task.FromResult(Json("{/*comment*/\"mode\":\"Ready\",}"))
        );
        using var client = new HttpClient(handler);
        var cache = Create(client, new() { SerializerOptions = settings });
        settings.AllowTrailingCommas = false;
        settings.Converters.Clear(); // Changing the original does not change the cache's snapshot.
        var remote = await cache.RefreshAsync<Model>("a", PathFor("a"), "https://example.test/a");
        var local = await cache.LoadLocalAsync<Model>(PathFor("a"));
        Assert.Equal(Mode.Ready, remote!.Mode);
        Assert.Equal(remote.Mode, local!.Mode);
        Assert.Contains("\"mode\":\"Ready\"", await File.ReadAllTextAsync(PathFor("a")));
        var memoized = await cache.LoadAsync<Model>(
            "b",
            PathFor("b"),
            "https://example.test/b",
            null
        );
        Assert.Equal(Mode.Ready, memoized!.Mode);
    }

    public enum Mode
    {
        Ready,
    }

    public sealed class Model
    {
        public Mode Mode { get; set; }
    }

    private Task<Dictionary<string, string>?> Load(JsonResourceCache cache, string key) =>
        cache.LoadAsync<Dictionary<string, string>>(
            key,
            PathFor(key),
            "https://example.test/" + key,
            null
        );

    private static JsonResourceCache Create(HttpClient client, JsonResourceCacheOptions options) =>
        new(client, _ => { }, _ => { }, options: options);

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json) };

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private sealed class Handler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond
    ) : HttpMessageHandler
    {
        internal int Calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken token
        )
        {
            Interlocked.Increment(ref Calls);
            return respond(request, token);
        }
    }

    private sealed class BodyStream(byte[] body, bool stall = false) : Stream
    {
        internal int BytesRead;
        internal bool Disposed;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken token = default
        )
        {
            if (stall)
                await Task.Delay(Timeout.Infinite, token);
            int length = Math.Min(buffer.Length, body.Length - BytesRead);
            body.AsMemory(BytesRead, length).CopyTo(buffer);
            BytesRead += length;
            return length;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
