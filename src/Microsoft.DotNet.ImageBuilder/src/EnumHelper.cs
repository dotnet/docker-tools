// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class EnumHelper
    {
        public static string GetHelpTextOptions<T>(T defaultValue)
            where T : Enum
        {
            string nonDefaultValueOptions = String.Join(", ",
                Enum.GetValues(typeof(T))
                    .Cast<T>()
                    .Where(enumVal => !enumVal.Equals(defaultValue))
                    .Select(enumVal => enumVal.ToString().ToCamelCase()));
            return $"Options: {defaultValue.ToString().ToCamelCase()} (default), {nonDefaultValueOptions}";
        }
    }
}
