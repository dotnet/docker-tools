// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.V2;

public readonly record struct ImageKey(string Repo, string Image)
{
    public override string ToString() => $"{Repo}__{Image}";
}

public readonly record struct PlatformKey(string Repo, string Image, string Platform)
{
    public override string ToString() => $"{Repo}__{Image}__{Platform}";
}
