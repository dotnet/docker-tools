// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Shouldly;

namespace Microsoft.DotNet.ImageBuilder.Tests;

[TestClass]
public class PathHelperTests
{
    [TestMethod]
    public void SafeCombine_RelativeSegments_CombinesUnderBasePath()
    {
        string basePath = $"{Path.DirectorySeparatorChar}repo";

        string result = PathHelper.SafeCombine(basePath, "eng", "docker-tools");

        result.ShouldBe(Path.Combine(basePath, "eng", "docker-tools"));
    }

    [TestMethod]
    public void SafeCombine_ForwardSlashSegment_ConvertsToPlatformSeparator()
    {
        string basePath = $"{Path.DirectorySeparatorChar}repo";

        string result = PathHelper.SafeCombine(basePath, "templates/variables/docker-images.yml");

        result.ShouldBe(Path.Combine(basePath, "templates", "variables", "docker-images.yml"));
    }

    [TestMethod]
    public void SafeCombine_RootedSegment_Throws()
    {
        string basePath = $"{Path.DirectorySeparatorChar}repo";
        string rootedSegment = $"{Path.DirectorySeparatorChar}etc{Path.DirectorySeparatorChar}passwd";

        ArgumentException exception =
            Should.Throw<ArgumentException>(() => PathHelper.SafeCombine(basePath, rootedSegment));
        exception.Message.ShouldContain("rooted");
    }

    [TestMethod]
    [DataRow("..")]
    [DataRow("../escape")]
    [DataRow("nested/../../escape")]
    public void SafeCombine_TraversalSegment_Throws(string traversalSegment)
    {
        string basePath = $"{Path.DirectorySeparatorChar}repo";

        ArgumentException exception =
            Should.Throw<ArgumentException>(() => PathHelper.SafeCombine(basePath, traversalSegment));
        exception.Message.ShouldContain("traversal");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    public void SafeCombine_NullOrWhitespaceSegment_Throws(string? segment)
    {
        string basePath = $"{Path.DirectorySeparatorChar}repo";

        Should.Throw<ArgumentException>(() => PathHelper.SafeCombine(basePath, segment!));
    }
}
