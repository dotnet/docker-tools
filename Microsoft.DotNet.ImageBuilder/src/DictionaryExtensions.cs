// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder
{
    internal static class DictionaryExtensions
    {
        public static Dictionary<TKey, TValue> Sort<TKey, TValue>(this IDictionary<TKey, TValue> dict)
            where TKey : IComparable
        {
            Dictionary<TKey, TValue> sorted = new Dictionary<TKey, TValue>();
            foreach (KeyValuePair<TKey, TValue> kvp in dict.OrderBy(kvp => kvp.Key))
            {
                sorted.Add(kvp.Key, kvp.Value);
            }
            return sorted;
        }
    }
}
