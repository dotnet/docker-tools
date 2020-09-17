// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class StringHelper
    {
        public static string GetUniqueString(string suggestedValue, IEnumerable<string> existingValues, string suffixSeparator)
        {
            int index = 1;
            string modifiedValue = suggestedValue;
            while (existingValues.Contains(modifiedValue))
            {
                modifiedValue = $"{suggestedValue}{suffixSeparator}{index++}";
            }

            return modifiedValue;
        }
    }
}
