// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Oras;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.LifecycleArtifactHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests;

[TestClass]
public class LifecycleAnnotationsTests
{
    private const string Reference = "registry.io/repo@sha256:lifecycle";

    [TestMethod]
    [DataRow("2026-05-22", 2026, 5, 22)]
    [DataRow("2026-05-22T00:00:00Z", 2026, 5, 22)]
    [DataRow("2026-05-22T23:00:00-05:00", 2026, 5, 23)]
    public void EndOfLifeDate_ParsesAnnotation(string value, int year, int month, int day)
    {
        CreateArtifact(value).EndOfLifeDate.ShouldBe(new DateOnly(year, month, day));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not-a-date")]
    public void EndOfLifeDate_InvalidAnnotation_ReturnsNull(string value)
    {
        CreateArtifact(value).EndOfLifeDate.ShouldBeNull();
    }

    [TestMethod]
    public void EndOfLifeDate_NoAnnotations_ReturnsNull()
    {
        LifecycleArtifact artifact = new(new ReferrerInfo(Reference, OciArtifactType.Lifecycle));

        artifact.EndOfLifeDate.ShouldBeNull();
    }

    [TestMethod]
    [DataRow(0, 0, true)]
    [DataRow(-1, 0, false)]
    [DataRow(0, 1, false)]
    [DataRow(2, 1, true)]
    public void IsEndOfLife_AppliesGracePeriod(int daysPastEndOfLife, int gracePeriodDays, bool expected)
    {
        DateOnly endOfLifeDate = new(2026, 5, 22);
        LifecycleArtifact artifact = CreateLifecycleArtifact(Reference, endOfLifeDate);

        // Use noon so that "0 days past end-of-life" is strictly after the end-of-life date.
        DateTimeOffset now = new DateTimeOffset(endOfLifeDate.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero)
            .AddDays(daysPastEndOfLife);

        artifact.IsEndOfLife(now, TimeSpan.FromDays(gracePeriodDays)).ShouldBe(expected);
    }

    [TestMethod]
    public void IsEndOfLife_NoEndOfLifeDate_ReturnsFalse()
    {
        LifecycleArtifact artifact = CreateLifecycleArtifact(Reference);

        artifact.IsEndOfLife(DateTimeOffset.MaxValue, TimeSpan.Zero).ShouldBeFalse();
    }

    private static LifecycleArtifact CreateArtifact(string endOfLifeValue) =>
        new(new ReferrerInfo(Reference, OciArtifactType.Lifecycle)
        {
            Annotations = new Dictionary<string, string> { [LifecycleAnnotations.EndOfLife] = endOfLifeValue }
        });
}
