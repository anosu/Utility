#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Utility.Caching;

/// <summary>Serializes resource access and releases entries when no owner or waiter remains.</summary>
internal sealed class ResourceGates
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    internal int Count
    {
        get
        {
            lock (_sync)
                return _entries.Count;
        }
    }

    internal async Task<IDisposable> EnterAsync(string key, CancellationToken token)
    {
        Entry entry;
        lock (_sync)
        {
            if (!_entries.TryGetValue(key, out entry!))
                _entries.Add(key, entry = new Entry());
            entry.Users++;
        }
        try
        {
            await entry.Gate.WaitAsync(token).ConfigureAwait(false);
        }
        catch
        {
            Release(key, entry, false);
            throw;
        }
        return new Lease(this, key, entry);
    }

    private void Release(string key, Entry entry, bool acquired)
    {
        if (acquired)
            entry.Gate.Release();
        lock (_sync)
        {
            if (--entry.Users != 0)
                return;
            _entries.Remove(key);
            entry.Gate.Dispose();
        }
    }

    private sealed class Entry
    {
        internal readonly SemaphoreSlim Gate = new(1, 1);
        internal int Users;
    }

    private sealed class Lease(ResourceGates owner, string key, Entry entry) : IDisposable
    {
        private ResourceGates? _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release(key, entry, true);
    }
}
