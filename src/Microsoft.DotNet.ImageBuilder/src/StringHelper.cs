using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder;

public static class StringHelper
{
    public static string Format<T>(params IEnumerable<T> value)
    {
        return $"[{string.Join(", ", value)}]";
    }
}
