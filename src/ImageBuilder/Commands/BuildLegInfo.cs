#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class BuildLegInfo
{
    public string Name { get; set; }
    public List<(string Name, string Value)> Variables { get; } = [];
}
