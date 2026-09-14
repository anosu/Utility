#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Utility.Caching;

/// <summary>The outcome when a JSON resource cannot be refreshed.</summary>
public enum CacheFallback
{
    /// <summary>A readable local copy is available.</summary>
    Stale,

    /// <summary>No usable copy is available.</summary>
    Unavailable,
}

/// <summary>Controls how a disk refresh selects a resource.</summary>
public enum JsonCachePolicy
{
    /// <summary>Use a verified local copy, otherwise download with local fallback.</summary>
    Refresh,

    /// <summary>Use any readable local copy before attempting a download.</summary>
    PreferLocal,

    /// <summary>Read the local copy without making a network request.</summary>
    LocalOnly,
}

/// <summary>
/// Caches JSON resources without owning their URLs, schemas or HTTP client. LoadAsync memoizes
/// within this instance; RefreshAsync rereads disk on every call. Use a new instance for a new
/// resource session. A key must consistently identify one path, URL and validation contract.
/// </summary>
public sealed class JsonResourceCache
{
    private readonly HttpClient _client;
    private readonly Func<string, string>? _hash;
    private readonly Action<string> _info;
    private readonly Action<string> _warn;
    private readonly Action<string, CacheFallback>? _fallback;
    private readonly Action<Exception>? _downloadError;
    private readonly TimeSpan _retryDelay;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly ConcurrentDictionary<(string, Type), object> _memory = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _retryAfter = new();
    private static readonly Encoding Utf8 = new UTF8Encoding(false);
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    /// <summary>Creates a memoizing cache with a caller-supplied hash of the original JSON.</summary>
    public JsonResourceCache(
        HttpClient client,
        Func<string, string> hash,
        Action<string> info,
        Action<string> warn,
        Action<string, CacheFallback> fallback,
        TimeSpan? retryDelay = null
    )
        : this(client, info, warn, fallback)
    {
        _hash = hash;
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>Creates a cache for disk refreshes with per-resource typed validation.</summary>
    /// <remarks>Download errors exclude caller cancellation and HTTP timeouts.</remarks>
    public JsonResourceCache(
        HttpClient client,
        Action<string> info,
        Action<string> warn,
        Action<string, CacheFallback>? fallback = null,
        Action<Exception>? downloadError = null
    )
    {
        _client = client;
        _info = info;
        _warn = warn;
        _fallback = fallback;
        _downloadError = downloadError;
        _retryDelay = TimeSpan.FromSeconds(30);
    }

    /// <summary>Reads a local snapshot without waiting for a download or memoizing it.</summary>
    public async Task<T?> LoadLocalAsync<T>(
        string path,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = await ReadLocalAsync(path, cancellationToken).ConfigureAwait(false);
        var value = Deserialize<T>(json, path, false);
        cancellationToken.ThrowIfCancellationRequested();
        return value;
    }

    /// <summary>
    /// Loads and memoizes a resource, merging simultaneous readers. A matching local hash avoids
    /// downloading; failures use stale data or enter a retry cooldown. Cancellation propagates.
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
        if (expectedHash != null && _hash == null)
            throw new InvalidOperationException("A JSON hash function is required for validation.");
        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_memory.TryGetValue((key, typeof(T)), out var saved))
                return (T)saved;
            if (_retryAfter.TryGetValue(key, out var retry) && DateTimeOffset.UtcNow < retry)
                return null;
            var data = await LoadCoreAsync<T>(
                    key,
                    path,
                    url,
                    JsonCachePolicy.Refresh,
                    expectedHash == null ? null : (json, _) => _hash!(json) == expectedHash,
                    false,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (data != null)
                _memory[(key, typeof(T))] = data;
            else
                _retryAfter[key] = DateTimeOffset.UtcNow + _retryDelay;
            return data;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Rereads disk without memoization or retry cooldown. Requests for the same key are serialized.
    /// A validator accepts both local and downloaded values; null means refresh without validation.
    /// Remote values use System.Text.Json web defaults and are serialized with default options for
    /// disk storage. LocalOnly never downloads. Invalid remote data never replaces the local file.
    /// </summary>
    public async Task<T?> RefreshAsync<T>(
        string key,
        string path,
        string url,
        JsonCachePolicy policy = JsonCachePolicy.Refresh,
        Func<T, bool>? validate = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadCoreAsync<T>(
                    key,
                    path,
                    url,
                    policy,
                    validate == null ? null : (_, value) => validate(value),
                    true,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<T?> LoadCoreAsync<T>(
        string key,
        string path,
        string url,
        JsonCachePolicy policy,
        Func<string, T, bool>? validate,
        bool webDefaults,
        CancellationToken cancellationToken
    )
        where T : class
    {
        var localJson = await ReadLocalAsync(path, cancellationToken).ConfigureAwait(false);
        var local = Deserialize<T>(localJson, path, false);
        cancellationToken.ThrowIfCancellationRequested();
        if (policy == JsonCachePolicy.LocalOnly)
            return local;
        if (
            local != null
            && (
                policy == JsonCachePolicy.PreferLocal
                || (validate != null && IsValid(localJson!, local, validate))
            )
        )
        {
            _info($"Cache hit: {key}");
            return local;
        }
        var remoteJson = await DownloadAsync(url, cancellationToken).ConfigureAwait(false);
        var remote = Deserialize<T>(remoteJson, url, webDefaults);
        cancellationToken.ThrowIfCancellationRequested();
        if (remote != null && (validate == null || IsValid(remoteJson!, remote, validate)))
        {
            await SaveAsync(
                    path,
                    webDefaults ? JsonSerializer.Serialize(remote) : remoteJson!,
                    cancellationToken
                )
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return remote;
        }
        if (local != null)
        {
            _warn($"Using stale cache: {key}");
            _fallback?.Invoke(key, CacheFallback.Stale);
            return local;
        }
        _fallback?.Invoke(key, CacheFallback.Unavailable);
        return null;
    }

    private async Task<string?> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            _info($"Fetching JSON: {url}");
            using var response = await _client
                .GetAsync(url, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _warn($"GET {url} returned {(int)response.StatusCode} {response.StatusCode}");
                return null;
            }
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
            _warn($"JSON download failed: {url}: {e.Message}");
            if (e is not OperationCanceledException)
                _downloadError?.Invoke(e);
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
            _warn($"Cannot read JSON cache: {path}: {e.Message}");
            return null;
        }
    }

    private T? Deserialize<T>(string? json, string source, bool webDefaults)
        where T : class
    {
        if (json == null)
            return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json, webDefaults ? WebJson : null);
        }
        catch (JsonException e)
        {
            _warn($"Invalid JSON: {source}: {e.Message}");
            if (webDefaults)
                _downloadError?.Invoke(e);
            return null;
        }
    }

    private bool IsValid<T>(string json, T value, Func<string, T, bool> validate)
    {
        try
        {
            if (validate(json, value))
                return true;
            _warn("JSON resource validation failed.");
        }
        catch (Exception e)
        {
            _warn($"Cannot validate JSON resource: {e.Message}");
        }
        return false;
    }

    private async Task SaveAsync(string path, string json, CancellationToken cancellationToken)
    {
        string temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(temporary, json, Utf8, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _warn($"Cannot save JSON cache: {path}: {e.Message}");
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
