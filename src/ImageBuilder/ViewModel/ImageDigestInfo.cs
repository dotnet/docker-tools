// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.ViewModel;

public record ImageDigestInfo(string Digest, List<string> Tags, bool IsManifestList);
