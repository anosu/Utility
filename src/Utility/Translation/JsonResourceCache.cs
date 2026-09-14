#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Utility.Translation;

/// <summary>The fallback selected when a translation resource cannot be refreshed.</summary>
public enum CacheFallback
{
    /// <summary>A readable local copy is available.</summary>
    Stale,

    /// <summary>No usable copy is available; requests enter a retry cooldown.</summary>
    Unavailable,
}

/// <summary>
/// A session-scoped JSON cache with per-resource request merging and offline fallback.
/// Paths, protocol hashes and user notifications belong to the caller. The caller owns HttpClient.
/// </summary>
public sealed class JsonResourceCache
{
    private readonly HttpClient _client;
    private readonly Func<string, string> _hash;
    private readonly Action<string> _info;
    private readonly Action<string> _warn;
    private readonly Action<string, CacheFallback> _fallback;
    private readonly TimeSpan _retryDelay;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly ConcurrentDictionary<(string, Type), object> _memory = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _retryAfter = new();
    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    /// <summary>Creates a cache. A new instance starts a new language/CDN session.</summary>
    public JsonResourceCache(
        HttpClient client,
        Func<string, string> hash,
        Action<string> info,
        Action<string> warn,
        Action<string, CacheFallback> fallback,
        TimeSpan? retryDelay = null
    )
    {
        _client = client;
        _hash = hash;
        _info = info;
        _warn = warn;
        _fallback = fallback;
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>Reads a local snapshot without taking the network lock or publishing it in memory.</summary>
    public async Task<T?> LoadLocalAsync<T>(
        string path,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = await ReadLocalAsync(path, cancellationToken).ConfigureAwait(false);
        var data = Deserialize<T>(json, path);
        cancellationToken.ThrowIfCancellationRequested();
        return data;
    }

    /// <summary>
    /// Loads one resource. A matching local hash avoids the network; failed refresh uses readable
    /// stale data. Cancellation is propagated and never reported as a download failure.
    /// </summary>
    public async Task<T?> LoadAsync<T>(
        string key,
        string path,
        string url,
        string? expectedHash,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        var memoryKey = (key, typeof(T));
        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_memory.TryGetValue(memoryKey, out var saved))
                return (T)saved;
            if (_retryAfter.TryGetValue(key, out var retry) && DateTimeOffset.UtcNow < retry)
                return null;
            var localJson = await ReadLocalAsync(path, cancellationToken).ConfigureAwait(false);
            var local = Deserialize<T>(localJson, path);
            if (local != null && expectedHash != null && MatchesHash(localJson, expectedHash))
            {
                _info($"Cache hit: {key}");
                _memory[memoryKey] = local;
                return local;
            }
            var remoteJson = await DownloadAsync(url, cancellationToken).ConfigureAwait(false);
            var remote = Deserialize<T>(remoteJson, url);
            if (remote != null && (expectedHash == null || MatchesHash(remoteJson, expectedHash)))
            {
                await SaveAsync(path, remoteJson!, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                _memory[memoryKey] = remote;
                return remote;
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (local != null)
            {
                _warn($"Using stale cache: {key}");
                _fallback(key, CacheFallback.Stale);
                _memory[memoryKey] = local;
                return local;
            }
            _retryAfter[key] = DateTimeOffset.UtcNow + _retryDelay;
            _fallback(key, CacheFallback.Unavailable);
            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<string?> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            _info($"Fetching translation: {url}");
            using var response = await _client
                .GetAsync(url, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response
                .Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
            when (e is HttpRequestException or OperationCanceledException or IOException)
        {
            _warn($"Translation download failed: {url}: {e.Message}");
            return null;
        }
    }

    private async Task<string?> ReadLocalAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            return await File.ReadAllTextAsync(path, Utf8, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _warn($"Cannot read translation cache: {path}: {e.Message}");
            return null;
        }
    }

    private T? Deserialize<T>(string? json, string source)
        where T : class
    {
        if (json == null)
            return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException e)
        {
            _warn($"Invalid translation JSON: {source}: {e.Message}");
            return null;
        }
    }

    private bool MatchesHash(string? json, string expectedHash)
    {
        try
        {
            return json != null && _hash(json) == expectedHash;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            _warn($"Invalid translation hash input: {e.Message}");
            return false;
        }
    }

    private async Task SaveAsync(string path, string json, CancellationToken cancellationToken)
    {
        string temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(temporary, json, Utf8, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _warn($"Cannot save translation cache: {path}: {e.Message}");
        }
        finally
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _warn($"Cannot remove temporary cache: {temporary}: {e.Message}");
            }
        }
    }
}
