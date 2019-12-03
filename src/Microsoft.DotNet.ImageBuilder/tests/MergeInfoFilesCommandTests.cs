// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class MergeImageInfoFilesCommandTests
    {
        [Fact]
        public async Task MergeImageInfoFilesCommand_HappyPath()
        {
            using (TempFolderContext context = TestHelper.UseTempFolder())
            {
                List<RepoData[]> repoDataSets = new List<RepoData[]>
                {
                    new RepoData[]
                    {
                        new RepoData
                        {
                            Repo = "repo1"
                        },
                        new RepoData
                        {
                            Repo = "repo2",
                            Images = new SortedDictionary<string, ImageData>
                            {
                                {
                                    "image1",
                                    new ImageData()
                                }
                            }
                        },
                        new RepoData
                        {
                            Repo = "repo4",
                            Images = new SortedDictionary<string, ImageData>
                            {
                                {
                                    "image2",
                                    new ImageData
                                    {
                                        SimpleTags =
                                        {
                                            "tag1"
                                        }
                                    }
                                }
                            }
                        },
                    },
                    new RepoData[]
                    {
                        new RepoData
                        {
                            Repo = "repo2",
                            Images = new SortedDictionary<string, ImageData>
                            {
                                {
                                    "image1",
                                    new ImageData
                                    {
                                        BaseImages = new SortedDictionary<string, string>
                                        {
                                            { "base1", "base1hash" }
                                        },
                                        SimpleTags =
                                        {
                                            "tag1"
                                        }
                                    }
                                }
                            }
                        },
                        new RepoData
                        {
                            Repo = "repo3",
                        },
                        new RepoData
                        {
                            Repo = "repo4",
                            Images = new SortedDictionary<string, ImageData>
                            {
                                {
                                    "image2",
                                    new ImageData
                                    {
                                        SimpleTags =
                                        {
                                            "tag2"
                                        }
                                    }
                                }
                            }
                        },
                    }
                };

                for (int i = 0; i < repoDataSets.Count; i++)
                {
                    string file = Path.Combine(context.Path, $"{i}.json");
                    File.WriteAllText(file, JsonHelper.SerializeObject(repoDataSets[i]));
                }

                MergeImageInfoCommand command = new MergeImageInfoCommand();
                command.Options.SourceImageInfoFolderPath = context.Path;
                command.Options.DestinationImageInfoPath = Path.Combine(context.Path, "output.json");
                await command.ExecuteAsync();

                string resultsContent = File.ReadAllText(command.Options.DestinationImageInfoPath);
                RepoData[] actual = JsonConvert.DeserializeObject<RepoData[]>(resultsContent);

                RepoData[] expected = new RepoData[]
                {
                    new RepoData
                    {
                        Repo = "repo1"
                    },
                    new RepoData
                    {
                        Repo = "repo2",
                        Images = new SortedDictionary<string, ImageData>
                        {
                            {
                                "image1",
                                new ImageData {
                                    BaseImages = new SortedDictionary<string, string>
                                    {
                                        { "base1", "base1hash" }
                                    },
                                    SimpleTags =
                                    {
                                        "tag1"
                                    }
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "repo3",
                    },
                    new RepoData
                    {
                        Repo = "repo4",
                        Images = new SortedDictionary<string, ImageData>
                        {
                            {
                                "image2",
                                new ImageData {
                                    SimpleTags =
                                    {
                                        "tag1",
                                        "tag2"
                                    }
                                }
                            }
                        }
                    },
                };

                ImageInfoHelperTests.CompareRepos(expected, actual);
            }
        }

        [Fact]
        public async Task MergeImageInfoFilesCommand_SourceFolderPathNotFound()
        {
            MergeImageInfoCommand command = new MergeImageInfoCommand();
            command.Options.SourceImageInfoFolderPath = "foo";
            command.Options.DestinationImageInfoPath = "output.json";

            await Assert.ThrowsAsync<DirectoryNotFoundException>(() => command.ExecuteAsync());
        }

        [Fact]
        public async Task MergeImageInfoFilesCommand_SourceFolderEmpty()
        {
            using (TempFolderContext context = TestHelper.UseTempFolder())
            {
                // Store the content in a .txt file which the command should NOT be looking for.
                File.WriteAllText("image-info.txt",
                    JsonHelper.SerializeObject(new RepoData[] { new RepoData { Repo = "repo" } }));

                MergeImageInfoCommand command = new MergeImageInfoCommand();
                command.Options.SourceImageInfoFolderPath = context.Path;
                command.Options.DestinationImageInfoPath = "output.json";

                await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync());
            }
        }
    }
}
