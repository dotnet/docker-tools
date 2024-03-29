// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GiGraph.Dot.Entities.Edges;
using GiGraph.Dot.Entities.Graphs;
using GiGraph.Dot.Entities.Nodes;
using GiGraph.Dot.Extensions;
using GiGraph.Dot.Types.Arrowheads;
using GiGraph.Dot.Types.Layout;
using GiGraph.Dot.Types.Nodes;
using GiGraph.Dot.Types.Styling;
using Microsoft.DotNet.ImageBuilder.Models.Docker;
using Microsoft.DotNet.ImageBuilder.ViewModel;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class GenerateImageGraphCommand : ManifestCommand<GenerateImageGraphOptions, GenerateImageGraphOptionsBuilder>
    {
        private readonly Dictionary<string, Manifest> _imageManifestCache = new Dictionary<string, Manifest>();
        private readonly Dictionary<string, DotNode> _nodeCache = new Dictionary<string, DotNode>();

        public GenerateImageGraphCommand() : base()
        {
        }

        protected override string Description => "Generate a DOT (graph description language) file illustrating the image and layer hierarchy";

        public override Task ExecuteAsync()
        {
            Logger.WriteHeading("GENERATING IMAGE GRAPH");

            DotGraph graph = new DotGraph("ImageGraph", true);
            graph.Layout.Direction = DotLayoutDirection.BottomToTop;
            graph.Font.Color = Color.Black;

            PlatformInfo[] platforms = Manifest.GetFilteredPlatforms().ToArray();
            AddBaseImages(graph, platforms);
            AddModeledImages(graph, platforms);

            graph.SaveToFile(Options.OutputPath);
            Logger.WriteMessage($"Graph saved to `{Options.OutputPath}`");

            if (Options.IsVerbose)
            {
                Logger.WriteMessage("Graph:");
                Logger.WriteMessage(File.ReadAllText(Options.OutputPath));
            }

            return Task.CompletedTask;
        }

        private void AddBaseImages(DotGraph graph, PlatformInfo[] platforms)
        {
            IEnumerable<string> externalBaseImages = platforms
                .Where(platform => platform.FinalStageFromImage is not null && !platform.IsInternalFromImage(platform.FinalStageFromImage))
                .Select(platform => platform.FinalStageFromImage)
                .Cast<string>()
                .Distinct()
                .OrderBy(name => name)
                .ToArray();

            LoadImageManifests(externalBaseImages.Select(image => new[] { image }));

            foreach (string externalBaseImage in externalBaseImages)
            {
                AddImageNode(graph, new[] { externalBaseImage }, Color.Crimson);
            }
        }

        private DotNode AddImageNode(DotGraph graph, IEnumerable<string> tags, Color color, string? fromImage = null)
        {
            string primaryTag = tags.First();
            Manifest manifest = _imageManifestCache[primaryTag];
            long totalSize = manifest.SchemaV2Manifest.Config.Size + manifest.SchemaV2Manifest.Layers.Select(layer => layer.Size).Sum();
            string recordBody = $"{GetImageDisplayName(primaryTag, fromImage != null)}| Size: {ConvertToMB(totalSize):#,#.0}";

            if (fromImage != null)
            {
                Manifest fromManifest = _imageManifestCache[fromImage];
                long ownedSize = manifest.SchemaV2Manifest.Config.Size +
                    manifest.SchemaV2Manifest.Layers.Except(fromManifest.SchemaV2Manifest.Layers).Select(layer => layer.Size).Sum();

                recordBody += $"| Owned: {ConvertToMB(ownedSize):#,#.0} ({ownedSize / Convert.ToDecimal(totalSize) * 100:.0}%)";
            }

            DotNode imageNode = new DotNode(primaryTag);
            imageNode.Shape = DotNodeShape.Record;
            imageNode.Color = color;
            imageNode.Label = $"{{{recordBody}}}";
            imageNode.Style.BorderStyle = DotBorderStyle.Solid;

            graph.Nodes.Add(imageNode);
            foreach (string tag in tags)
            {
                _nodeCache.Add(tag, imageNode);
            }

            return imageNode;
        }

        private void AddModeledImages(DotGraph graph, PlatformInfo[] platforms)
        {
            IEnumerable<IEnumerable<string>> images = platforms.Select(platform => platform.Tags.Select(tag => tag.FullyQualifiedName));
            LoadImageManifests(images);

            foreach (PlatformInfo platform in platforms)
            {
                IEnumerable<string> tags = platform.Tags.Select(tag => tag.FullyQualifiedName);
                DotNode imageNode = AddImageNode(graph, tags, Color.Navy, platform.FinalStageFromImage);

                if (platform.FinalStageFromImage is not null)
                {
                    var myEdge = new DotEdge(imageNode.Id, _nodeCache[platform.FinalStageFromImage].Id);
                    myEdge.Head.Arrowhead = DotArrowheadShape.Normal;
                    myEdge.Tail.Arrowhead = DotArrowheadShape.None;
                    myEdge.Color = Color.Black;
                    myEdge.Style.LineStyle = DotLineStyle.Dashed;
                    graph.Edges.Add(myEdge);
                }
            }
        }

        private decimal ConvertToMB(long bytes) => bytes / 1048576m;

        private string GetImageDisplayName(string tag, bool trimParentRepos)
        {
            string displayName = tag;

            if (Manifest.Registry != null)
            {
                displayName = DockerHelper.TrimRegistry(displayName, Manifest.Registry);
            }

            if (trimParentRepos)
            {
                // Strip off any parent product repos for brevity
                int lastSlash = displayName.LastIndexOf('/');
                if (lastSlash != -1)
                {
                    displayName = displayName.Substring(lastSlash + 1);
                }
            }

            return displayName;
        }

        private void LoadImageManifests(IEnumerable<IEnumerable<string>> images)
        {
            Parallel.ForEach(images, tagList =>
            {
                Manifest manifest = DockerHelper.InspectManifest(tagList.First(), Options.IsDryRun);
                lock (_imageManifestCache)
                {
                    foreach (string tag in tagList)
                    {
                        _imageManifestCache.Add(tag, manifest);
                    }
                }
            });
        }
    }
}
#nullable disable
