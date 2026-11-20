// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.V2;

public readonly record struct ImageReference(string? Registry, string Repository, string Tag)
{
    public override string ToString() =>
        Registry is null ? $"{Repository}:{Tag}" : $"{Registry}/{Repository}:{Tag}";
}
