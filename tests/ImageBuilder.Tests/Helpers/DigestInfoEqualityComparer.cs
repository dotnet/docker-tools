// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.DotNet.DockerTools.ImageBuilder;

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Tests.Helpers;

public class DigestInfoEqualityComparer : IEqualityComparer<DigestInfo>
{
    public static DigestInfoEqualityComparer Instance { get; } = new DigestInfoEqualityComparer();

    public bool Equals(DigestInfo x, DigestInfo y) =>
        x.Repo == y.Repo && x.Digest == y.Digest && x.RemainingTags.SequenceEqual(y.RemainingTags);

    public int GetHashCode([DisallowNull] DigestInfo obj) => throw new NotImplementedException();
}
