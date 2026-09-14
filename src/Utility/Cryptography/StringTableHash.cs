#nullable enable
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Utility.Cryptography;

/// <summary>Deterministic MD5 digests of string-table entries separated by NUL bytes.</summary>
public static class StringTableHash
{
    private static readonly IComparer<string> KeyComparer = Comparer<string>.Create(CompareKeys);
    private static readonly byte[] Separator = { 0 };

    /// <summary>Hashes ordered key/value entries as UTF-8 key, NUL, value, NUL. Null values are empty.</summary>
    /// <remarks>The caller defines ordering and nested-path encoding. This is a content fingerprint, not authentication.</remarks>
    public static string ComputeEntries(IEnumerable<(string Key, string? Value)> entries)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        foreach (var (key, value) in entries)
        {
            AppendUtf8(hash, key);
            hash.AppendData(Separator);
            AppendUtf8(hash, value);
            hash.AppendData(Separator);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendUtf8(IncrementalHash hash, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return;
        int byteCount = Encoding.UTF8.GetByteCount(value);
        byte[]? rented = null;
        Span<byte> buffer =
            byteCount <= 512
                ? stackalloc byte[byteCount]
                : (rented = ArrayPool<byte>.Shared.Rent(byteCount));
        try
        {
            int written = Encoding.UTF8.GetBytes(value.AsSpan(), buffer);
            hash.AppendData(buffer[..written]);
        }
        finally
        {
            if (rented != null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>Hashes Unicode-code-point-sorted paths and values separated by NUL bytes.</summary>
    public static string Compute(string json)
    {
        using var document = JsonDocument.Parse(json);
        var content = new StringBuilder();
        Append(document.RootElement, "", content);
        return Convert
            .ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(content.ToString())))
            .ToLowerInvariant();
    }

    private static void Append(JsonElement table, string prefix, StringBuilder content)
    {
        foreach (
            var property in table.EnumerateObject().OrderBy(property => property.Name, KeyComparer)
        )
        {
            string path = prefix + property.Name;
            if (property.Value.ValueKind == JsonValueKind.Object)
                Append(property.Value, path + '\x01', content);
            else
                content.Append(path).Append('\0').Append(property.Value.GetString()).Append('\0');
        }
    }

    private static int CompareKeys(string left, string right)
    {
        var first = left.EnumerateRunes().GetEnumerator();
        var second = right.EnumerateRunes().GetEnumerator();
        while (first.MoveNext())
        {
            if (!second.MoveNext())
                return 1;
            int order = first.Current.Value.CompareTo(second.Current.Value);
            if (order != 0)
                return order;
        }
        return second.MoveNext() ? -1 : 0;
    }
}
