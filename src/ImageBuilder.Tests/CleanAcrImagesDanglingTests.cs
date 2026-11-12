// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Azure;
using Azure.Containers.ContainerRegistry;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ContainerRegistryHelper;
using Options = Microsoft.Extensions.Options.Options;

namespace Microsoft.DotNet.ImageBuilder.Tests;

[TestClass]
public class CleanAcrImagesDanglingTests
{
    private const string RegistryName = "myacr.azurecr.io";
    private const string RepositoryName = "mirror/ubuntu";
    private const string ImageMediaType = "application/vnd.oci.image.manifest.v1+json";
    private const string IndexMediaType = "application/vnd.oci.image.index.v1+json";
    private const string ListMediaType = "application/vnd.docker.distribution.manifest.list.v2+json";

    public TestContext? TestContext { get; set; }

    private CancellationToken CancellationToken => TestContext?.CancellationToken ?? default;

    [TestMethod]
    [DataRow(IndexMediaType, false, 30)]
    [DataRow(IndexMediaType, true, 30)]
    [DataRow(ListMediaType, false, 30)]
    [DataRow(ListMediaType, true, 30)]
    [DataRow(IndexMediaType, true, 0)]
    [DataRow(ListMediaType, true, 0)]
    public async Task KeepsManifestListChildren(string mediaType, bool childrenFirst, int age)
    {
        ArtifactManifestProperties[] properties =
        [
            Properties("sha256:index", tags: ["22.04"]),
            Properties("sha256:amd64"),
            Properties("sha256:arm64"),
            Properties("sha256:orphan")
        ];
        Scenario scenario = CreateScenario(
            childrenFirst ? properties.Reverse().ToArray() : properties,
            Index("sha256:index", ["sha256:amd64", "sha256:arm64"], mediaType),
            Image("sha256:amd64"), Image("sha256:arm64"), Image("sha256:orphan"));
        scenario.Command.Options.Age = age;

        await scenario.Command.ExecuteAsync(CancellationToken);

        VerifyDeleted(scenario, "sha256:orphan");
    }

    [TestMethod]
    public async Task InspectsAllPagesBeforePruningChildren()
    {
        string[] children = Enumerable.Range(0, 260).Select(i => $"sha256:child{i}").ToArray();
        ArtifactManifestProperties[] properties =
            [.. children.Select(digest => Properties(digest)), Properties("sha256:index", tags: ["latest"])];
        Scenario scenario = CreateScenario(
            properties, [.. children.Select(digest => Image(digest)), Index("sha256:index", children)]);
        scenario.Repository
            .Setup(repo => repo.GetAllManifestPropertiesAsync(It.IsAny<ArtifactManifestOrder>(), It.IsAny<CancellationToken>()))
            .Returns(() => AsyncPageable<ArtifactManifestProperties>.FromPages(
            [
                Page<ArtifactManifestProperties>.FromValues(properties[..250], "next", Mock.Of<Response>()),
                Page<ArtifactManifestProperties>.FromValues(properties[250..], null, Mock.Of<Response>())
            ]));

        await scenario.Command.ExecuteAsync(CancellationToken);

        VerifyDeleted(scenario);
    }

    [TestMethod]
    public async Task CollectsOldIndexBeforeItsUnsharedChildren()
    {
        ArtifactManifestProperties[] properties =
        [
            Properties("sha256:current", tags: ["22.04"]),
            Properties("sha256:old"),
            Properties("sha256:shared"),
            Properties("sha256:obsolete")
        ];
        ManifestQueryResult[] manifests =
        [
            Index("sha256:current", ["sha256:shared"]),
            Index("sha256:old", ["sha256:shared", "sha256:obsolete"]),
            Image("sha256:shared"),
            Image("sha256:obsolete")
        ];
        Scenario firstRun = CreateScenario(properties, manifests);

        await firstRun.Command.ExecuteAsync(CancellationToken);

        VerifyDeleted(firstRun, "sha256:old");

        Scenario secondRun = CreateScenario(
            properties.Where(manifest => manifest.Digest != "sha256:old").ToArray(),
            manifests.Where(manifest => manifest.ContentDigest != "sha256:old").ToArray());

        await secondRun.Command.ExecuteAsync(CancellationToken);

        VerifyDeleted(secondRun, "sha256:obsolete");
    }

    [TestMethod]
    [DataRow("tagged")]
    [DataRow("young")]
    [DataRow("excluded")]
    [DataRow("locked")]
    public async Task PreservesRetainedIndexesAndTheirChildren(string retentionReason)
    {
        Scenario scenario = CreateScenario(
        [
            Properties("sha256:parent",
                tags: retentionReason == "tagged" ? ["latest"] : [],
                ageInDays: retentionReason == "young" ? 1 : 60,
                canDelete: retentionReason == "locked" ? false : null),
            Properties("sha256:child"),
            Properties("sha256:orphan")
        ],
        Index("sha256:parent", ["sha256:child"]), Image("sha256:child"), Image("sha256:orphan"));
        if (retentionReason == "excluded")
        {
            scenario.Command.Options.ImagesToExclude = [$"{RepositoryName}@sha256:parent"];
        }

        await scenario.Command.ExecuteAsync(CancellationToken);

        VerifyDeleted(scenario, "sha256:orphan");
    }

    [TestMethod]
    public async Task PreservesNestedIndexesAndCycles()
    {
        Scenario scenario = CreateScenario(
        [
            Properties("sha256:root", tags: ["latest"]),
            Properties("sha256:nested"),
            Properties("sha256:image"),
            Properties("sha256:cycle1"),
            Properties("sha256:cycle2"),
            Properties("sha256:orphan")
        ],
        Index("sha256:root", ["sha256:nested"]),
        Index("sha256:nested", ["sha256:image"]),
        Image("sha256:image"),
        Index("sha256:cycle1", ["sha256:cycle2"]),
        Index("sha256:cycle2", ["sha256:cycle1"]),
        Image("sha256:orphan"));

        await scenario.Command.ExecuteAsync(CancellationToken);

        VerifyDeleted(scenario, "sha256:orphan");
    }

    [TestMethod]
    public async Task HonorsLeafExclusionsAndAge()
    {
        Scenario scenario = CreateScenario(
        [
            Properties("sha256:excluded"),
            Properties("sha256:tagged", tags: ["keep"]),
            Properties("sha256:young", ageInDays: 1),
            Properties("sha256:locked", canDelete: false),
            Properties("sha256:orphan")
        ],
        Image("sha256:excluded"), Image("sha256:tagged"), Image("sha256:young"),
        Image("sha256:locked"), Image("sha256:orphan"));
        scenario.Command.Options.ImagesToExclude = [$"{RepositoryName}@sha256:excluded", $"{RepositoryName}:keep"];

        await scenario.Command.ExecuteAsync(CancellationToken);

        VerifyDeleted(scenario, "sha256:orphan");
    }

    [TestMethod]
    [DataRow("sha256:image")]
    [DataRow("sha256:missing")]
    public async Task DoesNotIndependentlyPruneReferrers(string subject)
    {
        ManifestQueryResult referrerIndex = Index("sha256:referrer-index", ["sha256:artifact"]);
        referrerIndex.Manifest["subject"] = new JsonObject { ["digest"] = subject };
        Scenario scenario = CreateScenario(
        [
            Properties("sha256:image", tags: ["latest"]),
            Properties("sha256:signature"),
            Properties("sha256:referrer-index"),
            Properties("sha256:artifact"),
            Properties("sha256:orphan")
        ],
        Image("sha256:image"), Image("sha256:signature", subject), referrerIndex,
        Image("sha256:artifact"), Image("sha256:orphan"));

        await scenario.Command.ExecuteAsync(CancellationToken);

        VerifyDeleted(scenario, "sha256:orphan");
    }

    [TestMethod]
    [DataRow("tagged")]
    [DataRow("young")]
    [DataRow("excluded")]
    [DataRow("locked")]
    [DataRow("referenced")]
    public async Task RetainedReferrersProtectTheirSubjectChain(string retentionReason)
    {
        Scenario scenario = CreateScenario(
        [
            Properties("sha256:index", tags: ["latest"]),
            Properties("sha256:signature",
                tags: retentionReason == "tagged" ? ["signature"] : [],
                ageInDays: retentionReason == "young" ? 1 : 60,
                canDelete: retentionReason == "locked" ? false : null),
            Properties("sha256:attestation"),
            Properties("sha256:image"),
            Properties("sha256:orphan")
        ],
        Index("sha256:index", retentionReason == "referenced" ? ["sha256:signature"] : []),
        Image("sha256:signature", "sha256:attestation"),
        Image("sha256:attestation", "sha256:image"),
        Image("sha256:image"), Image("sha256:orphan"));
        if (retentionReason == "excluded")
        {
            scenario.Command.Options.ImagesToExclude = [$"{RepositoryName}@sha256:signature"];
        }

        await scenario.Command.ExecuteAsync(CancellationToken);

        VerifyDeleted(scenario, "sha256:orphan");
    }

    [TestMethod]
    public async Task OldUntaggedReferrerDoesNotPermanentlyPinItsSubject()
    {
        Scenario scenario = CreateScenario(
            [Properties("sha256:image"), Properties("sha256:signature")],
            Image("sha256:image"), Image("sha256:signature", "sha256:image"));

        await scenario.Command.ExecuteAsync(CancellationToken);

        VerifyDeleted(scenario, "sha256:image");
    }

    [TestMethod]
    public async Task SubjectCyclesAreRetainedWithoutLooping()
    {
        Scenario scenario = CreateScenario(
        [
            Properties("sha256:signature1", tags: ["keep"]),
            Properties("sha256:signature2"),
            Properties("sha256:orphan")
        ],
        Image("sha256:signature1", "sha256:signature2"),
        Image("sha256:signature2", "sha256:signature1"),
        Image("sha256:orphan"));

        await scenario.Command.ExecuteAsync(CancellationToken);

        VerifyDeleted(scenario, "sha256:orphan");
    }

    [TestMethod]
    public async Task DoesNotTreatLayerOrConfigBlobsAsManifestReferences()
    {
        ManifestQueryResult image = Image("sha256:tagged");
        image.Manifest["config"] = new JsonObject { ["digest"] = "sha256:config" };
        image.Manifest["layers"] = new JsonArray(new JsonObject { ["digest"] = "sha256:layer" });
        Scenario scenario = CreateScenario(
        [
            Properties("sha256:tagged", tags: ["latest"]),
            Properties("sha256:config"),
            Properties("sha256:layer")
        ],
        image, Image("sha256:config"), Image("sha256:layer"));

        await scenario.Command.ExecuteAsync(CancellationToken);

        VerifyDeleted(scenario, "sha256:config", "sha256:layer");
    }

    [TestMethod]
    [DataRow("application/vnd.docker.distribution.manifest.v2+json", "layers")]
    [DataRow("application/vnd.docker.distribution.manifest.v1+json", "fsLayers")]
    [DataRow("application/vnd.docker.distribution.manifest.v1+prettyjws", "fsLayers")]
    [DataRow("application/vnd.oci.artifact.manifest.v1+json", "blobs")]
    [DataRow(null, "layers")]
    [DataRow(null, "fsLayers")]
    public async Task SupportsLeafManifestFormats(string? mediaType, string blobProperty)
    {
        ManifestQueryResult manifest = new("sha256:orphan", new JsonObject
        {
            ["mediaType"] = mediaType,
            [blobProperty] = new JsonArray()
        });
        Scenario scenario = CreateScenario([Properties("sha256:orphan")], manifest);

        await scenario.Command.ExecuteAsync(CancellationToken);

        VerifyDeleted(scenario, "sha256:orphan");
    }

    [TestMethod]
    public async Task DryRunInspectsDependenciesWithoutDeleting()
    {
        Scenario scenario = CreateScenario(
            [Properties("sha256:index"), Properties("sha256:child"), Properties("sha256:orphan")],
            Index("sha256:index", ["sha256:child"]), Image("sha256:child"), Image("sha256:orphan"));
        scenario.Command.Options.IsDryRun = true;

        await scenario.Command.ExecuteAsync(CancellationToken);

        scenario.ContentClient.Verify(
            client => client.GetManifestAsync("sha256:index", It.IsAny<CancellationToken>()), Times.Once);
        VerifyDeleted(scenario);
        scenario.AcrClient.Verify(client => client.DeleteRepositoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    [DataRow(404)]
    [DataRow(403)]
    [DataRow(429)]
    [DataRow(500)]
    public async Task ReadFailureAbortsBeforeAnyDeletion(int status)
    {
        Scenario scenario = CreateScenario(
            [Properties("sha256:orphan"), Properties("sha256:index", tags: ["latest"])],
            Image("sha256:orphan"));
        scenario.ContentClient.Setup(client => client.GetManifestAsync("sha256:index", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(status, "Unable to read index"));

        await Should.ThrowAsync<RequestFailedException>(() => scenario.Command.ExecuteAsync(CancellationToken));

        VerifyDeleted(scenario);
    }

    [TestMethod]
    [DataRow("""{"mediaType":"application/vnd.oci.image.index.v1+json"}""")]
    [DataRow("""{"mediaType":"application/vnd.oci.image.index.v1+json","manifests":null}""")]
    [DataRow("""{"manifests":{}}""")]
    [DataRow("""{"manifests":[null]}""")]
    [DataRow("""{"manifests":["sha256:image"]}""")]
    [DataRow("""{"manifests":[{}]}""")]
    [DataRow("""{"manifests":[{"digest":42}]}""")]
    [DataRow("""{"manifests":[{"digest":" "}]}""")]
    [DataRow("""{"mediaType":42,"layers":[]}""")]
    [DataRow("""{"mediaType":"application/vnd.oci.image.manifest.v1+json","layers":{} }""")]
    [DataRow("""{"mediaType":"application/vnd.oci.image.manifest.v1+json","layers":[],"manifests":[]}""")]
    [DataRow("""{"mediaType":"application/vnd.oci.image.manifest.v1+json","layers":[],"subject":{}}""")]
    [DataRow("""{"mediaType":"application/vnd.example.unknown","layers":[]}""")]
    [DataRow("""{}""")]
    public async Task MalformedOrUnknownManifestAbortsBeforeAnyDeletion(string json)
    {
        Scenario scenario = CreateScenario(
            [Properties("sha256:orphan"), Properties("sha256:index", tags: ["latest"])],
            Image("sha256:orphan"), new ManifestQueryResult("sha256:index", JsonNode.Parse(json)!.AsObject()));

        await Should.ThrowAsync<JsonException>(() => scenario.Command.ExecuteAsync(CancellationToken));

        VerifyDeleted(scenario);
    }

    [TestMethod]
    public async Task MissingReferencedManifestAbortsBeforeAnyDeletion()
    {
        Scenario scenario = CreateScenario(
            [Properties("sha256:index", tags: ["latest"]), Properties("sha256:orphan")],
            Index("sha256:index", ["sha256:missing"]), Image("sha256:orphan"));

        await Should.ThrowAsync<InvalidOperationException>(() => scenario.Command.ExecuteAsync(CancellationToken));

        VerifyDeleted(scenario);
    }

    [TestMethod]
    public async Task MismatchedContentDigestAbortsBeforeAnyDeletion()
    {
        Scenario scenario = CreateScenario([Properties("sha256:orphan")], Image("sha256:orphan"));
        scenario.ContentClient.Setup(client => client.GetManifestAsync("sha256:orphan", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Image("sha256:different"));

        await Should.ThrowAsync<InvalidOperationException>(() => scenario.Command.ExecuteAsync(CancellationToken));

        VerifyDeleted(scenario);
    }

    [TestMethod]
    public async Task IncompleteListingDoesNotDeleteEarlierPages()
    {
        ArtifactManifestProperties[] properties = [Properties("sha256:orphan")];
        Scenario scenario = CreateScenario(properties, Image("sha256:orphan"));
        scenario.Repository
            .Setup(repo => repo.GetAllManifestPropertiesAsync(It.IsAny<ArtifactManifestOrder>(), It.IsAny<CancellationToken>()))
            .Returns(() => AsyncPageable<ArtifactManifestProperties>.FromPages(FailingPages(properties)));

        await Should.ThrowAsync<RequestFailedException>(() => scenario.Command.ExecuteAsync(CancellationToken));

        VerifyDeleted(scenario);
    }

    [TestMethod]
    [DataRow("added")]
    [DataRow("removed")]
    [DataRow("tagged")]
    [DataRow("updated")]
    [DataRow("locked")]
    public async Task InventoryChangeAbortsBeforeAnyDeletion(string change)
    {
        ArtifactManifestProperties original = Properties("sha256:orphan");
        Scenario scenario = CreateScenario([original], Image("sha256:orphan"));
        ArtifactManifestProperties[] changed = change switch
        {
            "added" => [original, Properties("sha256:new-index", tags: ["latest"])],
            "removed" => [],
            "tagged" => [Properties(original.Digest, tags: ["latest"], lastUpdatedOn: original.LastUpdatedOn)],
            "updated" => [Properties(original.Digest, ageInDays: 1)],
            "locked" => [Properties(original.Digest, canDelete: false, lastUpdatedOn: original.LastUpdatedOn)],
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        scenario.Repository
            .SetupSequence(repo => repo.GetAllManifestPropertiesAsync(It.IsAny<ArtifactManifestOrder>(), It.IsAny<CancellationToken>()))
            .Returns(new AsyncPageableMock<ArtifactManifestProperties>([original]))
            .Returns(new AsyncPageableMock<ArtifactManifestProperties>(changed));

        await Should.ThrowAsync<InvalidOperationException>(() => scenario.Command.ExecuteAsync(CancellationToken));

        VerifyDeleted(scenario);
    }

    [TestMethod]
    public async Task InventoryAndTagOrderingDoNotAffectThePlan()
    {
        ArtifactManifestProperties tagged = Properties("sha256:tagged", tags: ["22.04", "jammy"]);
        ArtifactManifestProperties orphan = Properties("sha256:orphan");
        Scenario scenario = CreateScenario([tagged, orphan], Image(tagged.Digest), Image(orphan.Digest));
        scenario.Repository
            .SetupSequence(repo => repo.GetAllManifestPropertiesAsync(It.IsAny<ArtifactManifestOrder>(), It.IsAny<CancellationToken>()))
            .Returns(new AsyncPageableMock<ArtifactManifestProperties>([tagged, orphan]))
            .Returns(new AsyncPageableMock<ArtifactManifestProperties>(
                [orphan, Properties(tagged.Digest, tags: ["jammy", "22.04"], lastUpdatedOn: tagged.LastUpdatedOn)]));

        await scenario.Command.ExecuteAsync(CancellationToken);

        VerifyDeleted(scenario, orphan.Digest);
    }

    [TestMethod]
    public async Task RevalidationReadFailureDoesNotDelete()
    {
        ArtifactManifestProperties[] properties = [Properties("sha256:orphan")];
        Scenario scenario = CreateScenario(properties, Image("sha256:orphan"));
        scenario.Repository
            .SetupSequence(repo => repo.GetAllManifestPropertiesAsync(It.IsAny<ArtifactManifestOrder>(), It.IsAny<CancellationToken>()))
            .Returns(new AsyncPageableMock<ArtifactManifestProperties>(properties))
            .Throws(new RequestFailedException(429, "Unable to revalidate inventory"));

        await Should.ThrowAsync<RequestFailedException>(() => scenario.Command.ExecuteAsync(CancellationToken));

        VerifyDeleted(scenario);
    }

    [TestMethod]
    public async Task DoesNotDeleteAnEmptyRepositoryThatGainsAnImage()
    {
        Scenario scenario = CreateScenario([]);
        scenario.Repository
            .SetupSequence(repo => repo.GetAllManifestPropertiesAsync(It.IsAny<ArtifactManifestOrder>(), It.IsAny<CancellationToken>()))
            .Returns(new AsyncPageableMock<ArtifactManifestProperties>([]))
            .Returns(new AsyncPageableMock<ArtifactManifestProperties>([Properties("sha256:new")]));

        await Should.ThrowAsync<InvalidOperationException>(() => scenario.Command.ExecuteAsync(CancellationToken));

        VerifyDeleted(scenario);
        scenario.AcrClient.Verify(
            client => client.DeleteRepositoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task CancellationDuringInspectionDoesNotDelete()
    {
        using CancellationTokenSource cancellation = new();
        Scenario scenario = CreateScenario([Properties("sha256:orphan")], Image("sha256:orphan"));
        scenario.ContentClient.Setup(client => client.GetManifestAsync("sha256:orphan", It.IsAny<CancellationToken>()))
            .Callback(cancellation.Cancel)
            .ReturnsAsync(Image("sha256:orphan"));

        await Should.ThrowAsync<OperationCanceledException>(() => scenario.Command.ExecuteAsync(cancellation.Token));

        VerifyDeleted(scenario);
    }

    [TestMethod]
    public async Task FailedIndexDeletionNeverDeletesItsChildren()
    {
        Scenario scenario = CreateScenario(
            [Properties("sha256:index"), Properties("sha256:child")],
            Index("sha256:index", ["sha256:child"]), Image("sha256:child"));
        scenario.ContentClient.Setup(client => client.DeleteManifestAsync("sha256:index", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(500, "Unable to delete index"));

        await Should.ThrowAsync<RequestFailedException>(() => scenario.Command.ExecuteAsync(CancellationToken));

        VerifyDeleted(scenario, "sha256:index");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PreservesEmptyRepositoryCleanupAndDryRun(bool dryRun)
    {
        Scenario scenario = CreateScenario([]);
        scenario.Command.Options.IsDryRun = dryRun;

        await scenario.Command.ExecuteAsync(CancellationToken);

        VerifyDeleted(scenario);
        scenario.AcrClient.Verify(
            client => client.DeleteRepositoryAsync(RepositoryName, It.IsAny<CancellationToken>()),
            Times.Exactly(dryRun ? 0 : 1));
    }

    [TestMethod]
    public async Task GeneratedGraphsNeverDeleteReferencedTaggedOrLockedManifests()
    {
        Random random = new(797);
        for (int iteration = 0; iteration < 50; iteration++)
        {
            const int manifestCount = 40;
            string[] digests = Enumerable.Range(0, manifestCount).Select(i => $"sha256:manifest{i}").ToArray();
            List<ArtifactManifestProperties> properties = [];
            List<ManifestQueryResult> manifests = [];
            HashSet<string> referenced = [];
            HashSet<string> retained = [];
            foreach (string digest in digests)
            {
                bool tagged = random.Next(4) == 0;
                bool locked = random.Next(8) == 0;
                bool young = random.Next(8) == 0;
                properties.Add(Properties(digest, tags: tagged ? ["tag"] : [], ageInDays: young ? 1 : 60, canDelete: !locked));
                if (tagged || locked || young)
                {
                    retained.Add(digest);
                }

                if (random.Next(3) == 0)
                {
                    string[] children = Enumerable.Range(0, random.Next(5))
                        .Select(_ => digests[random.Next(manifestCount)]).ToArray();
                    referenced.UnionWith(children);
                    manifests.Add(Index(digest, children, random.Next(2) == 0 ? IndexMediaType : ListMediaType));
                }
                else
                {
                    manifests.Add(Image(digest));
                }
            }

            Scenario scenario = CreateScenario(properties.ToArray(), manifests.ToArray());

            await scenario.Command.ExecuteAsync(CancellationToken);

            VerifyDeleted(scenario, digests.Where(digest => !referenced.Contains(digest) && !retained.Contains(digest)).ToArray());
        }
    }

    private static ArtifactManifestProperties Properties(
        string digest, string[]? tags = null, int ageInDays = 60, bool? canDelete = null,
        DateTimeOffset? lastUpdatedOn = null) =>
        CreateArtifactManifestProperties(
            registryLoginServer: RegistryName, repositoryName: RepositoryName, digest: digest,
            lastUpdatedOn: lastUpdatedOn ?? DateTimeOffset.UtcNow.AddDays(-ageInDays),
            tags: tags, canDelete: canDelete);

    private static ManifestQueryResult Image(string digest, string? subject = null)
    {
        JsonObject manifest = new()
        {
            ["schemaVersion"] = 2,
            ["mediaType"] = ImageMediaType,
            ["config"] = new JsonObject { ["digest"] = "sha256:config" },
            ["layers"] = new JsonArray()
        };
        if (subject is not null)
        {
            manifest["subject"] = new JsonObject { ["digest"] = subject };
        }

        return new ManifestQueryResult(digest, manifest);
    }

    private static ManifestQueryResult Index(string digest, string[] children, string mediaType = IndexMediaType)
    {
        JsonArray manifests = [];
        foreach (string child in children)
        {
            manifests.Add(new JsonObject { ["mediaType"] = ImageMediaType, ["digest"] = child, ["size"] = 1 });
        }

        return new ManifestQueryResult(digest, new JsonObject
        {
            ["schemaVersion"] = 2,
            ["mediaType"] = mediaType,
            ["manifests"] = manifests
        });
    }

    private static Scenario CreateScenario(ArtifactManifestProperties[] properties, params ManifestQueryResult[] manifests)
    {
        ContainerRepository repository = CreateContainerRepository(
            RepositoryName, CreateContainerRepositoryProperties(), properties);
        Mock<IAcrClient> acrClient = CreateAcrClientMock([repository]);
        Mock<IAcrContentClient> contentClient = CreateAcrContentClientMock(
            RepositoryName, manifests.ToDictionary(manifest => manifest.ContentDigest));
        CleanAcrImagesCommand command = new(
            CreateAcrClientFactory(RegistryName, acrClient.Object),
            CreateAcrContentClientFactory(RegistryName, [contentClient]),
            Mock.Of<ILogger<CleanAcrImagesCommand>>(),
            Mock.Of<ILifecycleMetadataService>(),
            Options.Create(new PublishConfiguration()));
        command.Options.RegistryName = RegistryName;
        command.Options.RepoName = "mirror/*";
        command.Options.Action = CleanAcrImagesAction.PruneDangling;
        command.Options.Age = 30;
        return new Scenario(command, contentClient, Mock.Get(repository), acrClient);
    }

    private static void VerifyDeleted(Scenario scenario, params string[] digests)
    {
        scenario.ContentClient.Verify(
            client => client.DeleteManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(digests.Length));
        foreach (string digest in digests)
        {
            scenario.ContentClient.Verify(
                client => client.DeleteManifestAsync(digest, It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    private static IEnumerable<Page<ArtifactManifestProperties>> FailingPages(ArtifactManifestProperties[] properties)
    {
        yield return Page<ArtifactManifestProperties>.FromValues(properties, "next", Mock.Of<Response>());
        throw new RequestFailedException(500, "Unable to read the next page");
    }

    private sealed record Scenario(
        CleanAcrImagesCommand Command,
        Mock<IAcrContentClient> ContentClient,
        Mock<ContainerRepository> Repository,
        Mock<IAcrClient> AcrClient);
}
