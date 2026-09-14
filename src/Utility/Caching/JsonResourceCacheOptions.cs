#nullable enable
using System.Text.Json;

namespace Utility.Caching;

/// <summary>Resource limits and JSON settings for one cache instance.</summary>
public sealed class JsonResourceCacheOptions
{
    /// <summary>Maximum retained resource keys, including retry cooldowns. Zero disables retention.</summary>
    public int MaximumRetainedResources { get; init; } = 256;

    /// <summary>Maximum downloaded body bytes after HTTP decompression. Null disables the limit.</summary>
    public long? MaximumDownloadBytes { get; init; } = 64L * 1024 * 1024;

    /// <summary>
    /// Settings for all JSON reads and writes. A copy is taken at construction. Null preserves
    /// default local/memoized settings and web defaults for remote RefreshAsync reads.
    /// </summary>
    public JsonSerializerOptions? SerializerOptions { get; init; }
}
