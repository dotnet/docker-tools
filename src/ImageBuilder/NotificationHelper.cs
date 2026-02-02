// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class NotificationHelper
    {
        private const string MetadataPrefix = "<!--NOTIFICATION METADATA:";
        private const string MetadataSuffix = "-->";
        private const string MetadataGroup = "metadata";
        private static string MetadataRegex = $"{MetadataPrefix}(?<{MetadataGroup}>.+){MetadataSuffix}";

        /// <summary>
        /// Extracts the notification metadata out of a GitHub notification issue.
        /// </summary>
        public static T? GetNotificationMetadata<T>(string issueBody)
        {
            Match match = Regex.Match(issueBody, MetadataRegex, RegexOptions.Singleline);
            if (match.Success)
            {
                string content = match.Groups[MetadataGroup].Value;
                return JsonConvert.DeserializeObject<T>(content);
            }

            return default;
        }

        /// <summary>
        /// Serializes and formats the metadata object to be inserted as HTML within a GitHub notification issue.
        /// </summary>
        public static string FormatNotificationMetadata<T>(T metadata)
        {
            string json = JsonConvert.SerializeObject(metadata, Formatting.Indented);
            return MetadataPrefix + json + MetadataSuffix;
        }
    }
}
