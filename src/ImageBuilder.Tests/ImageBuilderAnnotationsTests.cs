// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Oras;
using Shouldly;

namespace Microsoft.DotNet.ImageBuilder.Tests;

[TestClass]
public class ImageBuilderAnnotationsTests
{
    [TestMethod]
    [DataRow("true", true)]
    [DataRow("True", true)]
    [DataRow("TRUE", true)]
    [DataRow("false", false)]
    [DataRow("", false)]
    [DataRow("yes", false)]
    public void IsInternal_ParsesAnnotationValue(string value, bool expected)
    {
        ReferrerInfo referrer = new("registry.io/repo@sha256:abc", OciArtifactType.Lifecycle)
        {
            Annotations = new Dictionary<string, string> { [ImageBuilderAnnotations.Internal] = value }
        };

        referrer.IsInternal.ShouldBe(expected);
    }

    [TestMethod]
    public void IsInternal_NoAnnotations_ReturnsFalse()
    {
        ReferrerInfo referrer = new("registry.io/repo@sha256:abc", OciArtifactType.Lifecycle);

        referrer.IsInternal.ShouldBeFalse();
    }

    [TestMethod]
    public void IsInternal_AnnotationMissing_ReturnsFalse()
    {
        ReferrerInfo referrer = new("registry.io/repo@sha256:abc", OciArtifactType.Lifecycle)
        {
            Annotations = new Dictionary<string, string>
            {
                [LifecycleAnnotations.EndOfLife] = "2026-01-01T00:00:00Z"
            }
        };

        referrer.IsInternal.ShouldBeFalse();
    }
}
