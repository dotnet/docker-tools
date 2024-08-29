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
    private readonly IRegistryCredentialsProvider _registryCredentialsProvider;

    [ImportingConstructor]
    public GenerateSigningPayloadsCommand(
        ILoggerService loggerService,
        IOrasClient orasClient,
        IRegistryCredentialsProvider registryCredentialsProvider)
    {
        ArgumentNullException.ThrowIfNull(loggerService, nameof(loggerService));
        ArgumentNullException.ThrowIfNull(orasClient, nameof(orasClient));
        ArgumentNullException.ThrowIfNull(registryCredentialsProvider, nameof(registryCredentialsProvider));

        _loggerService = loggerService;
        _orasClient = orasClient;
        _registryCredentialsProvider = registryCredentialsProvider;
    }

    protected override string Description => "Generate signing payloads";

    public override async Task ExecuteAsync()
    {
        // We want to accurately simulate creating the signing payloads, so this command should never be a dry-run.
        await _registryCredentialsProvider.ExecuteWithCredentialsAsync(
            isDryRun: false,
            action: ExecuteAsyncInternal,
            credentialsOptions: Options.RegistryCredentialsOptions,
            registryName: Options.RegistryOptions.Registry,
            ownedAcr: Options.RegistryOptions.Registry);
    }

    private async Task ExecuteAsyncInternal()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Options.ImageInfoPath, nameof(Options.ImageInfoPath));
        ArgumentException.ThrowIfNullOrWhiteSpace(Options.PayloadOutputDirectory, nameof(Options.PayloadOutputDirectory));
        FileHelper.CreateDirectoryIfNotExists(Options.PayloadOutputDirectory, throwIfNotEmpty: true);

        _loggerService.WriteHeading("GENERATING SIGNING PAYLOADS");

        _loggerService.WriteSubheading("Reading digests from image info file");
        ImageArtifactDetails imageInfo = ImageInfoHelper
            .DeserializeImageArtifactDetails(Options.ImageInfoPath)
            .ApplyRegistryOverride(Options.RegistryOptions);
        IReadOnlyList<string> digests = ImageInfoHelper.GetAllDigests(imageInfo);

        _loggerService.WriteSubheading("Generating signing payloads");
        IReadOnlyList<Payload> payloads = CreatePayloads(digests);

        // It is possible for two of our images, built separately, from different Dockerfiles, to have the same digest.
        // In this situation, they are literally the same image, so one signature payload will suffice.
        payloads = payloads.Distinct().ToList();

        _loggerService.WriteSubheading("Writing payloads to disk");
        await WritePayloadsToDiskAsync(payloads, Options.PayloadOutputDirectory);
    }

    private async Task WritePayloadsToDiskAsync(
        IEnumerable<Payload> payloads,
        string outputDirectory)
    {
        var outputFiles = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            payloads,
            async (payload, _) => 
                {
                    string output = await WritePayloadToDiskAsync(payload, outputDirectory);
                    outputFiles.Add(output);
                });

        string taskCompletedMessage = Options.IsDryRun
            ? $"Dry run: Done! Would have written {outputFiles.Count} signing payloads to {Options.PayloadOutputDirectory}"
            : $"Done! Wrote {outputFiles.Count} signing payloads to {Options.PayloadOutputDirectory}";
        _loggerService.WriteMessage(taskCompletedMessage);
    }

    private async Task<string> WritePayloadToDiskAsync(Payload payload, string outputDirectory)
    {
        string digestWithoutAlgorithm = payload.TargetArtifact.Digest.Split(':')[1];
        string fileName = $"{digestWithoutAlgorithm}.json";
        string payloadPath = Path.Combine(outputDirectory, fileName);

        if (!Options.IsDryRun)
        {
            await File.WriteAllTextAsync(payloadPath, payload.ToJson());
            _loggerService.WriteMessage($"Wrote signing payload to disk: {payloadPath}");
        }
        else
        {
            _loggerService.WriteMessage($"Dry run: Would have written signing payload to disk: {payloadPath}");
        }

        return fileName;
    }

    private List<Payload> CreatePayloads(IReadOnlyList<string> digests)
    {
        var payloads = new ConcurrentBag<Payload>();
        Parallel.ForEach(digests, digest => payloads.Add(CreatePayload(digest)));
        return payloads.ToList();
    }

    private Payload CreatePayload(string fullyQualifiedDigest)
    {
        // Always reach out to the registry even if the command is a dry-run. This is needed to properly simulate
        // generating signing payloads. Getting the descriptor does not have side effects.
        Descriptor descriptor = _orasClient.GetDescriptor(fullyQualifiedDigest, isDryRun: false);
        return new Payload(TargetArtifact: descriptor);
    }
}
