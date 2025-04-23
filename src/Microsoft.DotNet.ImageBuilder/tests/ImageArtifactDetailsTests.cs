// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Models;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public class ImageArtifactDetailsTests(ITestOutputHelper outputHelper)
{
    private readonly ITestOutputHelper _outputHelper = outputHelper;

    [Fact]
    public void CanReadJsonSchemaVersion1()
    {
        ImageArtifactDetails details = ImageArtifactDetails.FromJson(JsonSchemaVersion1);
        Assert.NotNull(details);

        // When reading schema version 1.0, it will be automatically converted
        // to version 2
        Assert.Equal("2.0", details.SchemaVersion);

        Assert.Single(details.Repos);
        RepoData repo = details.Repos.First();

        Assert.Single(repo.Images);
        ImageData image = repo.Images.First();

        Assert.Equal(2, image.Platforms.Count);

        PlatformData platform1 = image.Platforms.First();
        Assert.Equal(2, platform1.Layers.Count);
        Assert.Equal("sha256:platform1layer1sha", platform1.Layers.First().Digest);
        Assert.Equal(0, platform1.Layers.First().Size);
        Assert.Equal("sha256:platform1layer2sha", platform1.Layers.Last().Digest);
        Assert.Equal(0, platform1.Layers.Last().Size);

        PlatformData platform2 = image.Platforms.Last();
        Assert.Equal(2, platform2.Layers.Count());
        Assert.Equal("sha256:platform2layer1sha", platform2.Layers.First().Digest);
        Assert.Equal(0, platform2.Layers.First().Size);
        Assert.Equal("sha256:platform2layer2sha", platform2.Layers.Last().Digest);
        Assert.Equal(0, platform2.Layers.Last().Size);
    }

    [Fact]
    public void CanReadJsonSchemaVersion2()
    {
        ImageArtifactDetails details = ImageArtifactDetails.FromJson(JsonSchemaVersion2);
        Assert.NotNull(details);
        Assert.Equal("2.0", details.SchemaVersion);
        Assert.Single(details.Repos);

        RepoData repo = details.Repos.First();
        Assert.Single(repo.Images);

        ImageData image = repo.Images.First();
        Assert.Equal(2, image.Platforms.Count());

        PlatformData platform1 = image.Platforms.First();
        Assert.Equal(2, platform1.Layers.Count());
        Assert.Equal("sha256:platform1layer1sha", platform1.Layers.First().Digest);
        Assert.Equal(0, platform1.Layers.First().Size);
        Assert.Equal("sha256:platform1layer2sha", platform1.Layers.Last().Digest);
        Assert.Equal(1, platform1.Layers.Last().Size);

        PlatformData platform2 = image.Platforms.Last();
        Assert.Equal(2, platform2.Layers.Count());
        Assert.Equal("sha256:platform2layer1sha", platform2.Layers.First().Digest);
        Assert.Equal(2, platform2.Layers.First().Size);
        Assert.Equal("sha256:platform2layer2sha", platform2.Layers.Last().Digest);
        Assert.Equal(3, platform2.Layers.Last().Size);
    }

    [Fact]
    public void CanWriteJsonSchemaVersion2()
    {
        ImageArtifactDetails imageInfo = new()
        {
            Repos =
            [
                new RepoData()
                {
                    Repo = "testrepo",
                    Images =
                    [
                        new ImageData()
                        {
                            Platforms =
                            [
                                new PlatformData()
                                {
                                    Layers =
                                    [
                                        new Layer(Digest: "sha256:layer1", Size: 100),
                                        new Layer(Digest: "sha256:layer2", Size: 200)
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        string expectedJson =
            """
            {
              "schemaVersion": "2.0",
              "repos": [
                {
                  "repo": "testrepo",
                  "images": [
                    {
                      "platforms": [
                        {
                          "dockerfile": "",
                          "digest": "",
                          "osType": "",
                          "osVersion": "",
                          "architecture": "",
                          "created": "0001-01-01T00:00:00",
                          "commitUrl": "",
                          "layers": [
                            {
                              "digest": "sha256:layer1",
                              "size": 100
                            },
                            {
                              "digest": "sha256:layer2",
                              "size": 200
                            }
                          ]
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        string actualJson = JsonHelper.SerializeObject(imageInfo);

        _outputHelper.WriteLine("Expected JSON:");
        _outputHelper.WriteLine(expectedJson);
        _outputHelper.WriteLine("\nActual JSON:");
        _outputHelper.WriteLine(actualJson);

        // Normalize line endings and compare
        Assert.Equal(expectedJson.Replace("\r\n", "\n"), actualJson.Replace("\r\n", "\n"));
    }

    #region TestData

    private const string JsonSchemaVersion1 =
    """
    {
      "schemaVersion": "1.0",
      "repos": [
        {
          "repo": "repo1",
          "images": [
            {
              "productVersion": "1.0.0",
              "manifest": {
                "digest": "repo1@sha256:manifestdigest",
                "created": "2024-01-01T00:00:00.0000000Z",
                "sharedTags": [
                  "tag1",
                  "tag2",
                  "latest"
                ]
              },
              "platforms": [
                {
                  "dockerfile": "path/to/Dockerfile1",
                  "simpleTags": [
                    "tag1-arch1",
                    "tag2-arch1"
                  ],
                  "digest": "repo1@sha256:platform1digest",
                  "baseImageDigest": "baseimage@sha256:basedigest1",
                  "osType": "OS1",
                  "osVersion": "osversion1",
                  "architecture": "arch1",
                  "created": "2024-01-01T00:00:01.0000000Z",
                  "commitUrl": "https://example.com/commit1",
                  "layers": [
                    "sha256:platform1layer1sha",
                    "sha256:platform1layer2sha"
                  ]
                },
                {
                  "dockerfile": "path/to/Dockerfile2",
                  "simpleTags": [
                    "tag1-arch2",
                    "tag2-arch2"
                  ],
                  "digest": "repo1@sha256:platform2digest",
                  "baseImageDigest": "baseimage@sha256:basedigest2",
                  "osType": "OS2",
                  "osVersion": "osversion2",
                  "architecture": "arch2",
                  "created": "2024-01-01T00:00:02.0000000Z",
                  "commitUrl": "https://example.com/commit2",
                  "layers": [
                    "sha256:platform2layer1sha",
                    "sha256:platform2layer2sha"
                  ]
                }
              ]
            }
          ]
        }
      ]
    }
    """;

    private const string JsonSchemaVersion2 =
    """
    {
      "schemaVersion": "2.0",
      "repos": [
        {
          "repo": "repo1",
          "images": [
            {
              "productVersion": "1.0.0",
              "manifest": {
                "digest": "repo1@sha256:manifestdigest",
                "created": "2024-01-01T00:00:00.0000000Z",
                "sharedTags": [
                  "tag1",
                  "tag2",
                  "latest"
                ]
              },
              "platforms": [
                {
                  "dockerfile": "path/to/Dockerfile1",
                  "simpleTags": [
                    "tag1-arch1",
                    "tag2-arch1"
                  ],
                  "digest": "repo1@sha256:platform1digest",
                  "baseImageDigest": "baseimage@sha256:basedigest1",
                  "osType": "OS1",
                  "osVersion": "osversion1",
                  "architecture": "arch1",
                  "created": "2024-01-01T00:00:01.0000000Z",
                  "commitUrl": "https://example.com/commit1",
                  "layers": [
                    { "digest": "sha256:platform1layer1sha", "size": 0 },
                    { "digest": "sha256:platform1layer2sha", "size": 1 }
                  ]
                },
                {
                  "dockerfile": "path/to/Dockerfile2",
                  "simpleTags": [
                    "tag1-arch2",
                    "tag2-arch2"
                  ],
                  "digest": "repo1@sha256:platform2digest",
                  "baseImageDigest": "baseimage@sha256:basedigest2",
                  "osType": "OS2",
                  "osVersion": "osversion2",
                  "architecture": "arch2",
                  "created": "2024-01-01T00:00:02.0000000Z",
                  "commitUrl": "https://example.com/commit2",
                  "layers": [
                    { "digest": "sha256:platform2layer1sha", "size": 2 },
                    { "digest": "sha256:platform2layer2sha", "size": 3 }
                  ]
                }
              ]
            }
          ]
        }
      ]
    }
    """;

    #endregion
}
