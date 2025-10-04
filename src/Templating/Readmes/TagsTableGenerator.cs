// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.DotNet.DockerTools.Templating.Shared;
using Microsoft.DotNet.ImageBuilder.ReadModel;

namespace Microsoft.DotNet.DockerTools.Templating.Readmes;

public static class TagsTableGenerator
{
    public static string GenerateTagsTables(RepoInfo repo)
    {
        var output = new StringBuilder();

        var documentedPlatforms = repo.Images
            .SelectMany(image => image.Platforms)
            .Where(platform => platform.Tags
                .Any(tag => tag.IsDocumented));

        var platformsByOsArch = documentedPlatforms
            .GroupBy(platform => (platform.Model.OS, platform.Model.Architecture));

        foreach (var archGroup in platformsByOsArch)
        {
            var os = archGroup.Key.OS.ToString();
            var arch = archGroup.Key.Architecture.GetDisplayName();

            output.AppendLine($"""

                ### {os} {arch} Tags

                """);

            output.AppendLine(GeneratePlatformsTable(archGroup));
        }

        return output.ToString();
    }

    private static string GeneratePlatformsTable(IEnumerable<PlatformInfo> platforms)
    {
        var table = new MarkdownTableBuilder()
            .WithColumnHeadings("Tags", "Dockerfile", "OS Version");

        foreach (var platform in platforms)
        {
            var tags = platform.Tags
                .Where(tag => tag.IsDocumented)
                .Select(tag => tag.Tag);

            table.AddRow(
                string.Join(", ", tags),
                platform.RelativeDockerfilePath,
                platform.OSDisplayName);
        }

        return table.ToString();
    }
}
