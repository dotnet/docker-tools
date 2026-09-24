// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Oras;
using Shouldly;

namespace Microsoft.DotNet.ImageBuilder.Tests;

[TestClass]
public class OciAnnotationsTests
{
    [TestMethod]
    public void Created_ParsesOrasTimestamp()
    {
        DateTimeOffset created = new DateTimeOffset(2026, 5, 1, 12, 34, 56, TimeSpan.Zero).AddTicks(1234567);

        CreateReferrer(created.UtcDateTime.ToString("o")).Created.ShouldBe(created);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not-a-date")]
    public void Created_InvalidAnnotation_ReturnsNull(string value)
    {
        CreateReferrer(value).Created.ShouldBeNull();
    }

    [TestMethod]
    public void Created_NoAnnotations_ReturnsNull()
    {
        new ReferrerInfo("registry.io/repo@sha256:abc", ArtifactType: null).Created.ShouldBeNull();
    }

    private static ReferrerInfo CreateReferrer(string createdValue) =>
        new("registry.io/repo@sha256:abc", ArtifactType: null)
        {
            Annotations = new Dictionary<string, string> { [OciAnnotations.ImageCreated] = createdValue }
        };
}
