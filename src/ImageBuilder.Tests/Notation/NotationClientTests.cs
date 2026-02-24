// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Notation;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Notation;

public class NotationClientTests
{
    [Fact]
    public void Verify_DryRun_DoesNotThrow()
    {
        var client = new NotationClient();

        var result = client.Verify("registry.io/repo@sha256:abc", isDryRun: true);

        result.ShouldNotBeNull();
    }
}
