// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Notary;
using Microsoft.DotNet.ImageBuilder.Models.Oci;

namespace Microsoft.DotNet.ImageBuilder.Commands.Signing;

#nullable enable
[Export(typeof(ICommand))]
public class GenerateSigningPayloadsCommand : Command<GenerateSigningPayloadsOptions, GenerateSigningPayloadsOptionsBuilder>
{
    private readonly ILoggerService _loggerService;
    private readonly IOrasClient _orasClient;

    [ImportingConstructor]
    public GenerateSigningPayloadsCommand(
        ILoggerService loggerService,
        IOrasClient orasClient)
    {
        ArgumentNullException.ThrowIfNull(loggerService, nameof(loggerService));
        _loggerService = loggerService;
        _orasClient = orasClient;
    }

    protected override string Description => "Generate signing payloads";

    public override async Task ExecuteAsync()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Options.ImageInfoPath, nameof(Options.ImageInfoPath));
        ArgumentException.ThrowIfNullOrWhiteSpace(Options.PayloadOutputDirectory, nameof(Options.PayloadOutputDirectory));
        FileHelper.CreateDirectoryIfNotExists(Options.PayloadOutputDirectory);

        _loggerService.WriteHeading("GENERATING SIGNING PAYLOADS");

        _loggerService.WriteSubheading("Reading digests from image info file");
        ImageArtifactDetails imageInfo = ImageInfoHelper.LoadFromFile(Options.ImageInfoPath);
        IReadOnlyList<string> digests = GetAllDigests(imageInfo);

        _loggerService.WriteSubheading("Generating signing payloads using ORAS");
        IReadOnlyList<Payload> payloads = CreatePayloads(digests);

        // It is possible for two of our images, built separately, from different Dockerfiles, to have the same digest.
        // In this situation, they are literally the same image, so one signature payload will suffice.
        payloads = payloads.Distinct().ToList();

        _loggerService.WriteSubheading("Writing payloads to disk");
        IReadOnlyList<string> outputFiles = await WriteAllPayloadsAsync(payloads, Options.PayloadOutputDirectory);

        _loggerService.WriteMessage(
            $"Done! Wrote {outputFiles.Count} signing payloads to {Options.PayloadOutputDirectory}");
    }

    private async Task<IReadOnlyList<string>> WriteAllPayloadsAsync(IEnumerable<Payload> payloads, string outputDirectory)
    {
        var outputs = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            payloads,
            async (payload, _) => 
                {
                    string output = await WritePayloadToDiskAsync(payload, outputDirectory);
                    outputs.Add(output);
                });

        return outputs.ToList();
    }

    private async Task<string> WritePayloadToDiskAsync(Payload payload, string outputDirectory)
    {
        string digestWithoutAlgorithm = payload.TargetArtifact.Digest.Split(':')[1];
        string fileName = $"{digestWithoutAlgorithm}.json";
        string payloadPath = Path.Combine(outputDirectory, fileName);
        await File.WriteAllTextAsync(payloadPath, payload.ToJson());
        _loggerService.WriteMessage($"Wrote signing payload to disk: {payloadPath}");
        return fileName;
    }

    private IReadOnlyList<Payload> CreatePayloads(IReadOnlyList<string> digests)
    {
        var payloads = new ConcurrentBag<Payload>();
        Parallel.ForEach(digests, digest => payloads.Add(CreatePayload(digest)));
        return payloads.ToList();
    }

    private Payload CreatePayload(string digest)
    {
        Descriptor descriptor = _orasClient.GetDescriptor(digest, Options.IsDryRun);
        return new Payload(TargetArtifact: descriptor);
    }

    private static List<string> GetAllDigests(ImageArtifactDetails imageInfo) =>
        imageInfo.Repos.SelectMany(GetAllDigestsForRepo).ToList();

    private static IEnumerable<string> GetAllDigestsForRepo(RepoData repoData) =>
        repoData.Images.SelectMany(GetAllDigestsForImage);

    private static IEnumerable<string> GetAllDigestsForImage(ImageData imageData)
    {
        IEnumerable<string> digests = imageData.Platforms.Select(platform => platform.Digest);

        if (imageData.Manifest is not null)
        {
            digests = [ ..digests, imageData.Manifest.Digest ];
        }

        return digests;
    }
}
