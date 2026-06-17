// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder;

internal static class ConcurrentDictionaryExtensions
{
    public static bool TryRemove<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        TKey key,
        TValue value)
        where TKey : notnull =>
        dictionary.TryRemove(new KeyValuePair<TKey, TValue>(key, value));
}
