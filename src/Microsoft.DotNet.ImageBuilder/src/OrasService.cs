﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IOrasService))]
    public class OrasService : IOrasService
    {
        private const string LifecycleArtifactType = "application/vnd.microsoft.artifact.lifecycle";

        public bool IsDigestAnnotatedForEol(string digest, bool isDryRun)
        {
            string? stdOut = ExecuteHelper.ExecuteWithRetry(
                "oras",
                $"discover --artifact-type {LifecycleArtifactType} {digest}",
                isDryRun);

            if (!string.IsNullOrEmpty(stdOut) && stdOut.Contains("Discovered 0 artifact"))
            {
                return false;
            }

            return true;
        }

        public bool AnnotateEolDigest(string digest, DateOnly? date, ILoggerService loggerService, bool isDryRun)
        {
            try
            {
                ExecuteHelper.ExecuteWithRetry(
                    "oras",
                    $"attach --artifact-type {LifecycleArtifactType} --annotation \"vnd.microsoft.artifact.lifecycle.end-of-life.date={date:yyyy-MM-dd}\" {digest}",
                    isDryRun);
            }
            catch (InvalidOperationException ex)
            {
                loggerService.WriteError($"Failed to annotate EOL for digest '{digest}': {ex.Message}");
                return false;
            }

            return true;
        }
    }
}
