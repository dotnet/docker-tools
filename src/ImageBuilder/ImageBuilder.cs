// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Commands.Signing;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.RateLimiting;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.Signing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using ICommand = Microsoft.DotNet.ImageBuilder.Commands.ICommand;

namespace Microsoft.DotNet.ImageBuilder;

public static class ImageBuilder
{
    public static IHost CreateAppHost()
    {
        var builder = Host.CreateApplicationBuilder();

        // Configuration
        builder.AddPublishConfiguration();
        builder.AddBuildConfiguration();

        // Logging
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        builder.Logging.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });

        // Register abstractions
        builder.Services.AddSingleton<IFileSystem, FileSystem>();

        // Register services
        builder.Services.AddSingleton<IAzdoGitHttpClientFactory, AzdoGitHttpClientFactory>();
        builder.Services.AddSingleton<IAzureTokenCredentialProvider, AzureTokenCredentialProvider>();
        builder.Services.AddSingleton<IAcrClientFactory, AcrClientFactory>();
        builder.Services.AddSingleton<IAcrContentClientFactory, AcrContentClientFactory>();
        builder.Services.AddSingleton<IAcrImageImporter, AcrImageImporter>();
        builder.Services.AddSingleton<ICopyImageService, CopyImageService>();
        builder.Services.AddSingleton<IDateTimeService, DateTimeService>();
        builder.Services.AddSingleton<IDockerService, DockerService>();
        builder.Services.AddSingleton<IEnvironmentService, EnvironmentService>();
        builder.Services.AddSingleton<IGitHubClientFactory, GitHubClientFactory>();
        builder.Services.AddSingleton<IGitService, GitService>();

        // Add ACR rate limiting:
        // Singleton limiter holds the rate limiting state.
        builder.Services.AddSingleton(_ => new AcrRateLimiter());
        // Transient handler gets instantiated for each client and uses the singleton rate limiter.
        builder.Services.AddTransient<AcrRateLimitingHandler>();

        builder.Services.ConfigureHttpClientDefaults(httpClientBuilder =>
        {
            var retryOptions = new HttpRetryStrategyOptions()
            {
                MaxRetryAttempts = 5,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(3),
                UseJitter = true,
            };
            // Don't retry for requests that might not be idempotent
            retryOptions.DisableForUnsafeHttpMethods();

            // Retry should be the outer-most policy
            httpClientBuilder.AddResilienceHandler(
                "image-builder-retry",
                pipeline => pipeline.AddRetry(retryOptions));

            // ACR rate limiting must be per-retry-attempt, otherwise retries would eat up rate
            // limited requests without acquiring additional rate limiting leases/tokens.
            httpClientBuilder.AddHttpMessageHandler<AcrRateLimitingHandler>();

            // Per-request timeout covers individual requests, and doesn't apply to the outer
            // retry policy.
            httpClientBuilder.AddResilienceHandler(
                "image-builder-timeout",
                pipeline => pipeline.AddTimeout(TimeSpan.FromSeconds(10)));
        });

        builder.Services.AddSingleton<IImageCacheService, ImageCacheService>();
        builder.Services.AddSingleton<IKustoClient, KustoClientWrapper>();
        builder.Services.AddSingleton<ILifecycleMetadataService, LifecycleMetadataService>();
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<IManifestJsonService, ManifestJsonService>();
        builder.Services.AddSingleton<IManifestServiceFactory, ManifestServiceFactory>();
        builder.Services.AddSingleton<IMarImageIngestionReporter, MarImageIngestionReporter>();
        builder.Services.AddSingleton<IMcrStatusClientFactory, McrStatusClientFactory>();
        builder.Services.AddSingleton<INotificationService, NotificationService>();
        builder.Services.AddSingleton<Notation.INotationClient, Notation.NotationClient>();
        builder.Services.AddSingleton<IOctokitClientFactory, OctokitClientFactory>();
        builder.Services.AddSingleton<Oras.IOrasService, Oras.OrasDotNetService>();
        builder.Services.AddSingleton<Oras.IOrasServiceFactory, Oras.OrasServiceFactory>();
        builder.Services.AddSingleton<IProcessService, ProcessService>();
        builder.Services.AddSingleton<IRegistryResolver, RegistryResolver>();
        builder.Services.AddSingleton<IRegistryCredentialsProvider, RegistryCredentialsProvider>();
        builder.Services.AddSingleton<IVssConnectionFactory, VssConnectionFactory>();

        builder.Services.AddSingleton<IEsrpSigningService, EsrpSigningService>();
        builder.Services.AddSingleton<IImageSigningService, ImageSigningService>();

        // Commands
        builder.Services.AddSingleton<ICommand, AnnotateEolDigestsCommand>();
        builder.Services.AddSingleton<ICommand, BuildCommand>();
        builder.Services.AddSingleton<ICommand, CleanAcrImagesCommand>();
        builder.Services.AddSingleton<ICommand, CopyAcrImagesCommand>();
        builder.Services.AddSingleton<ICommand, CopyBaseImagesCommand>();
        builder.Services.AddSingleton<ICommand, CreateManifestListCommand>();
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
        builder.Services.AddSingleton<ICommand, PublishMcrDocsCommand>();
        builder.Services.AddSingleton<ICommand, PullImagesCommand>();
        builder.Services.AddSingleton<ICommand, QueueBuildCommand>();
        builder.Services.AddSingleton<ICommand, ShowImageStatsCommand>();
        builder.Services.AddSingleton<ICommand, ShowManifestSchemaCommand>();
        builder.Services.AddSingleton<ICommand, SignImagesCommand>();
        builder.Services.AddSingleton<ICommand, TrimUnchangedPlatformsCommand>();
        builder.Services.AddSingleton<ICommand, VerifySignaturesCommand>();
        builder.Services.AddSingleton<ICommand, WaitForMarAnnotationIngestionCommand>();
        builder.Services.AddSingleton<ICommand, WaitForMcrDocIngestionCommand>();
        builder.Services.AddSingleton<ICommand, WaitForMcrImageIngestionCommand>();

        return builder.Build();
    }
}
