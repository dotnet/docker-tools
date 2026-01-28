#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Microsoft.DotNet.ImageBuilder.Models.McrStatus
{
    public class ImageSubstatus
    {
        /// <summary>
        /// The stage that processes a request in response to the web hook trigger.
        /// </summary>
        public StageStatus Initialization { get; set; }

        /// <summary>
        /// The stage where images are imported into the internal ACR registry msint.
        /// </summary>
        public StageStatus MsInt { get; set; }

        /// <summary>
        /// The stage where images are imported into MCR.
        /// </summary>
        public StageStatus McrProd { get; set; }

        /// <summary>
        /// The stage that waits for mcr.microsoft.com to reflect the information on the recently onboarded image.
        /// </summary>
        public StageStatus TagConsistency { get; set; }

        /// <summary>
        /// The stages wehre the repository catalog is updated to reflect the recently onboarded image.
        /// </summary>
        public StageStatus CatalogUpdate { get; set; }

        /// <summary>
        /// The stage where images are syndicated to Docker Hub.
        /// </summary>
        public StageStatus ImageSyndication { get; set; }

        /// <summary>
        /// The stage where readmes are updated in Docker Hub.
        /// </summary>
        public StageStatus ContentSyndication { get; set; }

        /// <summary>
        /// The stage which validates that the readme on Docker Hub represents the most recent version of the readme checked into GitHub.
        /// </summary>
        public StageStatus ValidateNoDocRepoChanges { get; set; }

        public override string ToString()
        {
            return ToString(null);
        }

        public string ToString(string prefix)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"{prefix}Initialization status: {Initialization}");
            stringBuilder.AppendLine($"{prefix}Msint status: {MsInt}");
            stringBuilder.AppendLine($"{prefix}MCR prod status: {McrProd}");
            stringBuilder.AppendLine($"{prefix}Tag consistency status: {TagConsistency}");
            stringBuilder.AppendLine($"{prefix}Catalog update status: {CatalogUpdate}");
            stringBuilder.AppendLine($"{prefix}Image syndication status: {ImageSyndication}");
            stringBuilder.AppendLine($"{prefix}Content syndication status: {ContentSyndication}");
            stringBuilder.AppendLine($"{prefix}Validate no doc repo changes status: {ValidateNoDocRepoChanges}");
            return stringBuilder.ToString();
        }
    }
}
