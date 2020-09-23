// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class WaitForMcrImageIngestionOptions : ManifestOptions
    {
        public const string MinimumQueueTimeOptionName = "min-queue-time";

        protected override string CommandHelp => "Waits for images to complete ingestion into MCR";

        public string ImageInfoPath { get; set; }

        public DateTime MinimumQueueTime { get; set; }

        public TimeSpan WaitTimeout { get; set; }

        public TimeSpan RequeryDelay { get; set; }

        public ServicePrincipalOptions ServicePrincipal { get; } = new ServicePrincipalOptions();

        public override void DefineParameters(ArgumentSyntax syntax)
        {
            base.DefineParameters(syntax);

            string imageInfoPath = null;
            syntax.DefineParameter("image-info", ref imageInfoPath, "Path to image info file");
            ImageInfoPath = imageInfoPath;

            ServicePrincipal.DefineParameters(syntax);
        }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            DateTime minimumQueueTime = DateTime.MinValue;
            syntax.DefineOption(MinimumQueueTimeOptionName, ref minimumQueueTime,
                val => string.IsNullOrEmpty(val) ? minimumQueueTime : DateTime.Parse(val),
                "Minimum queue time an image must have to be awaited");
            MinimumQueueTime = minimumQueueTime.ToUniversalTime();

            TimeSpan waitTimeout = TimeSpan.FromMinutes(20);
            syntax.DefineOption("timeout", ref waitTimeout,
                val => string.IsNullOrEmpty(val) ? waitTimeout : TimeSpan.Parse(val),
                $"Maximum time to wait for image ingestion (default: {waitTimeout})");
            WaitTimeout = waitTimeout;

            TimeSpan requeryDelay = TimeSpan.FromSeconds(10);
            syntax.DefineOption("requery-delay", ref requeryDelay,
                val => string.IsNullOrEmpty(val) ? requeryDelay : TimeSpan.Parse(val),
                $"Amount of time to wait before requerying the status of an image (default: {requeryDelay})");
            RequeryDelay = requeryDelay;
        }
    }
}
