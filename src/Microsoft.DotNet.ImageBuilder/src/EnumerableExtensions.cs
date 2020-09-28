// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Returns a value indicating whether the two enumerables are equivalent (order does not matter).
        /// </summary>
        public static bool AreEquivalent<T>(this IEnumerable<T> source, IEnumerable<T> items)
        {
            if (source.Count() != items.Count())
            {
                return false;
            }

            IList<T> sourceList = source
                .OrderBy(item => item)
                .ToList();
            IList<T> itemsList = items
                .OrderBy(item => item)
                .ToList();

            for (int i = 0; i < sourceList.Count; i++)
            {
                if (!Equals(sourceList[i], itemsList[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
