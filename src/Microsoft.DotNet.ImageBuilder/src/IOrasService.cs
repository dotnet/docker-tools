﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.ImageBuilder.Models.Oras;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    public interface IOrasService
    {
        bool IsDigestAnnotatedForEol(string digest, ILoggerService loggerService, bool isDryRun, [MaybeNullWhen(false)] out OciManifest lifecycleArtifactManifest);

        bool AnnotateEolDigest(string digest, DateOnly date, ILoggerService loggerService, bool isDryRun, [MaybeNullWhen(false)] out OciManifest lifecycleArtifactManifest);
    }
}
#nullable disable
