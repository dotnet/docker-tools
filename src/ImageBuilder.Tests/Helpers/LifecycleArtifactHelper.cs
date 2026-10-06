// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Oras;

namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers;

internal static class LifecycleArtifactHelper
{
    public static LifecycleArtifact CreateLifecycleArtifact(string reference, DateOnly? endOfLifeDate = null)
    {
        Dictionary<string, string> annotations = [];
        if (endOfLifeDate is DateOnly date)
        {
            annotations[LifecycleAnnotations.EndOfLife] = date.ToString(LifecycleAnnotations.EndOfLifeDateFormat);
        }

        return new LifecycleArtifact(
            new ReferrerInfo(reference, OciArtifactType.Lifecycle) { Annotations = annotations });
    }
}
