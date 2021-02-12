// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Subscription;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    public static class SubscriptionHelper
    {
        public static async Task<IEnumerable<(Subscription Subscription, ManifestInfo Manifest)>> GetSubscriptionManifestsAsync(
            string subscriptionsPath, ManifestFilterOptions filterOptions, HttpClient httpClient, Action<ManifestOptions>? configureOptions = null)
        {
            string subscriptionsJson = File.ReadAllText(subscriptionsPath);
            Subscription[] subscriptions = JsonConvert.DeserializeObject<Subscription[]>(subscriptionsJson);

            List<(Subscription Subscription, ManifestInfo Manifest)> subscriptionManifests = new
                List<(Subscription Subscription, ManifestInfo Manifest)>();
            foreach (Subscription subscription in subscriptions)
            {
                ManifestInfo? manifest = await GetSubscriptionManifestAsync(subscription, filterOptions, httpClient, configureOptions);
                if (manifest is not null)
                {
                    subscriptionManifests.Add((subscription, manifest));
                }
            }

            return subscriptionManifests;
        }

        private static async Task<ManifestInfo?> GetSubscriptionManifestAsync(Subscription subscription,
            ManifestFilterOptions filterOptions, HttpClient httpClient, Action<ManifestOptions>? configureOptions)
        {
            // If the command is filtered with an OS type that does not match the OsType filter of the subscription,
            // then there are no images that need to be inspected.
            string osTypeRegexPattern = ManifestFilter.GetFilterRegexPattern(filterOptions.OsType);
            if (!string.IsNullOrEmpty(subscription.OsType) &&
                !Regex.IsMatch(subscription.OsType, osTypeRegexPattern, RegexOptions.IgnoreCase))
            {
                return null;
            }

            string repoPath = await GitHelper.DownloadAndExtractGitRepoArchiveAsync(httpClient, subscription.Manifest);
            try
            {
                TempManifestOptions manifestOptions = new TempManifestOptions(filterOptions)
                {
                    Manifest = Path.Combine(repoPath, subscription.Manifest.Path),
                };

                configureOptions?.Invoke(manifestOptions);

                return ManifestInfo.Load(manifestOptions);
            }
            finally
            {
                // The path to the repo is stored inside a zip extraction folder so be sure to delete that
                // zip extraction folder, not just the inner repo folder.
                Directory.Delete(new DirectoryInfo(repoPath).Parent!.FullName, true);
            }
        }

        private class TempManifestOptions : ManifestOptions, IFilterableOptions
        {
            public TempManifestOptions(ManifestFilterOptions filterOptions)
            {
                FilterOptions = filterOptions;
            }

            public ManifestFilterOptions FilterOptions { get; }
        }
    }
}
#nullable disable
