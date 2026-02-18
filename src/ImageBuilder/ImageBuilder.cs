// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Commands.Signing;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.Signing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ICommand = Microsoft.DotNet.ImageBuilder.Commands.ICommand;

namespace Microsoft.DotNet.ImageBuilder;

public static class ImageBuilder
{
    public static IEnumerable<ICommand> Commands => ServiceProvider.Value.GetServices<ICommand>();

    private static Lazy<IServiceProvider> ServiceProvider { get; } = new(() =>
        {
            var builder = Host.CreateApplicationBuilder();

            // Configuration
            builder.AddPublishConfiguration();
            builder.AddBuildConfiguration();

            // Services
            builder.Services.AddSingleton<IAzdoGitHttpClientFactory, AzdoGitHttpClientFactory>();
            builder.Services.AddSingleton<IAzureTokenCredentialProvider, AzureTokenCredentialProvider>();
            builder.Services.AddSingleton<IAcrClientFactory, AcrClientFactory>();
            builder.Services.AddSingleton<IAcrContentClientFactory, AcrContentClientFactory>();
            builder.Services.AddSingleton<ICopyImageService, CopyImageService>();
            builder.Services.AddSingleton<IDateTimeService, DateTimeService>();
            builder.Services.AddSingleton<IDockerService, DockerService>();
            builder.Services.AddSingleton<IEnvironmentService, EnvironmentService>();
            builder.Services.AddSingleton<IGitHubClientFactory, GitHubClientFactory>();
            builder.Services.AddSingleton<IGitService, GitService>();
            builder.Services.AddSingleton<IHttpClientProvider, HttpClientProvider>();
            builder.Services.AddSingleton<IImageCacheService, ImageCacheService>();
            builder.Services.AddSingleton<IKustoClient, KustoClientWrapper>();
            builder.Services.AddSingleton<ILifecycleMetadataService, LifecycleMetadataService>();
            builder.Services.AddSingleton<IFileSystem, FileSystem>();
            builder.Services.AddSingleton<ILoggerService, LoggerService>();
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<IManifestServiceFactory, ManifestServiceFactory>();
            builder.Services.AddSingleton<IMarImageIngestionReporter, MarImageIngestionReporter>();
            builder.Services.AddSingleton<IMcrStatusClientFactory, McrStatusClientFactory>();
            builder.Services.AddSingleton<INotificationService, NotificationService>();
            builder.Services.AddSingleton<IOctokitClientFactory, OctokitClientFactory>();
            builder.Services.AddSingleton<IOrasClient, OrasClient>();
            builder.Services.AddSingleton<Oras.OrasDotNetService>();
            builder.Services.AddSingleton<Oras.IOrasDescriptorService>(sp => sp.GetRequiredService<Oras.OrasDotNetService>());
            builder.Services.AddSingleton<Oras.IOrasSignatureService>(sp => sp.GetRequiredService<Oras.OrasDotNetService>());
            builder.Services.AddSingleton<IProcessService, ProcessService>();
            builder.Services.AddSingleton<IRegistryResolver, RegistryResolver>();
            builder.Services.AddSingleton<IRegistryManifestClientFactory, RegistryManifestClientFactory>();
            builder.Services.AddSingleton<IRegistryCredentialsProvider, RegistryCredentialsProvider>();
            builder.Services.AddSingleton<IVssConnectionFactory, VssConnectionFactory>();
            builder.AddSigningServices();

            // Commands
            builder.Services.AddSingleton<ICommand, AnnotateEolDigestsCommand>();
            builder.Services.AddSingleton<ICommand, BuildCommand>();
            builder.Services.AddSingleton<ICommand, CleanAcrImagesCommand>();
            builder.Services.AddSingleton<ICommand, CopyAcrImagesCommand>();
            builder.Services.AddSingleton<ICommand, CopyBaseImagesCommand>();
            builder.Services.AddSingleton<ICommand, GenerateBuildMatrixCommand>();
            builder.Services.AddSingleton<ICommand, GenerateDockerfilesCommand>();
            builder.Services.AddSingleton<ICommand, GenerateEolAnnotationDataForAllImagesCommand>();
            builder.Services.AddSingleton<ICommand, GenerateEolAnnotationDataForPublishCommand>();
            builder.Services.AddSingleton<ICommand, GenerateReadmesCommand>();
            builder.Services.AddSingleton<ICommand, GetBaseImageStatusCommand>();
            builder.Services.AddSingleton<ICommand, GetStaleImagesCommand>();
            builder.Services.AddSingleton<ICommand, IngestKustoImageInfoCommand>();
            builder.Services.AddSingleton<ICommand, MergeImageInfoCommand>();
            builder.Services.AddSingleton<ICommand, PostPublishNotificationCommand>();
            builder.Services.AddSingleton<ICommand, PublishImageInfoCommand>();
            builder.Services.AddSingleton<ICommand, PublishManifestCommand>();
            builder.Services.AddSingleton<ICommand, PublishMcrDocsCommand>();
            builder.Services.AddSingleton<ICommand, PullImagesCommand>();
            builder.Services.AddSingleton<ICommand, QueueBuildCommand>();
            builder.Services.AddSingleton<ICommand, ShowImageStatsCommand>();
            builder.Services.AddSingleton<ICommand, ShowManifestSchemaCommand>();
            builder.Services.AddSingleton<ICommand, SignImagesCommand>();
            builder.Services.AddSingleton<ICommand, TrimUnchangedPlatformsCommand>();
            builder.Services.AddSingleton<ICommand, WaitForMarAnnotationIngestionCommand>();
            builder.Services.AddSingleton<ICommand, WaitForMcrDocIngestionCommand>();
            builder.Services.AddSingleton<ICommand, WaitForMcrImageIngestionCommand>();

            var host = builder.Build();
            return host.Services;
        }
    );
}
