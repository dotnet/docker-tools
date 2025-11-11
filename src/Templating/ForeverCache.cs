// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.DockerTools.Templating;

/// <summary>
/// A cache that retains values for the lifetime of the object.
/// </summary>
public sealed class ForeverCache<T>(Func<string, T> valueFactory) : ICache<T>
{
    private readonly Func<string, T> _valueFactory = valueFactory;
    private readonly Dictionary<string, T> _cache = [];

    /// <summary>
    /// Number of times a cached value was returned.
    /// </summary>
    public int Hits { get; private set; } = 0;

    /// <summary>
    /// Number of times a new value was created and added to the cache.
    /// </summary>
    public int Misses { get; private set; } = 0;

    public T GetOrAdd(string key)
    {
        if (!_cache.TryGetValue(key, out T? value))
        {
            value = _valueFactory(key);
            _cache[key] = value;
            Misses += 1;
        }
        else
        {
            Hits += 1;
        }

        return value;
    }
}
