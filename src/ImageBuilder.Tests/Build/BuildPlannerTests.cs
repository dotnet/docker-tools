// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Build;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Moq;
using Newtonsoft.Json;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.Build;

[TestClass]
public class BuildPlannerTests
{
    [TestMethod]
    public async Task ChangedBaseImageExplainsFullDependencyChain()
    {
        using TempFolderContext tempFolder = TestHelper.UseTempFolder();
        ManifestInfo manifest = LoadManifest(
            tempFolder,
            CreateManifest(
                CreateRepo("root", CreateImage(CreatePlatform(
                    CreateDockerfile("root", tempFolder, "base:tag"), ["tag"]))),
                CreateRepo("middle", CreateImage(CreatePlatform(
                    CreateDockerfile("middle", tempFolder, "root:tag"), ["tag"]))),
                CreateRepo("support", CreateImage(CreatePlatform(
                    CreateDockerfile("support", tempFolder, "support-base:tag"), ["tag"]))),
                CreateRepo("leaf", CreateImage(CreatePlatform(
                    CreateDockerfile("leaf", tempFolder, "support:tag", "middle:tag"), ["tag"])))));
        ImageArtifactDetails imageInfo = CreatePublishedImages(
            manifest,
            new Dictionary<string, string?>
            {
                ["root"] = "base@sha256:old",
                ["middle"] = "root@sha256:root",
                ["support"] = "support-base@sha256:support-base",
                ["leaf"] = "middle@sha256:middle",
            });
        BuildGraph graph = BuildGraph.Create(manifest);
        Mock<IManifestService> manifestService = new();
        manifestService
            .Setup(service => service.GetManifestDigestShaAsync(
                It.Is<string>(image => image.Contains("support-base", StringComparison.Ordinal)),
                false))
            .ReturnsAsync("sha256:support-base");
        manifestService
            .Setup(service => service.GetManifestDigestShaAsync(
                It.Is<string>(image => !image.Contains("support-base", StringComparison.Ordinal)),
                false))
            .ReturnsAsync("sha256:new");

        BuildPlanItem[] plan = await CreatePlanner().CreatePlanAsync(
            graph,
            imageInfo,
            new CompositeBuildPolicy(
                new BuildPolicyResult(
                    BuildAction.NoAction,
                    new BuildReason("All checks passed, so no work is required.")),
                Mock.Of<ILogger<CompositeBuildPolicy>>(),
                new MissingPublishedImagePolicy(),
                CreateBaseImageRule(manifest, manifestService.Object),
                new TagSetChangedPolicy()));

        BuildPlanItem root = GetItem(plan, "root");
        BuildPlanItem middle = GetItem(plan, "middle");
        BuildPlanItem support = GetItem(plan, "support");
        BuildPlanItem leaf = GetItem(plan, "leaf");

        root.Decision.Action.ShouldBe(BuildAction.BuildImage);
        middle.Decision.Action.ShouldBe(BuildAction.BuildImage);
        leaf.Decision.Action.ShouldBe(BuildAction.BuildImage);
        support.Decision.Action.ShouldBe(BuildAction.UsePublishedImage);

        BuildReason leafReason = leaf.Decision.Reason;
        leafReason.Message.ShouldStartWith("Dependency");
        BuildReason middleReason = leafReason.CausedBy.ShouldNotBeNull();
        middleReason.Message.ShouldStartWith("Dependency");
        BuildReason rootReason = middleReason.CausedBy.ShouldNotBeNull();
        rootReason.Message.ShouldContain("changed from 'sha256:old' to 'sha256:new'");

        BuildReason supportReason = support.Decision.Reason;
        supportReason.Message.ShouldContain("leaf/Dockerfile");
        supportReason.CausedBy.ShouldBe(leafReason);
    }

    [TestMethod]
    public async Task ChangedTagSetsRequireReuse()
    {
        using TempFolderContext tempFolder = TestHelper.UseTempFolder();
        ManifestInfo manifest = LoadManifest(
            tempFolder,
            CreateManifest(
                CreateRepo("runtime", CreateImage(
                    ["old-shared", "new-shared"],
                    CreatePlatform(
                        CreateDockerfile("runtime", tempFolder, "base:tag"),
                        ["old", "new"])))));
        ImageArtifactDetails imageInfo = CreatePublishedImages(
            manifest,
            new Dictionary<string, string?> { ["runtime"] = "base@sha256:base" });
        ImageData image = imageInfo.Repos.Single().Images.Single();
        image.Platforms.Single().SimpleTags = ["old", "removed"];
        image.Manifest!.SharedTags = ["old-shared"];
        BuildGraph graph = BuildGraph.Create(manifest);
        Mock<IManifestService> manifestService = CreateDigestService("sha256:base");

        BuildPlanItem[] plan = await CreatePlanner().CreatePlanAsync(
            graph,
            imageInfo,
            new CompositeBuildPolicy(
                new BuildPolicyResult(
                    BuildAction.NoAction,
                    new BuildReason("All checks passed, so no work is required.")),
                Mock.Of<ILogger<CompositeBuildPolicy>>(),
                new MissingPublishedImagePolicy(),
                CreateBaseImageRule(manifest, manifestService.Object),
                new TagSetChangedPolicy()));

        BuildPlanItem item = plan.ShouldHaveSingleItem();
        item.Decision.Action.ShouldBe(BuildAction.PublishExistingImage);
        BuildReason reason = item.Decision.Reason;
        reason.Message.ShouldContain("[old, removed]");
        reason.Message.ShouldContain("[old, new]");
        reason.Message.ShouldContain("[old-shared, new-shared]");
    }

    [TestMethod]
    public async Task InvalidatedSharedBuildForcesEveryTargetToBuild()
    {
        using TempFolderContext tempFolder = TestHelper.UseTempFolder();
        string dockerfile = CreateDockerfile("shared", tempFolder, "base:tag");
        ManifestInfo manifest = LoadManifest(
            tempFolder,
            CreateManifest(
                CreateRepo("first", CreateImage(CreatePlatform(dockerfile, ["tag"]))),
                CreateRepo("second", CreateImage(CreatePlatform(dockerfile, ["tag"])))));
        ImageArtifactDetails imageInfo = CreatePublishedImages(
            manifest,
            new Dictionary<string, string?>
            {
                ["first"] = "base@sha256:old",
                ["second"] = "base@sha256:new",
            });
        imageInfo.Repos.Single(repo => repo.Repo == "second")
            .Images.Single().Platforms.Single().IsUnchanged = true;
        BuildGraph graph = BuildGraph.Create(manifest);
        Mock<IManifestService> manifestService = CreateDigestService("sha256:new");

        BuildPlanItem[] plan = await CreatePlanner().CreatePlanAsync(
            graph,
            imageInfo,
            new CompositeBuildPolicy(
                new BuildPolicyResult(
                    BuildAction.NoAction,
                    new BuildReason("All checks passed, so no work is required.")),
                Mock.Of<ILogger<CompositeBuildPolicy>>(),
                new MissingPublishedImagePolicy(),
                CreateBaseImageRule(manifest, manifestService.Object),
                new TagSetChangedPolicy()));

        BuildPlanItem first = GetItem(plan, "first");
        BuildPlanItem second = GetItem(plan, "second");
        first.Decision.Action.ShouldBe(BuildAction.BuildImage);
        second.Decision.Action.ShouldBe(BuildAction.BuildImage);
        BuildReason reason = second.Decision.Reason;
        reason.Message.ShouldContain("first");
        reason.CausedBy.ShouldNotBeNull().Message.ShouldContain("changed from");
    }

    [TestMethod]
    public async Task SharedTagCreatesDependencyEdge()
    {
        using TempFolderContext tempFolder = TestHelper.UseTempFolder();
        ManifestInfo manifest = LoadManifest(
            tempFolder,
            CreateManifest(
                CreateRepo("parent", CreateImage(
                    ["shared"],
                    CreatePlatform(
                        CreateDockerfile("parent", tempFolder, "base:tag"),
                        ["specific"]))),
                CreateRepo("child", CreateImage(CreatePlatform(
                    CreateDockerfile("child", tempFolder, "parent:shared"), ["tag"])))));
        ImageArtifactDetails imageInfo = CreatePublishedImages(
            manifest,
            new Dictionary<string, string?>
            {
                ["parent"] = "base@sha256:old",
                ["child"] = "parent@sha256:parent",
            });
        BuildGraph graph = BuildGraph.Create(manifest);
        Mock<IManifestService> manifestService = CreateDigestService("sha256:new");

        BuildPlanItem[] plan = await CreatePlanner().CreatePlanAsync(
            graph,
            imageInfo,
            new CompositeBuildPolicy(
                new BuildPolicyResult(
                    BuildAction.NoAction,
                    new BuildReason("All checks passed, so no work is required.")),
                Mock.Of<ILogger<CompositeBuildPolicy>>(),
                new MissingPublishedImagePolicy(),
                CreateBaseImageRule(manifest, manifestService.Object),
                new TagSetChangedPolicy()));

        BuildPlanItem child = GetItem(plan, "child");
        child.Decision.Action.ShouldBe(BuildAction.BuildImage);
        child.Decision.Reason.Message.ShouldStartWith("Dependency");
    }

    [TestMethod]
    public void StructurallyDifferentBuildArgumentsDoNotShareImages()
    {
        using TempFolderContext tempFolder = TestHelper.UseTempFolder();
        string dockerfile = CreateDockerfile("shared", tempFolder, "base:tag");
        Platform first = CreatePlatform(dockerfile, ["tag"]);
        first.BuildArgs = new Dictionary<string, string> { ["A"] = "x|B=y" };
        Platform second = CreatePlatform(dockerfile, ["tag"]);
        second.BuildArgs = new Dictionary<string, string> { ["A"] = "x", ["B"] = "y" };
        ManifestInfo manifest = LoadManifest(
            tempFolder,
            CreateManifest(
                CreateRepo("first", CreateImage(first)),
                CreateRepo("second", CreateImage(second))));
        BuildGraph graph = BuildGraph.Create(manifest);

        foreach (BuildTarget target in graph.Targets)
        {
            graph.SharedBuildTargets[target].ShouldHaveSingleItem();
        }
    }

    [TestMethod]
    public void DuplicateFromOverridesProduceOneTargetOverride()
    {
        using TempFolderContext tempFolder = TestHelper.UseTempFolder();
        const string FromImage = "mcr.microsoft.com/app:base";
        string dockerfile = CreateDockerfile("app", tempFolder, FromImage, FromImage);
        Manifest model = CreateManifest(
            CreateRepo("app", CreateImage(CreatePlatform(dockerfile, ["base"]))));
        model.Registry = "mcr.microsoft.com";
        ManifestInfo manifest = LoadManifest(tempFolder, model, repoPrefix: "prefix/");
        BuildGraph graph = BuildGraph.Create(manifest);

        KeyValuePair<string, string> imageOverride = graph.Targets.ShouldHaveSingleItem()
            .FromImageOverrides.ShouldHaveSingleItem();
        imageOverride.Key.ShouldBe(FromImage);
        imageOverride.Value.ShouldBe("mcr.microsoft.com/prefix/app:base");
    }

    [TestMethod]
    public async Task CachedSiblingIsNotIncludedWhenAnotherChildBuilds()
    {
        using TempFolderContext tempFolder = TestHelper.UseTempFolder();
        ManifestInfo manifest = LoadManifest(
            tempFolder,
            CreateManifest(
                CreateRepo("parent", CreateImage(CreatePlatform(
                    CreateDockerfile("parent", tempFolder, "base:tag"), ["tag"]))),
                CreateRepo("first", CreateImage(CreatePlatform(
                    CreateDockerfile("first", tempFolder, "parent:tag"), ["tag"]))),
                CreateRepo("second", CreateImage(CreatePlatform(
                    CreateDockerfile("second", tempFolder, "parent:tag"), ["tag"])))));
        ImageArtifactDetails imageInfo = CreatePublishedImages(
            manifest,
            new Dictionary<string, string?>
            {
                ["parent"] = "base@sha256:base",
                ["first"] = "parent@sha256:parent",
                ["second"] = "parent@sha256:parent",
            });
        imageInfo.Repos.RemoveAll(repo => repo.Repo == "first");
        BuildGraph graph = BuildGraph.Create(manifest);
        Mock<IManifestService> manifestService = CreateDigestService("sha256:base");

        BuildPlanItem[] plan = await CreatePlanner().CreatePlanAsync(
            graph,
            imageInfo,
            new CompositeBuildPolicy(
                new BuildPolicyResult(
                    BuildAction.NoAction,
                    new BuildReason("All checks passed, so no work is required.")),
                Mock.Of<ILogger<CompositeBuildPolicy>>(),
                new MissingPublishedImagePolicy(),
                CreateBaseImageRule(manifest, manifestService.Object),
                new TagSetChangedPolicy()));

        GetItem(plan, "parent").Decision.Action.ShouldBe(BuildAction.UsePublishedImage);
        GetItem(plan, "first").Decision.Action.ShouldBe(BuildAction.BuildImage);
        GetItem(plan, "second").Decision.Action.ShouldBe(BuildAction.NoAction);
    }

    [TestMethod]
    public async Task CustomRuleMethodCanAddAPlanningDecision()
    {
        using TempFolderContext tempFolder = TestHelper.UseTempFolder();
        ManifestInfo manifest = LoadManifest(
            tempFolder,
            CreateManifest(
                CreateRepo("runtime", CreateImage(CreatePlatform(
                    CreateDockerfile("runtime", tempFolder, "base:tag"), ["tag"])))));
        BuildGraph graph = BuildGraph.Create(manifest);
        BuildPlanItem[] plan = await CreatePlanner().CreatePlanAsync(
            graph,
            imageInfo: null,
            new CompositeBuildPolicy(
                new BuildPolicyResult(
                    BuildAction.NoAction,
                    new BuildReason("No package changed.")),
                Mock.Of<ILogger<CompositeBuildPolicy>>(),
                new PackageVersionChangedPolicy()));

        BuildPlanItem item = plan.ShouldHaveSingleItem();
        item.Decision.Action.ShouldBe(BuildAction.BuildImage);
        item.Decision.Reason.Message.ShouldContain("openssl");
    }

    [TestMethod]
    public async Task CompositePolicyAppliesEveryChildAndChoosesStrongestAction()
    {
        using TempFolderContext tempFolder = TestHelper.UseTempFolder();
        ManifestInfo manifest = LoadManifest(
            tempFolder,
            CreateManifest(
                CreateRepo("runtime", CreateImage(CreatePlatform(
                    CreateDockerfile("runtime", tempFolder, "base:tag"), ["tag"])))));
        BuildGraph graph = BuildGraph.Create(manifest);
        List<string> appliedPolicies = [];
        IBuildPolicy policy = new CompositeBuildPolicy(
            new BuildPolicyResult(
                BuildAction.NoAction,
                new BuildReason("No checks selected work.")),
            Mock.Of<ILogger<CompositeBuildPolicy>>(),
            new TestPolicy(
                appliedPolicies,
                "use",
                new BuildPolicyResult(
                    BuildAction.UsePublishedImage,
                    new BuildReason("Use the published image."))),
            new TestPolicy(
                appliedPolicies,
                "build",
                new BuildPolicyResult(
                    BuildAction.BuildImage,
                    new BuildReason("Build the image."))));

        BuildPlanItem[] plan = await CreatePlanner().CreatePlanAsync(
            graph,
            CreatePublishedImages(
                manifest,
                new Dictionary<string, string?> { ["runtime"] = "base@sha256:base" }),
            policy);

        appliedPolicies.ShouldBe(["use", "build"]);
        BuildPlanItem item = plan.ShouldHaveSingleItem();
        item.Decision.Action.ShouldBe(BuildAction.BuildImage);
        item.Decision.Reason.Message.ShouldBe("Build the image.");
    }

    private static BuildPlanner CreatePlanner() =>
        new(Mock.Of<ILogger<BuildPlanner>>());

    private static IBuildPolicy CreateBaseImageRule(
        ManifestInfo manifest,
        IManifestService manifestService) =>
        BaseImageChangedPolicy.FromRegistry(
            new ImageDigestCache(new Lazy<IManifestService>(() => manifestService)),
            new ImageNameResolverForMatrix(new(), manifest, null, null),
            isDryRun: false);

    private sealed class PackageVersionChangedPolicy : IBuildPolicy
    {
        public Task<BuildPolicyResult> EvaluateAsync(
            BuildPolicyContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                new BuildPolicyResult(
                    BuildAction.BuildImage,
                    new BuildReason("Package 'openssl' changed from '1.0' to '1.1'.")));
    }

    private sealed class TestPolicy(
        ICollection<string> appliedPolicies,
        string name,
        BuildPolicyResult result) : IBuildPolicy
    {
        public Task<BuildPolicyResult> EvaluateAsync(
            BuildPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            appliedPolicies.Add(name);
            return Task.FromResult(result);
        }
    }

    private static Mock<IManifestService> CreateDigestService(string digest)
    {
        Mock<IManifestService> manifestService = new();
        manifestService
            .Setup(service => service.GetManifestDigestShaAsync(It.IsAny<string>(), false))
            .ReturnsAsync(digest);
        return manifestService;
    }

    private static BuildPlanItem GetItem(
        IEnumerable<BuildPlanItem> plan,
        string repoName) =>
        plan.Single(item => item.Target.Repo.Name == repoName);

    private static ManifestInfo LoadManifest(
        TempFolderContext tempFolder,
        Manifest manifest,
        string? repoPrefix = null)
    {
        string manifestPath = Path.Combine(tempFolder.Path, "manifest.json");
        File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));
        IManifestOptionsInfo options = GetManifestOptions(manifestPath);
        Mock.Get(options)
            .SetupGet(manifestOptions => manifestOptions.RepoPrefix)
            .Returns(repoPrefix);
        return TestHelper.CreateManifestJsonService().Load(options);
    }

    private static ImageArtifactDetails CreatePublishedImages(
        ManifestInfo manifest,
        IReadOnlyDictionary<string, string?> baseImageDigests)
    {
        ImageArtifactDetails details = new();

        foreach (RepoInfo repo in manifest.AllRepos)
        {
            RepoData repoData = new() { Repo = repo.Name };
            details.Repos.Add(repoData);

            foreach (ImageInfo image in repo.AllImages)
            {
                ImageData imageData = new()
                {
                    Manifest = image.SharedTags.Any()
                        ? new ManifestData
                        {
                            SharedTags = image.SharedTags.Select(tag => tag.Name).ToList()
                        }
                        : null
                };
                repoData.Images.Add(imageData);

                foreach (PlatformInfo platform in image.AllPlatforms)
                {
                    imageData.Platforms.Add(new PlatformData(image, platform)
                    {
                        Dockerfile = platform.DockerfilePathRelativeToManifest,
                        Digest = $"{repo.Name}@sha256:{repo.Name}",
                        BaseImageDigest = baseImageDigests[repo.Name],
                        CommitUrl = $"https://example.test/{platform.DockerfilePathRelativeToManifest}",
                        SimpleTags = platform.Tags.Select(tag => tag.Name).ToList(),
                    });
                }
            }
        }

        return details;
    }
}
