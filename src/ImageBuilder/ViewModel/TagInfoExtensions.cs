// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.ViewModel;

internal static class TagExtensions
{
    public static string GetDisplayString(this IEnumerable<TagInfo> tags) =>
        $"[{string.Join(',', tags.Select(t => t.Name))}]";
}
