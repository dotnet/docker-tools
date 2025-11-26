// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Commands.Signing;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ICommand = Microsoft.DotNet.ImageBuilder.Commands.ICommand;

namespace Microsoft.DotNet.ImageBuilder;

public static class ImageBuilder
{
    public static IEnumerable<ICommand> Commands => ServiceProvider.Value.GetServices<ICommand>();

    private static Lazy<IServiceProvider> ServiceProvider { get; } = new(() =>
        {
            var builder = Host.CreateDefaultBuilder();

            builder.ConfigureServices((context, services) =>
            {


                // Services
                services.AddSingleton<IAzdoGitHttpClientFactory, AzdoGitHttpClientFactory>();
                services.AddSingleton<IAzureTokenCredentialProvider, AzureTokenCredentialProvider>();
                services.AddSingleton<IAcrClientFactory, AcrClientFactory>();
                services.AddSingleton<IAcrContentClientFactory, AcrContentClientFactory>();
                services.AddSingleton<ICopyImageServiceFactory, CopyImageServiceFactory>();
                services.AddSingleton<IDateTimeService, DateTimeService>();
                services.AddSingleton<IDockerService, DockerService>();
                services.AddSingleton<IEnvironmentService, EnvironmentService>();
                services.AddSingleton<IGitHubClientFactory, GitHubClientFactory>();
                services.AddSingleton<IGitService, GitService>();
                services.AddSingleton<IHttpClientProvider, HttpClientProvider>();
                services.AddSingleton<IImageCacheService, ImageCacheService>();
                services.AddSingleton<IKustoClient, KustoClientWrapper>();
                services.AddSingleton<ILifecycleMetadataService, LifecycleMetadataService>();
                services.AddSingleton<ILoggerService, LoggerService>();
                services.AddSingleton<IManifestServiceFactory, ManifestServiceFactory>();
                services.AddSingleton<IMarImageIngestionReporter, MarImageIngestionReporter>();
                services.AddSingleton<IMcrStatusClientFactory, McrStatusClientFactory>();
                services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<IOctokitClientFactory, OctokitClientFactory>();
                services.AddSingleton<IOrasClient, OrasClient>();
                services.AddSingleton<IProcessService, ProcessService>();
                services.AddSingleton<IRegistryManifestClientFactory, RegistryManifestClientFactory>();
                services.AddSingleton<IRegistryCredentialsProvider, RegistryCredentialsProvider>();
                services.AddSingleton<IVssConnectionFactory, VssConnectionFactory>();

                // Commands
                services.AddSingleton<ICommand, AnnotateEolDigestsCommand>();
                services.AddSingleton<ICommand, BuildCommand>();
                services.AddSingleton<ICommand, CleanAcrImagesCommand>();
                services.AddSingleton<ICommand, CopyAcrImagesCommand>();
                services.AddSingleton<ICommand, CopyBaseImagesCommand>();
                services.AddSingleton<ICommand, GenerateBuildMatrixCommand>();
                services.AddSingleton<ICommand, GenerateDockerfilesCommand>();
                services.AddSingleton<ICommand, GenerateEolAnnotationDataForAllImagesCommand>();
                services.AddSingleton<ICommand, GenerateEolAnnotationDataForPublishCommand>();
                services.AddSingleton<ICommand, GenerateReadmesCommand>();
                services.AddSingleton<ICommand, GenerateSigningPayloadsCommand>();
                services.AddSingleton<ICommand, GetBaseImageStatusCommand>();
                services.AddSingleton<ICommand, GetStaleImagesCommand>();
                services.AddSingleton<ICommand, IngestKustoImageInfoCommand>();
                services.AddSingleton<ICommand, MergeImageInfoCommand>();
                services.AddSingleton<ICommand, PostPublishNotificationCommand>();
                services.AddSingleton<ICommand, PublishImageInfoCommand>();
                services.AddSingleton<ICommand, PublishManifestCommand>();
                services.AddSingleton<ICommand, PublishMcrDocsCommand>();
                services.AddSingleton<ICommand, PullImagesCommand>();
                services.AddSingleton<ICommand, QueueBuildCommand>();
                services.AddSingleton<ICommand, ShowImageStatsCommand>();
                services.AddSingleton<ICommand, ShowManifestSchemaCommand>();
                services.AddSingleton<ICommand, TrimUnchangedPlatformsCommand>();
                services.AddSingleton<ICommand, WaitForMarAnnotationIngestionCommand>();
                services.AddSingleton<ICommand, WaitForMcrDocIngestionCommand>();
                services.AddSingleton<ICommand, WaitForMcrImageIngestionCommand>();
            });

            var host = builder.Build();
            return host.Services;
        }
    );
}
