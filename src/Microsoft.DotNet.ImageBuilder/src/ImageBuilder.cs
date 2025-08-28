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
using ICommand = Microsoft.DotNet.ImageBuilder.Commands.ICommand;

namespace Microsoft.DotNet.ImageBuilder;

public static class ImageBuilder
{
    public static IEnumerable<ICommand> Commands => ServiceProvider.Value.GetServices<ICommand>();

    private static Lazy<ServiceProvider> ServiceProvider { get; } = new(() =>
        {
            var builder = new ServiceCollection();

            // Services
            builder.AddSingleton<IAzdoGitHttpClientFactory, AzdoGitHttpClientFactory>();
            builder.AddSingleton<IAzureTokenCredentialProvider, AzureTokenCredentialProvider>();
            builder.AddSingleton<IContainerRegistryClientFactory, ContainerRegistryClientFactory>();
            builder.AddSingleton<IContainerRegistryContentClientFactory, ContainerRegistryContentClientFactory>();
            builder.AddSingleton<ICopyImageService, CopyImageService>();
            builder.AddSingleton<ICopyImageServiceFactory, CopyImageServiceFactory>();
            builder.AddSingleton<IDateTimeService, DateTimeService>();
            builder.AddSingleton<IDockerService, DockerService>();
            builder.AddSingleton<IEnvironmentService, EnvironmentService>();
            builder.AddSingleton<IGitHubClientFactory, GitHubClientFactory>();
            builder.AddSingleton<IGitService, GitService>();
            builder.AddSingleton<IHttpClientProvider, HttpClientProvider>();
            builder.AddSingleton<IImageCacheService, ImageCacheService>();
            builder.AddSingleton<IKustoClient, KustoClientWrapper>();
            builder.AddSingleton<ILifecycleMetadataService, LifecycleMetadataService>();
            builder.AddSingleton<ILoggerService, LoggerService>();
            builder.AddSingleton<IManifestServiceFactory, ManifestServiceFactory>();
            builder.AddSingleton<IMarImageIngestionReporter, MarImageIngestionReporter>();
            builder.AddSingleton<IMcrStatusClientFactory, McrStatusClientFactory>();
            builder.AddSingleton<INotificationService, NotificationService>();
            builder.AddSingleton<IOctokitClientFactory, OctokitClientFactory>();
            builder.AddSingleton<IOrasClient, OrasClient>();
            builder.AddSingleton<IProcessService, ProcessService>();
            builder.AddSingleton<IRegistryContentClientFactory, RegistryContentClientFactory>();
            builder.AddSingleton<IRegistryCredentialsProvider, RegistryCredentialsProvider>();
            builder.AddSingleton<IVssConnectionFactory, VssConnectionFactory>();

            // Commands
            builder.AddSingleton<ICommand, AnnotateEolDigestsCommand>();
            builder.AddSingleton<ICommand, BuildCommand>();
            builder.AddSingleton<ICommand, CleanAcrImagesCommand>();
            builder.AddSingleton<ICommand, CopyAcrImagesCommand>();
            builder.AddSingleton<ICommand, CopyBaseImagesCommand>();
            builder.AddSingleton<ICommand, GenerateBuildMatrixCommand>();
            builder.AddSingleton<ICommand, GenerateDockerfilesCommand>();
            builder.AddSingleton<ICommand, GenerateEolAnnotationDataForAllImagesCommand>();
            builder.AddSingleton<ICommand, GenerateEolAnnotationDataForPublishCommand>();
            builder.AddSingleton<ICommand, GenerateSigningPayloadsCommand>();
            builder.AddSingleton<ICommand, GetBaseImageStatusCommand>();
            builder.AddSingleton<ICommand, GetStaleImagesCommand>();
            builder.AddSingleton<ICommand, IngestKustoImageInfoCommand>();
            builder.AddSingleton<ICommand, MergeImageInfoCommand>();
            builder.AddSingleton<ICommand, PostPublishNotificationCommand>();
            builder.AddSingleton<ICommand, PublishImageInfoCommand>();
            builder.AddSingleton<ICommand, PublishManifestCommand>();
            builder.AddSingleton<ICommand, PublishMcrDocsCommand>();
            builder.AddSingleton<ICommand, PullImagesCommand>();
            builder.AddSingleton<ICommand, QueueBuildCommand>();
            builder.AddSingleton<ICommand, ShowImageStatsCommand>();
            builder.AddSingleton<ICommand, ShowManifestSchemaCommand>();
            builder.AddSingleton<ICommand, TrimUnchangedPlatformsCommand>();
            builder.AddSingleton<ICommand, WaitForMarAnnotationIngestionCommand>();
            builder.AddSingleton<ICommand, WaitForMcrDocIngestionCommand>();
            builder.AddSingleton<ICommand, WaitForMcrImageIngestionCommand>();

            return builder.BuildServiceProvider();
        }
    );
}
